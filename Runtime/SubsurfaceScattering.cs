using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using System;

namespace SoulRender
{
    /// <summary>
    /// SSS rendering mode.
    /// </summary>
    public enum SSSMode
    {
        [Tooltip("5S mode: Compute shader based bilateral filtering with Burley diffusion profile. High quality, requires compute shader support.")]
        FiveS = 0,
        [Tooltip("4S mode: Separable two-pass blur (horizontal + vertical). Lower cost, suitable for low-end PCs and mobile.")]
        FourS = 1
    }

    /// <summary>
    /// Subsurface Scattering Render Feature for URP
    /// </summary>
    public class SubsurfaceScattering : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            [Tooltip("SSS rendering mode.\nFiveS: Compute shader based (high quality, requires compute support).\nFourS: Separable blur (lower cost, mobile-friendly).")]
            public SSSMode sssMode = SSSMode.FiveS;

            [Tooltip("Diffusion profiles (will be serialized in build)")]
            public DiffusionProfileSettings[] diffusionProfiles = new DiffusionProfileSettings[0];
            
            [Tooltip("When to render the SSS buffer")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            
            [Tooltip("Layer mask for SSS objects")]
            public LayerMask layerMask = -1;

            // ===== 5S (Compute) Settings =====
            [Header("5S Settings (Compute-based)")]
            [Tooltip("Sets the sample budget of the Subsurface Scattering algorithm. Higher values result in better quality but slower performance.")]
            [Range(1, 128)]
            public int sampleBudget = 20;
            
            [Tooltip("Sets the custom number of downsample steps done to the source irradiance texture before it is used by the Subsurface Scattering algorithm. Higher value will improve performance, but might lower quality.")]
            [Range(0, 2)]
            public int downsampleSteps = 0;
            
            [Tooltip("Enable SubSurface-Scattering occlusion computation. Enabling this makes the SSS slightly more expensive but add great details to occluded zones with SSS materials.")]
            public bool subsurfaceScatteringAttenuation = false;
            
            [Tooltip("Global detail preservation for SSS.")]
            [Range(0, 1)]
            public float globalDetailPreservation = 0.5f;

            [Tooltip("Main SSS compute shader")]
            public ComputeShader subsurfaceScatteringCS;
            
            [Tooltip("Downsample compute shader")]
            public ComputeShader subsurfaceScatteringDownsampleCS;
            
            [Tooltip("Resolve stencil compute shader (for coarse stencil optimization)")]
            public ComputeShader resolveStencilCS;

            [Tooltip("Combine lighting shader (additive blend of SSS filtered diffuse with color buffer)")]
            public Shader combineLightingShader;

            // ===== 4S (Separable) Settings =====
            [Header("4S Settings (Separable blur)")]
            [Tooltip("Separable SSS blur shader")]
            public Shader separableSSSShader;

            [Tooltip("Width multiplier for the separable SSS blur. Controls overall blur radius derived from diffusion profile.")]
            [Range(0.01f, 5.0f)]
            public float separableWidth = 0.3f;

            [Tooltip("Controls how harshly depth discontinuities limit the blur. Higher values prevent more bleed across depth boundaries.")]
            [Range(0f, 3f)]
            public float separableDepthFalloff = 1.0f;

            [Tooltip("When enabled, prevents the blur from crossing depth discontinuities (e.g. skin edge against background).")]
            public bool separableFollowSurface = true;

            /// <summary>
            /// Validate that all required assets are assigned for the selected mode.
            /// </summary>
            public bool IsValid()
            {
                if (sssMode == SSSMode.FiveS)
                {
                    return subsurfaceScatteringCS != null &&
                           subsurfaceScatteringDownsampleCS != null &&
                           resolveStencilCS != null &&
                           combineLightingShader != null;
                }
                else // FourS
                {
                    return separableSSSShader != null &&
                           combineLightingShader != null;
                }
            }

            /// <summary>
            /// Validate platform capabilities for SSS.
            /// 5S requires compute shaders and specific texture formats with UAV support.
            /// 4S only requires basic render texture support (no compute required).
            /// </summary>
            public bool IsPlatformSupported()
            {
                if (sssMode == SSSMode.FourS)
                {
                    // 4S mode: Only need basic render texture support, no compute required
#if UNITY_6000_0_OR_NEWER || UNITY_2023_2_OR_NEWER
                    bool b10g11r11Render = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Render);
                    bool rgba8SrgbRender = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormatUsage.Render);
#else
                    bool b10g11r11Render = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Render);
                    bool rgba8SrgbRender = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, FormatUsage.Render);
