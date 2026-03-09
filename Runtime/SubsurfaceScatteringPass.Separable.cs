using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SoulRender
{
    /// <summary>
    /// Separable (4S) subsurface scattering rendering path.
    /// Uses two-pass separable blur (horizontal + vertical) instead of compute shader,
    /// making it more suitable for low-end PCs and mobile devices.
    /// </summary>
    public partial class SubsurfaceScatteringPass
    {
        // 4S constants
        private const int kSeparableSampleCount = 11;
        // Epsilon to prevent division by zero in Gaussian falloff calculation
        private const float kFalloffEpsilon = 0.001f;
        // Minimum per-channel falloff to avoid extremely narrow Gaussians that cause artifacts
        private const float kMinFalloffThreshold = 0.05f;
        // Millimeters per meter (for converting filter radius from mm to world units)
        private const float kMillimetersPerMeter = 1000.0f;
        // Depth falloff compensation factor.
        // The depth follow shader formula uses: depthFalloff * 10 * distToProj * |Δd|.
        // Previously distToProj was ~3x larger (due to FOV/3 approximation), so we compensate
        // to maintain similar depth rejection sensitivity after switching to correct projection.
        private const float kDepthFalloffCompensation = 3.0f;

        // 4S material and kernel
        private Material m_SeparableSSSMaterial;
        private Vector4[] m_SeparableKernel = new Vector4[kSeparableSampleCount];
        private RTHandle m_SeparableTempRT;

        // 4S mode flag
        private bool m_UseSeparableSSS;

        // 4S quality parameters
        private float m_SeparableWidth;
        private float m_SeparableDepthFalloff;
        private bool m_SeparableFollowSurface;

        // Profiling
        private readonly ProfilingSampler m_SeparableSSSHSampler = new ProfilingSampler("SSS Separable Horizontal");
        private readonly ProfilingSampler m_SeparableSSSVSampler = new ProfilingSampler("SSS Separable Vertical");

        // Shader property IDs for separable SSS
        private static readonly int _MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int _SSSSDirection = Shader.PropertyToID("_SSSSDirection");
        private static readonly int _SSSSDepthFalloff = Shader.PropertyToID("_SSSSDepthFalloff");
        private static readonly int _DistanceToProjectionWindow = Shader.PropertyToID("_DistanceToProjectionWindow");
        private static readonly int _Kernel = Shader.PropertyToID("_Kernel");
        private static readonly int _SeparableTempTarget = Shader.PropertyToID("_SeparableSSSTemp");

        /// <summary>
        /// Initialize the separable SSS resources.
        /// </summary>
        private void InitializeSeparableSSS(Shader separableSSSShader, bool useSeparable,
            float width, float depthFalloff, bool followSurface)
        {
            m_UseSeparableSSS = useSeparable;
            m_SeparableWidth = width;
            m_SeparableDepthFalloff = depthFalloff;
            m_SeparableFollowSurface = followSurface;

            if (separableSSSShader != null)
            {
                m_SeparableSSSMaterial = CoreUtils.CreateEngineMaterial(separableSSSShader);
                m_SeparableSSSMaterial.SetInt(SSSShaderIDs._StencilRef, SSSShaderIDs.STENCILUSAGE_SUBSURFACE_SCATTERING);
            }
        }

        /// <summary>
        /// Cleanup separable SSS resources.
        /// </summary>
        private void CleanupSeparableSSS()
        {
            CoreUtils.Destroy(m_SeparableSSSMaterial);
            m_SeparableSSSMaterial = null;
            m_SeparableTempRT?.Release();
            m_SeparableTempRT = null;
        }

        /// <summary>
        /// Gaussian function for the d'Eon skin profile.
        /// Falloff modulates the shape: large falloff = wider, small falloff = narrower.
        /// </summary>
        private Vector3 SeparableGaussian(float variance, float r, Vector3 falloff)
        {
            Vector3 g = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                float rr = r / (kFalloffEpsilon + falloff[i]);
                g[i] = Mathf.Exp(-(rr * rr) / (2.0f * variance)) / (2.0f * Mathf.PI * variance);
            }
            return g;
        }

        /// <summary>
        /// Sum-of-Gaussians skin profile from d'Eon 2007.
        /// Uses the red channel profile for all channels, scaled by falloff.
        /// </summary>
        private Vector3 SeparableProfile(float r, Vector3 falloff)
        {
            return 0.100f * SeparableGaussian(0.0484f, r, falloff) +
                   0.118f * SeparableGaussian(0.187f, r, falloff) +
                   0.113f * SeparableGaussian(0.567f, r, falloff) +
                   0.358f * SeparableGaussian(1.99f, r, falloff) +
                   0.078f * SeparableGaussian(7.41f, r, falloff);
        }

        /// <summary>
        /// Calculate the 11-sample separable kernel using strength and falloff parameters.
        /// Adapted from Refer implementation.
        /// </summary>
        private void CalculateSeparableKernel(Vector3 strength, Vector3 falloff)
        {
            const float RANGE = kSeparableSampleCount > 20 ? 3.0f : 2.0f;
            const float EXPONENT = 2.0f;

            // Calculate the offsets with power-curve distribution
            float step = 2.0f * RANGE / (kSeparableSampleCount - 1);
            for (int i = 0; i < kSeparableSampleCount; i++)
            {
                float o = -RANGE + i * step;
                float sign = o < 0.0f ? -1.0f : 1.0f;
                m_SeparableKernel[i].w = RANGE * sign * Mathf.Abs(Mathf.Pow(o, EXPONENT)) / Mathf.Pow(RANGE, EXPONENT);
            }

            // Calculate the weights from the profile
            for (int i = 0; i < kSeparableSampleCount; i++)
            {
                float w0 = i > 0 ? Mathf.Abs(m_SeparableKernel[i].w - m_SeparableKernel[i - 1].w) : 0.0f;
                float w1 = i < kSeparableSampleCount - 1 ? Mathf.Abs(m_SeparableKernel[i].w - m_SeparableKernel[i + 1].w) : 0.0f;
                float area = (w0 + w1) / 2.0f;
                Vector3 tt = area * SeparableProfile(m_SeparableKernel[i].w, falloff);
                m_SeparableKernel[i].x = tt.x;
                m_SeparableKernel[i].y = tt.y;
                m_SeparableKernel[i].z = tt.z;
            }

            // Move center sample (offset=0) to index 0
            Vector4 center = m_SeparableKernel[kSeparableSampleCount / 2];
            for (int i = kSeparableSampleCount / 2; i > 0; i--)
                m_SeparableKernel[i] = m_SeparableKernel[i - 1];
            m_SeparableKernel[0] = center;

            // Normalize the weights per-channel
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < kSeparableSampleCount; i++)
            {
                sum.x += m_SeparableKernel[i].x;
                sum.y += m_SeparableKernel[i].y;
                sum.z += m_SeparableKernel[i].z;
            }

            for (int i = 0; i < kSeparableSampleCount; i++)
            {
                m_SeparableKernel[i].x /= sum.x;
                m_SeparableKernel[i].y /= sum.y;
                m_SeparableKernel[i].z /= sum.z;
            }

            // Apply strength: center = lerp(1, weight, strength), others *= strength
            m_SeparableKernel[0].x = (1.0f - strength.x) * 1.0f + strength.x * m_SeparableKernel[0].x;
            m_SeparableKernel[0].y = (1.0f - strength.y) * 1.0f + strength.y * m_SeparableKernel[0].y;
            m_SeparableKernel[0].z = (1.0f - strength.z) * 1.0f + strength.z * m_SeparableKernel[0].z;

            for (int i = 1; i < kSeparableSampleCount; i++)
            {
                m_SeparableKernel[i].x *= strength.x;
                m_SeparableKernel[i].y *= strength.y;
                m_SeparableKernel[i].z *= strength.z;
            }
        }

        /// <summary>
        /// Derive 4S kernel parameters from the primary DiffusionProfile.
        /// Maps scatteringDistance → falloff ratio, filterRadius → width.
        /// </summary>
        private void UpdateSeparableKernelFromProfile()
        {
            if (m_DiffusionProfiles == null || m_DiffusionProfiles.Length == 0)
                return;

            // Use the first valid diffusion profile
            DiffusionProfileSettings primaryProfile = null;
            for (int i = 0; i < m_DiffusionProfiles.Length; i++)
            {
                if (m_DiffusionProfiles[i] != null)
                {
                    primaryProfile = m_DiffusionProfiles[i];
                    break;
                }
            }

            if (primaryProfile == null || primaryProfile.profile == null)
                return;

            var profile = primaryProfile.profile;

            // Derive falloff from scattering distance ratios
            // Larger scattering distance = wider scatter = higher falloff value
            float sd_r = profile.scatteringDistance.r * profile.scatteringDistanceMultiplier;
            float sd_g = profile.scatteringDistance.g * profile.scatteringDistanceMultiplier;
            float sd_b = profile.scatteringDistance.b * profile.scatteringDistanceMultiplier;
            float maxSD = Mathf.Max(sd_r, Mathf.Max(sd_g, sd_b));

            Vector3 falloff;
            if (maxSD > 0.0001f)
            {
                // Normalize so the channel with largest scatter gets 1.0
                falloff = new Vector3(sd_r / maxSD, sd_g / maxSD, sd_b / maxSD);
                // Clamp minimum to avoid division by zero in Gaussian
                falloff.x = Mathf.Max(falloff.x, kMinFalloffThreshold);
                falloff.y = Mathf.Max(falloff.y, kMinFalloffThreshold);
                falloff.z = Mathf.Max(falloff.z, kMinFalloffThreshold);
            }
            else
            {
                falloff = Vector3.one;
            }

            // Strength: full for all channels since MRT pipeline handles per-pixel masking
            Vector3 strength = Vector3.one;

            CalculateSeparableKernel(strength, falloff);
        }

        /// <summary>
        /// Execute the separable 4S SSS filtering pass (replaces compute shader filtering).
        /// Performs horizontal then vertical blur on the diffuse lighting buffer.
        /// </summary>
        private void ExecuteSeparableSSSFiltering(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (m_SeparableSSSMaterial == null)
                return;

            var cameraData = renderingData.cameraData;
            var camera = cameraData.camera;
            RTHandle depthTarget = cameraData.renderer.cameraDepthTargetHandle;
            var cameraDescriptor = cameraData.cameraTargetDescriptor;

            // Allocate temp RT matching the filtering buffer
            var tempDesc = cameraDescriptor;
            tempDesc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32;
            tempDesc.depthBufferBits = 0;
            tempDesc.msaaSamples = 1;
            tempDesc.useDynamicScale = false;
            RenderingUtils.ReAllocateIfNeeded(ref m_SeparableTempRT, tempDesc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_SeparableSSSTemp");

            // Update kernel from diffusion profile
            UpdateSeparableKernelFromProfile();

            // Use the camera's actual projection matrix for correct FOV handling.
            // projectionMatrix.m11 = 1/tan(FOV/2), which correctly maps world-space vertical
            // offsets to clip space. This ensures consistent blur across all cameras (Scene, Game, etc.)
            // because different cameras with different FOVs all use their correct projection.
            float distanceToProjectionWindow = camera.projectionMatrix.m11;

            // Set material properties
            m_SeparableSSSMaterial.SetVectorArray("_Kernel", m_SeparableKernel);
            // Compensate depth falloff for the correct projection (previously distToProj was ~3x
            // larger due to the FOV/3 approximation, so we scale to maintain similar sensitivity)
            m_SeparableSSSMaterial.SetFloat("_SSSSDepthFalloff", m_SeparableDepthFalloff * kDepthFalloffCompensation);
            m_SeparableSSSMaterial.SetFloat("_DistanceToProjectionWindow", distanceToProjectionWindow);

            if (m_SeparableFollowSurface)
                m_SeparableSSSMaterial.EnableKeyword("SSSS_FOLLOW_SURFACE");
            else
                m_SeparableSSSMaterial.DisableKeyword("SSSS_FOLLOW_SURFACE");

            // Derive width from DiffusionProfile with physically correct conversion.
            //
            // In the 5S compute shader, the blur is computed by:
            //   1. Converting filterRadius (mm) to world units: R_world = filterRadius / (1000 * worldScale)
            //   2. Computing pixelsPerMm from the camera's inverse projection matrix
            //   3. Sampling at positions: pixelCoord + round(pixelsPerMm * r * direction)
            //
            // For the 4S shader, the blur formula is:
            //   pixel_offset = width * distToProj / depth * kernel_offset
            //
            // To match the 5S physical scatter, we need:
            //   width * distToProj / depth = R_world * projScale * (height/2) / depth
            //   where projScale = 1/tan(FOV/2) = distToProj
            //
            // This simplifies to: width = R_world * height / 2
            // With user control: width = R_world * height / 2 * separableWidth
            //
            // This makes the blur:
            //   - Correctly scale with camera FOV (via distToProj)
            //   - Correctly scale with resolution (via height)
            //   - Match the 5S physical scatter when separableWidth ≈ 0.5
            float width = m_SeparableWidth;
            float height = cameraDescriptor.height;
            if (m_DiffusionProfiles != null)
            {
                for (int i = 0; i < m_DiffusionProfiles.Length; i++)
                {
                    if (m_DiffusionProfiles[i] != null)
                    {
                        float filterRadius = m_DiffusionProfiles[i].profile.filterRadius; // In mm
                        float worldScale = m_DiffusionProfiles[i].profile.worldScale;     // meters per unit
                        // Convert filter radius from mm to world units
                        float scatterRadiusWorld = filterRadius / (kMillimetersPerMeter * Mathf.Max(worldScale, 0.0001f));
                        // Convert to the width parameter the shader expects
                        width = scatterRadiusWorld * (height * 0.5f) * m_SeparableWidth;
                        break;
                    }
                }
            }

            // === Horizontal blur pass ===
            using (new ProfilingScope(cmd, m_SeparableSSSHSampler))
            {
                // Copy diffuse lighting to temp RT
                cmd.Blit(m_DiffuseRT, m_SeparableTempRT);

                // Set horizontal direction
                m_SeparableSSSMaterial.SetVector("_SSSSDirection", new Vector4(width, 0f, 0f, 0f));
                cmd.SetGlobalTexture(_MainTex, m_SeparableTempRT);

                // Render to filtering RT with stencil test from depth buffer
                CoreUtils.SetRenderTarget(cmd, m_FilteringRT,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    depthTarget,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.DontCare);

                Blitter.BlitTexture(cmd, m_SeparableTempRT, new Vector4(1, 1, 0, 0), m_SeparableSSSMaterial, 0);
            }

            // === Vertical blur pass ===
            using (new ProfilingScope(cmd, m_SeparableSSSVSampler))
            {
                // Copy horizontal result to temp RT
                cmd.Blit(m_FilteringRT, m_SeparableTempRT);

                // Set vertical direction
                m_SeparableSSSMaterial.SetVector("_SSSSDirection", new Vector4(0f, width, 0f, 0f));
                cmd.SetGlobalTexture(_MainTex, m_SeparableTempRT);

                // Render to filtering RT with stencil test from depth buffer
                CoreUtils.SetRenderTarget(cmd, m_FilteringRT,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    depthTarget,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.DontCare);

                Blitter.BlitTexture(cmd, m_SeparableTempRT, new Vector4(1, 1, 0, 0), m_SeparableSSSMaterial, 0);
            }
        }

        /// <summary>
        /// Dispose separable SSS legacy resources.
        /// </summary>
        private void DisposeSeparableLegacy()
        {
            m_SeparableTempRT?.Release();
            m_SeparableTempRT = null;
        }
    }
}
