#ifndef SHADER_VARIABLES_GLOBAL_SUBSURFACE_HLSL
#define SHADER_VARIABLES_GLOBAL_SUBSURFACE_HLSL

// ----------------------------------------------------------------------------
// Subsurface Scattering Global Variables
// (Based on HDRP implementation)
// ----------------------------------------------------------------------------

// Structure containing all subsurface scattering data
// Field order and names match HDRP's ShaderVariablesGlobal exactly
struct ShaderVariablesGlobalSubsurface
{
    // Shape parameters and maximum scatter distance for each profile (RGB = S = 1/D, A = max scatter dist)
    float4 _ShapeParamsAndMaxScatterDists[16];
    
    // Transmission tint and Fresnel0 for each profile (RGB = tint, A = fresnel0)
    float4 _TransmissionTintsAndFresnel0[16];
    
    // World scale, filter radius and thickness remap (X = world scale, Y = filter radius, Z = thickness min, W = thickness range)
    float4 _WorldScalesAndFilterRadiiAndThicknessRemaps[16];
    
    // Dual lobe and diffuse power (X = smoothness A, Y = smoothness B, Z = lobe mix, W = diffuse power - 1)
    float4 _DualLobeAndDiffusePower[16];
    
    // Border attenuation color (RGB = color, A = unused)
    float4 _BorderAttenuationColor[16];
    
    // Diffusion profile hash table (for runtime lookup) - stored as uint4 but only .x is used
    uint4 _DiffusionProfileHashTable[16];
    
    // SSS control flags (order matches HDRP)
    uint _EnableSubsurfaceScattering;
    uint _TexturingModeFlags;      // 1 bit per profile: 0 = PreAndPostScatter, 1 = PostScatter
    uint _TransmissionFlags;       // 1 bit per profile: 0 = Regular, 1 = ThinObject
    uint _DiffusionProfileCount;
};

CBUFFER_START(ShaderVariablesGlobalSubsurface)
    float4 _ShapeParamsAndMaxScatterDists[16];
    float4 _TransmissionTintsAndFresnel0[16];
    float4 _WorldScalesAndFilterRadiiAndThicknessRemaps[16];
    float4 _DualLobeAndDiffusePower[16];
    float4 _BorderAttenuationColor[16];
    uint4 _DiffusionProfileHashTable[16];
    uint _EnableSubsurfaceScattering;
    uint _TexturingModeFlags;
    uint _TransmissionFlags;
    uint _DiffusionProfileCount;
CBUFFER_END

#endif // SHADER_VARIABLES_GLOBAL_SUBSURFACE_HLSL