#endif
                    return b10g11r11Render && rgba8SrgbRender;
                }

                // 5S mode: Full compute shader requirements
                if (!SystemInfo.supportsComputeShaders)
                {
                    return false;
                }

                // Exclude OpenGL platforms (GLES and OpenGLCore) - poor compute shader support
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || 
                    SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 ||
                    SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore)
                {
                    return false;
                }

#if UNITY_6000_0_OR_NEWER || UNITY_2023_2_OR_NEWER
                bool b10g11r11Render5S = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Render);
                bool b10g11r11Uav = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.LoadStore);
                bool rgba8SrgbRender5S = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormatUsage.Render);
                bool r32Uav = SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, GraphicsFormatUsage.LoadStore);
                bool rg8UintUav = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8_UInt, GraphicsFormatUsage.LoadStore);
                bool r8UintUav = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UInt, GraphicsFormatUsage.LoadStore);
#else
                bool b10g11r11Render5S = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Render);
                bool b10g11r11Uav = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.LoadStore);
                bool rgba8SrgbRender5S = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, FormatUsage.Render);
                bool r32Uav = SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, FormatUsage.LoadStore);
                bool rg8UintUav = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8_UInt, FormatUsage.LoadStore);
                bool r8UintUav = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UInt, FormatUsage.LoadStore);
#endif

                return b10g11r11Render5S && b10g11r11Uav && rgba8SrgbRender5S && 
                       r32Uav && rg8UintUav && r8UintUav;
            }
        }

        public Settings settings = new Settings();

        private SubsurfaceScatteringPass m_SSSPass;

        /// <summary>
        /// Check if the current platform supports SSS (mode-dependent)
        /// </summary>
        private bool IsPlatformSupported()
        {
            return settings.IsPlatformSupported();
        }

        public override void Create()
        {
            // Initialize subsurface scattering system with rendering settings
            m_SSSPass = new SubsurfaceScatteringPass();
            m_SSSPass.Initialize(
                settings.diffusionProfiles,
                settings.layerMask,
                settings.renderPassEvent,
                settings.subsurfaceScatteringCS,
                settings.subsurfaceScatteringDownsampleCS,
                settings.resolveStencilCS,
                settings.combineLightingShader,
                settings.sampleBudget,
                settings.downsampleSteps,
                settings.subsurfaceScatteringAttenuation,
                settings.globalDetailPreservation,
                settings.sssMode,
                settings.separableSSSShader,
                settings.separableWidth,
                settings.separableDepthFalloff,
                settings.separableFollowSurface);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!IsPlatformSupported())
            {
                return;
            }

            if (!settings.IsValid())
            {
                return;
            }
            
            if (settings.diffusionProfiles == null || settings.diffusionProfiles.Length == 0)
            {
                return;
            }

#if !UNITY_6000_0_OR_NEWER
            // Skip preview cameras in Legacy mode (non-RenderGraph) to avoid stencil buffer issues
            // Preview cameras often lack proper depth-stencil buffers which causes compute shader errors
            if (renderingData.cameraData.isPreviewCamera)
            {
                return;
            }
#endif
 
            m_SSSPass.UpdateDiffusionProfileSettings();
            
            renderer.EnqueuePass(m_SSSPass);
        }

        protected override void Dispose(bool disposing)
        {
            m_SSSPass?.Cleanup();
        }
    }
}

