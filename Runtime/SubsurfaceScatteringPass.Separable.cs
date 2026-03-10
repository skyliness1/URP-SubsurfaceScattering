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
        // Minimum world scale to prevent division by zero in mm→world conversion
        private const float kMinWorldScale = 0.0001f;
        // Depth falloff compensation factor.
        // The depth follow shader formula uses: depthFalloff * 10 * distToProj * |Δd|.
        // Previously distToProj was ~3x larger (due to FOV/3 approximation), so we compensate
        // to maintain similar depth rejection sensitivity after switching to correct projection.
        private const float kDepthFalloffCompensation = 3.0f;
        
        // 4S material and kernel
        private Material m_separableSSSMaterial;
        private Vector4[] m_separableKernel = new Vector4[kSeparableSampleCount];
        private RTHandle m_separableTempRT;
        
        // 4S mode flag
        private bool m_useSeparableSSS;
        
        // 4S quality parameters
        private float m_separableWidth;
        private float m_separableDepthFalloff;
        private bool m_separableFollowSurface;
        
        // Profiling
        private readonly ProfilingSampler m_separableSSSHSampler = new ProfilingSampler("SSS Separable Horizontal");
        private readonly ProfilingSampler m_separableSSSVSampler = new ProfilingSampler("SSS Separable Vertical");
        
        // Shader property IDs for separable SSS
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int SSSSDirection = Shader.PropertyToID("_SSSSDirection");
        private static readonly int SSSSDepthFalloff = Shader.PropertyToID("_SSSSDepthFalloff");
        private static readonly int DistanceToProjectionWindow = Shader.PropertyToID("_DistanceToProjectionWindow");
        private static readonly int Kernel = Shader.PropertyToID("_Kernel");
        private static readonly int SeparableTempTarget = Shader.PropertyToID("_SeparableSSSTemp");
        
        /// <summary>
        /// Initialize the separable SSS resources.
        /// </summary>
        private void InitializeSeparableSSS(Shader separableSSSShader, bool useSeparable,
            float width, float depthFalloff, bool followSurface)
        {
            m_useSeparableSSS = useSeparable;
            m_separableWidth = width;
            m_separableDepthFalloff = depthFalloff;
            m_separableFollowSurface = followSurface;
            if (separableSSSShader != null)
            {
                m_separableSSSMaterial = CoreUtils.CreateEngineMaterial(separableSSSShader);
                m_separableSSSMaterial.SetInt(SSSShaderIDs._StencilRef, SSSShaderIDs.STENCILUSAGE_SUBSURFACE_SCATTERING);
            }
        }
        
        /// <summary>
        /// Cleanup separable SSS resources.
        /// </summary>
        private void CleanupSeparableSSS()
        {
            CoreUtils.Destroy(m_separableSSSMaterial);
            m_separableSSSMaterial = null;
            m_separableTempRT?.Release();
            m_separableTempRT = null;
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
                m_separableKernel[i].w = RANGE * sign * Mathf.Abs(Mathf.Pow(o, EXPONENT)) / Mathf.Pow(RANGE, EXPONENT);
            }
            
            // Calculate the weights from the profile
            for (int i = 0; i < kSeparableSampleCount; i++)
            {
                float w0 = i > 0 ? Mathf.Abs(m_separableKernel[i].w - m_separableKernel[i - 1].w) : 0.0f;
                float w1 = i < kSeparableSampleCount - 1 ? Mathf.Abs(m_separableKernel[i].w - m_separableKernel[i + 1].w) : 0.0f;
                float area = (w0 + w1) / 2.0f;
                Vector3 tt = area * SeparableProfile(m_separableKernel[i].w, falloff);
                m_separableKernel[i].x = tt.x;
                m_separableKernel[i].y = tt.y;
                m_separableKernel[i].z = tt.z;
            }
            
            // Move center sample (offset=0) to index 0
            Vector4 center = m_separableKernel[kSeparableSampleCount / 2];
            for (int i = kSeparableSampleCount / 2; i > 0; i--)
            {
                m_separableKernel[i] = m_separableKernel[i - 1];
            }
            m_separableKernel[0] = center;
            
            // Normalize the weights per-channel
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < kSeparableSampleCount; i++)
            {
                sum.x += m_separableKernel[i].x;
                sum.y += m_separableKernel[i].y;
                sum.z += m_separableKernel[i].z;
            }
            for (int i = 0; i < kSeparableSampleCount; i++)
            {
                m_separableKernel[i].x /= sum.x;
                m_separableKernel[i].y /= sum.y;
                m_separableKernel[i].z /= sum.z;
            }
            
            // Apply strength: center = lerp(1, weight, strength), others *= strength
            m_separableKernel[0].x = (1.0f - strength.x) * 1.0f + strength.x * m_separableKernel[0].x;
            m_separableKernel[0].y = (1.0f - strength.y) * 1.0f + strength.y * m_separableKernel[0].y;
            m_separableKernel[0].z = (1.0f - strength.z) * 1.0f + strength.z * m_separableKernel[0].z;
            
            for (int i = 1; i < kSeparableSampleCount; i++)
            {
                m_separableKernel[i].x *= strength.x;
                m_separableKernel[i].y *= strength.y;
                m_separableKernel[i].z *= strength.z;
            }
        }
        
        /// <summary>
        /// Derive 4S kernel parameters from the primary DiffusionProfile.
        /// Maps scatteringDistance → falloff ratio, filterRadius → width.
        /// </summary>
        private void UpdateSeparableKernelFromProfile()
        {
            if (m_DiffusionProfiles == null || m_DiffusionProfiles.Length == 0)
            {
                return;
            }
               
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
            {
                return;
            }
               
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
            if (m_separableSSSMaterial == null)
            {
                return;
            }
               
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
            RenderingUtils.ReAllocateIfNeeded(ref m_separableTempRT, tempDesc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_SeparableSSSTemp");
            // Update kernel from diffusion profile
            UpdateSeparableKernelFromProfile();
            // Use the camera's actual projection matrix for correct FOV handling.
            // projectionMatrix.m11 = 1/tan(FOV/2), which correctly maps world-space vertical
            // offsets to clip space. This ensures consistent blur across all cameras (Scene, Game, etc.)
            // because different cameras with different FOVs all use their correct projection.
            float distanceToProjectionWindow = camera.projectionMatrix.m11;
            // Set material properties
            m_separableSSSMaterial.SetVectorArray("_Kernel", m_separableKernel);
            // Compensate depth falloff for the correct projection (previously distToProj was ~3x
            // larger due to the FOV/3 approximation, so we scale to maintain similar sensitivity)
            m_separableSSSMaterial.SetFloat("_SSSSDepthFalloff", m_separableDepthFalloff * kDepthFalloffCompensation);
            m_separableSSSMaterial.SetFloat("_DistanceToProjectionWindow", distanceToProjectionWindow);
            
            /*
            if (m_separableFollowSurface)
            {
                m_separableSSSMaterial.EnableKeyword("SSSS_FOLLOW_SURFACE");
            }
            */
            
            m_separableSSSMaterial.DisableKeyword("SSSS_FOLLOW_SURFACE");
               
            // Derive width from primary profile's filter radius and world scale
            float width = m_separableWidth;
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
                        float scatterRadiusWorld = filterRadius / (kMillimetersPerMeter * Mathf.Max(worldScale, kMinWorldScale));
                        // Convert to the width parameter the shader expects
                        width = scatterRadiusWorld * (height * 0.5f) * m_separableWidth;
                        break;
                    }
                }
            }
            // === Horizontal blur pass ===
            using (new ProfilingScope(cmd, m_separableSSSHSampler))
            {
                // Copy diffuse lighting to temp RT
                cmd.Blit(m_DiffuseRT, m_separableTempRT);
                // Set horizontal direction
                m_separableSSSMaterial.SetVector("_SSSSDirection", new Vector4(width, 0f, 0f, 0f));
                cmd.SetGlobalTexture(MainTex, m_separableTempRT);
                // Render to filtering RT with stencil test from depth buffer
                CoreUtils.SetRenderTarget(cmd, m_FilteringRT,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    depthTarget,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.DontCare);
                Blitter.BlitTexture(cmd, m_separableTempRT, new Vector4(1, 1, 0, 0), m_separableSSSMaterial, 0);
            }
            
            // === Vertical blur pass ===
            using (new ProfilingScope(cmd, m_separableSSSVSampler))
            {
                // Copy horizontal result to temp RT
                cmd.Blit(m_FilteringRT, m_separableTempRT);
                // Set vertical direction
                m_separableSSSMaterial.SetVector("_SSSSDirection", new Vector4(0f, width, 0f, 0f));
                cmd.SetGlobalTexture(MainTex, m_separableTempRT);
                // Render to filtering RT with stencil test from depth buffer
                CoreUtils.SetRenderTarget(cmd, m_FilteringRT,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    depthTarget,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.DontCare);
                Blitter.BlitTexture(cmd, m_separableTempRT, new Vector4(1, 1, 0, 0), m_separableSSSMaterial, 0);
            }
        }
        /// <summary>
        /// Dispose separable SSS legacy resources.
        /// </summary>
        private void DisposeSeparableLegacy()
        {
            m_separableTempRT?.Release();
            m_separableTempRT = null;
        }
    }
}
