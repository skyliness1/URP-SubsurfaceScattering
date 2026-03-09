using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using System;

namespace SoulRender
{
    /// <summary>
    /// Subsurface Scattering Render Feature for URP
    /// </summary>
    public class SubsurfaceScattering : ScriptableRendererFeature
    {
        /// <summary>Filter mode for the SSS pass.</summary>
        public enum SSSFilterMode
        {
            /// <summary>Disk-sampling compute shader (original quality path).</summary>
            DiskSampling,
            /// <summary>Two-pass separable 1D bilateral filter (faster, noise-free).</summary>
            SeparableFilter
        }

        [Serializable]
        public class Settings
        {
            [Tooltip("Diffusion profiles (will be serialized in build)")]
            public DiffusionProfileSettings[] diffusionProfiles = new DiffusionProfileSettings[0];
            
            [Tooltip("When to render the SSS buffer")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            
            [Tooltip("Layer mask for SSS objects")]
            public LayerMask layerMask = -1;

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

            [Tooltip("Filter mode: DiskSampling (original) or SeparableFilter (faster, mobile-friendly).")]
            public SSSFilterMode filterMode = SSSFilterMode.DiskSampling;

            [Tooltip("Separable filter compute shader (two-pass horizontal + vertical Burley blur)")]
            public ComputeShader subsurfaceScatteringSeparableCS;

            [Tooltip("Number of kernel taps for the separable filter.")]
            [Range(3, 25)]
            public int separableKernelSize = 11;

            /// <summary>
            /// Validate that all required assets are assigned
            /// </summary>
            public bool IsValid()
            {
                bool hasRequiredShaders = subsurfaceScatteringCS != null &&
                                         subsurfaceScatteringDownsampleCS != null &&
                                         resolveStencilCS != null &&
                                         combineLightingShader != null;

                if (filterMode == SSSFilterMode.SeparableFilter)
                    hasRequiredShaders = hasRequiredShaders && subsurfaceScatteringSeparableCS != null;
                
                return hasRequiredShaders;
            }

            /// <summary>
            /// Validate platform capabilities for SSS: compute shaders and required texture formats (UAV/Render).
            /// SSS requires B10G11R11, R8G8B8A8_SRGB, R8G8_UInt, R32_SFloat, and R8_UInt with UAV support.
            /// </summary>
            public static bool IsPlatformSupported()
            {
                // Require compute support
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
                bool b10g11r11Render = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Render);
                bool b10g11r11Uav = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.LoadStore);
                bool rgba8SrgbRender = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormatUsage.Render);
                bool r32Uav = SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, GraphicsFormatUsage.LoadStore);
                bool rg8UintUav = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8_UInt, GraphicsFormatUsage.LoadStore);
                bool r8UintUav = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UInt, GraphicsFormatUsage.LoadStore);
#else
                bool b10g11r11Render = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Render);
                bool b10g11r11Uav = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.LoadStore);
                bool rgba8SrgbRender = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, FormatUsage.Render);
                bool r32Uav = SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, FormatUsage.LoadStore);
                bool rg8UintUav = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8_UInt, FormatUsage.LoadStore);
                bool r8UintUav = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UInt, FormatUsage.LoadStore);
#endif

                return b10g11r11Render && b10g11r11Uav && rgba8SrgbRender && 
                       r32Uav && rg8UintUav && r8UintUav;
            }
        }

        public Settings settings = new Settings();

        private SubsurfaceScatteringPass m_SSSPass;

        /// <summary>
        /// Check if the current platform supports SSS
        /// </summary>
        private bool IsPlatformSupported()
        {
            return Settings.IsPlatformSupported();
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
                settings.filterMode,
                settings.subsurfaceScatteringSeparableCS,
                settings.separableKernelSize);
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

