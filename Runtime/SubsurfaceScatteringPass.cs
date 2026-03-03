using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SoulRender
{
    /// <summary>
    /// (Based on HDRP implementation adapted for URP)
    /// </summary>
    public partial class SubsurfaceScatteringPass : ScriptableRenderPass
    {
        // Internal data arrays 
        private Vector4[] m_ShapeParamsAndMaxScatterDists;
        private Vector4[] m_TransmissionTintsAndFresnel0;
        private Vector4[] m_DisabledTransmissionTintsAndFresnel0;
        private Vector4[] m_WorldScalesAndFilterRadiiAndThicknessRemaps;
        private Vector4[] m_DualLobeAndDiffusePower;
        private Vector4[] m_BorderAttenuationColor;
        private uint[] m_DiffusionProfileHashes;
        
        // Profile tracking
        private int[] m_DiffusionProfileUpdate;
        private DiffusionProfileSettings[] m_SetDiffusionProfiles;
        private int m_ActiveDiffusionProfileCount;
        private uint m_TexturingModeFlags;
        private uint m_TransmissionFlags;

        // Profile references (serialized, included in build)
        private DiffusionProfileSettings[] m_DiffusionProfiles;

        // Global constant buffer (reused to avoid allocations)
        private ShaderVariablesGlobalSubsurface m_ShaderVariablesGlobalSubsurface;

        // Rendering settings
        private LayerMask m_LayerMask;
        private FilteringSettings m_FilteringSettings;
        
        // ProfilingSamplers
        private readonly ProfilingSampler m_SSSSampler = new ProfilingSampler(SSSShaderIDs.SSSPassName);
        private readonly ProfilingSampler m_SSSBufferSampler = new ProfilingSampler(SSSShaderIDs.SSSBufferPassName);
        private readonly ProfilingSampler m_ResolveStencilSampler = new ProfilingSampler(SSSShaderIDs.ResolveStencilPassName);
        private readonly ProfilingSampler m_SSSFilteringSampler = new ProfilingSampler(SSSShaderIDs.SSSFilteringPassName);
        private readonly ProfilingSampler m_CombineLightingSampler = new ProfilingSampler(SSSShaderIDs.SSSCombineLightingPassName);

        // Compute shaders for SSS
        private ComputeShader m_SubsurfaceScatteringCS;
        private ComputeShader m_SubsurfaceScatteringDownsampleCS;
        private ComputeShader m_ResolveStencilCS;
        private int m_SubsurfaceScatteringKernel;
        private int m_PackDiffusionProfileKernel;
        private int m_SubsurfaceScatteringDownsampleKernel;
        private int m_ResolveStencilKernel;

        // Quality settings
        private int m_SampleBudget;
        private int m_DownsampleSteps;
        private bool m_SubsurfaceScatteringAttenuation;
        
        private float m_globalDetailPreservation;

        public int sampleBudget
        {
            get { return m_SampleBudget; }
            set { m_SampleBudget = value; }
        }

        public int downsampleSteps
        {
            get { return m_DownsampleSteps; }
            set { m_DownsampleSteps = value; }
        }

        public bool subsurfaceScatteringAttenuation
        {
            get { return m_SubsurfaceScatteringAttenuation; }
            set { m_SubsurfaceScatteringAttenuation = value; }
        }
        
        public float globalDetailPreservation
        {
            get { return m_globalDetailPreservation; }
            set { m_globalDetailPreservation = value; }
        }

        // Combine Lighting Material
        private Material m_CombineLightingMaterial;

        /// <summary>
        /// Initialize the subsurface scattering system
        /// </summary>
        public void Initialize(
            DiffusionProfileSettings[] diffusionProfiles,
            LayerMask layerMask,
            RenderPassEvent passEvent,
            ComputeShader subsurfaceScatteringCS,
            ComputeShader subsurfaceScatteringDownsampleCS,
            ComputeShader resolveStencilCS,
            Shader combineLightingShader,
            int sampleBudget,
            int downsampleSteps,
            bool subsurfaceScatteringAttenuation,
            float globalDetailPreservation)
        {
            m_DiffusionProfiles = diffusionProfiles ?? new DiffusionProfileSettings[0];
            m_LayerMask = layerMask == 0 ? -1 : layerMask;
            renderPassEvent = passEvent;

            // Store compute shaders and quality settings
            m_SubsurfaceScatteringCS = subsurfaceScatteringCS;
            m_SubsurfaceScatteringDownsampleCS = subsurfaceScatteringDownsampleCS;
            m_ResolveStencilCS = resolveStencilCS;
            m_SampleBudget = sampleBudget;
            m_DownsampleSteps = downsampleSteps;
            m_SubsurfaceScatteringAttenuation = subsurfaceScatteringAttenuation;
            
            m_globalDetailPreservation = globalDetailPreservation;

            // Create combine lighting material (HDRP-style)
            if (combineLightingShader != null)
            {
                m_CombineLightingMaterial = CoreUtils.CreateEngineMaterial(combineLightingShader);
                m_CombineLightingMaterial.SetInt(SSSShaderIDs._StencilRef, SSSShaderIDs.STENCILUSAGE_SUBSURFACE_SCATTERING);
            }

            // Find kernel indices
            if (m_SubsurfaceScatteringCS != null)
            {
                m_SubsurfaceScatteringKernel = m_SubsurfaceScatteringCS.FindKernel("SubsurfaceScattering");
                m_PackDiffusionProfileKernel = m_SubsurfaceScatteringCS.FindKernel("PackDiffusionProfile");
            }
            
            if (m_SubsurfaceScatteringDownsampleCS != null)
            {
                m_SubsurfaceScatteringDownsampleKernel = m_SubsurfaceScatteringDownsampleCS.FindKernel("Downsample");
            }
            
            if (m_ResolveStencilCS != null)
            {
                m_ResolveStencilKernel = m_ResolveStencilCS.FindKernel("Main");
            }

            // Initialize data arrays
            m_ShapeParamsAndMaxScatterDists = new Vector4[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT];
            m_TransmissionTintsAndFresnel0 = new Vector4[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT];
            m_DisabledTransmissionTintsAndFresnel0 = new Vector4[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT];
            m_WorldScalesAndFilterRadiiAndThicknessRemaps = new Vector4[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT];
            m_DualLobeAndDiffusePower = new Vector4[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT];
            m_BorderAttenuationColor = new Vector4[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT];
            m_DiffusionProfileHashes = new uint[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT];

            // Initialize tracking arrays
            m_DiffusionProfileUpdate = new int[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT];
            m_SetDiffusionProfiles = new DiffusionProfileSettings[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT];

            // Setup rendering
            RenderQueueRange renderQueueRange = RenderQueueRange.opaque;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, m_LayerMask);
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Cleanup()
        {
            CoreUtils.Destroy(m_CombineLightingMaterial);
            m_CombineLightingMaterial = null;
            
            DisposeLegacy();
        }

        /// <summary>
        /// Update diffusion profile settings from profile array
        /// </summary>
        public void UpdateDiffusionProfileSettings()
        {
            int profileCount = 0;

            // Add all profiles from array
            if (m_DiffusionProfiles != null)
            {
                for (int i = 0; i < m_DiffusionProfiles.Length && profileCount < DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT; i++)
                {
                    if (m_DiffusionProfiles[i] != null)
                    {
                        SetDiffusionProfileAtIndex(m_DiffusionProfiles[i], profileCount);
                        profileCount++;
                    }
                }
            }

            m_ActiveDiffusionProfileCount = profileCount;

            // Update shader variables structure (HDRP-style)
            UpdateShaderVariablesGlobalSubsurface();
            
            // Push to GPU
            PushGlobalParams();
        }

        /// <summary>
        /// Set a diffusion profile at a specific index (HDRP-style)
        /// </summary>
        void SetDiffusionProfileAtIndex(DiffusionProfileSettings settings, int index)
        {
            // if the diffusion profile was already set and it haven't changed then there is nothing to upgrade
            if (m_SetDiffusionProfiles[index] == settings && m_DiffusionProfileUpdate[index] == settings.updateCount)
            {
                return;
            }

            // if the settings have not yet been initialized
            if (settings.profile.hash == 0)
            {
                return;
            }

            m_ShapeParamsAndMaxScatterDists[index] = settings.shapeParamAndMaxScatterDist;
            m_TransmissionTintsAndFresnel0[index] = settings.transmissionTintAndFresnel0;
            m_DisabledTransmissionTintsAndFresnel0[index] = settings.disabledTransmissionTintAndFresnel0;
            m_WorldScalesAndFilterRadiiAndThicknessRemaps[index] = settings.worldScaleAndFilterRadiusAndThicknessRemap;
            m_DualLobeAndDiffusePower[index] = settings.dualLobeAndDiffusePower;
            m_BorderAttenuationColor[index] = settings.borderAttenuationColorMultiplier;
            m_DiffusionProfileHashes[index] = settings.profile.hash;

            // Erase previous value (This need to be done here individually as in the SSS editor we edit individual component)
            uint mask = 1u << index;
            m_TexturingModeFlags &= ~mask;
            m_TransmissionFlags &= ~mask;

            m_TexturingModeFlags |= (uint)settings.profile.texturingMode << index;
            m_TransmissionFlags |= (uint)settings.profile.transmissionMode << index;

            m_SetDiffusionProfiles[index] = settings;
            m_DiffusionProfileUpdate[index] = settings.updateCount;
        }

        /// <summary>
        /// Update shader variables global subsurface structure (HDRP-style)
        /// Only updates the data, doesn't push to GPU
        /// </summary>
        unsafe void UpdateShaderVariablesGlobalSubsurface()
        {
            // Set control variables (order matches HDRP)
            m_ShaderVariablesGlobalSubsurface._EnableSubsurfaceScattering = 1;
            m_ShaderVariablesGlobalSubsurface._TexturingModeFlags = m_TexturingModeFlags;
            m_ShaderVariablesGlobalSubsurface._TransmissionFlags = m_TransmissionFlags;
            m_ShaderVariablesGlobalSubsurface._DiffusionProfileCount = (uint)m_ActiveDiffusionProfileCount;

            // Copy Vector4 arrays to fixed float arrays (each Vector4 = 4 floats)
            for (int i = 0; i < m_ActiveDiffusionProfileCount; ++i)
            {
                // Vector4 components
                for (int c = 0; c < 4; ++c)
                {
                    m_ShaderVariablesGlobalSubsurface._ShapeParamsAndMaxScatterDists[i * 4 + c] = m_ShapeParamsAndMaxScatterDists[i][c];
                    m_ShaderVariablesGlobalSubsurface._TransmissionTintsAndFresnel0[i * 4 + c] = m_TransmissionTintsAndFresnel0[i][c];
                    m_ShaderVariablesGlobalSubsurface._WorldScalesAndFilterRadiiAndThicknessRemaps[i * 4 + c] = m_WorldScalesAndFilterRadiiAndThicknessRemaps[i][c];
                    m_ShaderVariablesGlobalSubsurface._DualLobeAndDiffusePower[i * 4 + c] = m_DualLobeAndDiffusePower[i][c];
                    m_ShaderVariablesGlobalSubsurface._BorderAttenuationColor[i * 4 + c] = m_BorderAttenuationColor[i][c];
                }

                // Hash table - only use first component of uint4 (HDRP approach)
                m_ShaderVariablesGlobalSubsurface._DiffusionProfileHashTable[i * 4] = m_DiffusionProfileHashes[i];
            }
        }

        /// <summary>
        /// Push global parameters to GPU using ConstantBuffer (HDRP-style)
        /// Call UpdateShaderVariablesGlobalSubsurface() before this
        /// </summary>
        void PushGlobalParams()
        {
            // Use ConstantBuffer.PushGlobal for efficient data transfer (HDRP-style)
            ConstantBuffer.PushGlobal(in m_ShaderVariablesGlobalSubsurface, SSSShaderIDs.ShaderVariablesGlobalSubsurface);
        }
    }
}
