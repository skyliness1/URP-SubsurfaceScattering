using UnityEngine;
using UnityEngine.Rendering;

namespace SoulRender
{
    /// <summary>
    /// Shader property IDs and names for Subsurface Scattering
    /// </summary>
    public static class SSSShaderIDs
    {
        // ===== Global Constant Buffer =====
        public static readonly int ShaderVariablesGlobalSubsurface = Shader.PropertyToID("ShaderVariablesGlobalSubsurface");

        // ===== Render Targets (MRT outputs) =====
        public const string SSSSpecularLightingName = "_SSSSpecularLighting";
        public const string SSSDiffuseLightingName = "_SSSDiffuseLighting";
        public const string SSSBufferTextureName = "_SSSBufferTexture";
        public static readonly int SSSSpecularLighting = Shader.PropertyToID(SSSSpecularLightingName);
        public static readonly int SSSDiffuseLighting = Shader.PropertyToID(SSSDiffuseLightingName);
        public static readonly int SSSBufferTexture = Shader.PropertyToID(SSSBufferTextureName);
        
        // ===== Legacy cached identifiers (k_ prefix for constants) =====
        public static readonly int k_SSSBufferTextureID = Shader.PropertyToID(SSSBufferTextureName);
        public static readonly int k_SSSDiffuseLightingID = Shader.PropertyToID(SSSDiffuseLightingName);
        public static readonly int k_SSSFilteringTextureID = Shader.PropertyToID(SSSFilteringTextureName);

        // ===== Material Properties (used in SSSLit shader) =====
        // Profile reference
        public const string DiffusionProfileAsset = "_DiffusionProfileAsset";
        public const string DiffusionProfileHash = "_DiffusionProfileHash";
        
        // SSS parameters
        public const string SubsurfaceMask = "_SubsurfaceMask";
        public const string SubsurfaceMaskMap = "_SubsurfaceMaskMap";
        
        // Transmission parameters
        public const string Thickness = "_Thickness";
        public const string ThicknessMap = "_ThicknessMap";
        public const string ThicknessRange = "_ThicknessRange";
        public const string ThicknessPower = "_ThicknessPower";
        public const string TransmissionMask = "_TransmissionMask";
        public const string TransmissionMaskMap = "_TransmissionMaskMap";

        // ===== Shader Pass Tags =====
        public static readonly ShaderTagId SSSBufferPass = new ShaderTagId("SSSBuffer");

        // ===== Pass Names (for profiling and debugging) =====
        public const string SSSPassName = "Subsurface Scattering";
        public const string SSSBufferPassName = "SSS Buffer";
        public const string SSSFilteringPassName = "SSS Filtering";
        public const string SSSCombineLightingPassName = "SSS Combine Lighting";
        public const string ResolveStencilPassName = "BuildCoarseStencilAndResolveIfNeeded";

        // ===== Compute Shader Parameters =====
        public static readonly int _SssSampleBudget = Shader.PropertyToID("_SssSampleBudget");
        public static readonly int _SssDownsampleSteps = Shader.PropertyToID("_SssDownsampleSteps");
        public static readonly int _FrameCount = Shader.PropertyToID("_FrameCount");
        
        public static readonly int _GlobalDetailPreservation = Shader.PropertyToID("_GlobalDetailPreservation");
        
        // Input textures for SSS compute
        public static readonly int _IrradianceSource = Shader.PropertyToID("_IrradianceSource");
        public static readonly int _IrradianceSourceDownsampled = Shader.PropertyToID("_IrradianceSourceDownsampled");
        
        // Output textures
        public static readonly int _CameraFilteringTexture = Shader.PropertyToID("_CameraFilteringTexture");
        
        // Downsample compute shader
        public static readonly int _SourceTexture = Shader.PropertyToID("_SourceTexture");
        public static readonly int _OutputTexture = Shader.PropertyToID("_OutputTexture");
        
        // Diffusion profile index texture for occlusion
        public static readonly int _DiffusionProfileIndexTexture = Shader.PropertyToID("_DiffusionProfileIndexTexture");
        
        // Coarse stencil buffer for SSS optimization
        public static readonly int _CoarseStencilBuffer = Shader.PropertyToID("_CoarseStencilBuffer");
        public static readonly int _CoarseStencilBufferSize = Shader.PropertyToID("_CoarseStencilBufferSize");
        
        // Stencil resolve compute shader
        public static readonly int _DepthTexture = Shader.PropertyToID("_DepthTexture");
        public static readonly int _StencilTexture = Shader.PropertyToID("_StencilTexture");
        public static readonly int _OutputStencilBuffer = Shader.PropertyToID("_OutputStencilBuffer");
        public static readonly int _OutputDepthBuffer = Shader.PropertyToID("_OutputDepthBuffer");

        // ===== Texture Names (for RenderGraph creation) =====
        public const string SSSDownsampledTextureName = "_SSSDownsampled";
        public const string SSSFilteringTextureName = "_SSSFiltering";
        public const string SSSProfileIndexTextureName = "_SSSProfileIndex";
        public const string CoarseStencilBufferName = "CoarseStencilBuffer";
        
        // ===== Combine Lighting Shader Properties =====
        public static readonly int _StencilRef = Shader.PropertyToID("_StencilRef");
        
        // ===== Separable Filter =====
        public static readonly int _SSSKernel = Shader.PropertyToID("_SSSKernel");
        public static readonly int _SSSKernelSize = Shader.PropertyToID("_SSSKernelSize");
        public static readonly int _SeparableInput = Shader.PropertyToID("_SeparableInput");
        public const string SSSSeparableIntermediateName = "_SSSSeparableIntermediate";
        public const string SSSFilteringSeparablePassName = "SSS Filtering Separable";

        // ===== Stencil Values =====
        public const int STENCILUSAGE_SUBSURFACE_SCATTERING = 4;
    }
}

