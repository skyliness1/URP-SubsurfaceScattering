// Ported from HDRP SubsurfaceScattering.hlsl
// Adapted for URP environment

#ifndef SUBSURFACE_SCATTERING_INCLUDED
#define SUBSURFACE_SCATTERING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/ShaderVariablesGlobalSubsurface.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/DiffusionProfile.hlsl"

// Note: HasFlag is already defined in Common.hlsl (line 1510)
// No need to redefine it here

// ----------------------------------------------------------------------------
// helper functions
// ----------------------------------------------------------------------------

// 0: [ albedo = albedo ]
// 1: [ albedo = 1 ]
// 2: [ albedo = sqrt(albedo) ]
uint GetSubsurfaceScatteringTexturingMode(int diffusionProfile)
{
    uint texturingMode = 0;

    // For URP, we always check the global flag
    bool enableSss = _EnableSubsurfaceScattering != 0;

    if (enableSss)
    {
        // Check if this profile uses post-scatter texturing mode
        bool performPostScatterTexturing = ((_TexturingModeFlags >> diffusionProfile) & 1u) != 0;

        if (performPostScatterTexturing)
        {
            // Post-scatter texturing mode: the albedo is only applied during the SSS pass.
            // In forward pass, we output albedo = 1
            #if defined(SHADERPASS) && (SHADERPASS != SHADERPASS_SUBSURFACE_SCATTERING)
                texturingMode = 1;
            #endif
        }
        else
        {
            // Pre- and post- scatter texturing mode.
            texturingMode = 2;
        }
    }

    return texturingMode;
}

// Returns the modified albedo (diffuse color) for materials with subsurface scattering.
// See GetSubsurfaceScatteringTexturingMode() above for more details.
// Ref: Advanced Techniques for Realistic Real-Time Skin Rendering.
float3 ApplySubsurfaceScatteringTexturingMode(uint texturingMode, float3 color)
{
    switch (texturingMode)
    {
        case 2:  color = sqrt(color); break;
        case 1:  color = 1;           break;
        default: color = color;       break;
    }

    return color;
}

// ----------------------------------------------------------------------------
// Encoding/decoding SSS buffer functions
// ----------------------------------------------------------------------------

struct SSSData
{
    float3 diffuseColor;
    float  subsurfaceMask;
    uint   diffusionProfileIndex;
};

#define SSSBufferType0 float4 // Must match GBufferType0 in deferred
#define SSSBufferType float4  // Alias for easier usage

// SSSBuffer texture declaration (URP uses TEXTURE2D instead of TEXTURE2D_X)
TEXTURE2D(_SSSBufferTexture);
SAMPLER(sampler_SSSBufferTexture);

// Note: The SSS buffer used here is sRGB
void EncodeIntoSSSBuffer(SSSData sssData, uint2 positionSS, out SSSBufferType0 outSSSBuffer0)
{
    outSSSBuffer0 = float4(sssData.diffuseColor, PackFloatInt8bit(sssData.subsurfaceMask, sssData.diffusionProfileIndex, 16));
}

// Note: The SSS buffer used here is sRGB
void DecodeFromSSSBuffer(float4 sssBuffer, uint2 positionSS, out SSSData sssData)
{
    sssData.diffuseColor = sssBuffer.rgb;
    UnpackFloatInt8bit(sssBuffer.a, 16, sssData.subsurfaceMask, sssData.diffusionProfileIndex);
}

void DecodeFromSSSBuffer(uint2 positionSS, out SSSData sssData)
{
    // URP: Use LOAD_TEXTURE2D instead of LOAD_TEXTURE2D_X
    float4 sssBuffer = LOAD_TEXTURE2D(_SSSBufferTexture, positionSS);
    DecodeFromSSSBuffer(sssBuffer, positionSS, sssData);
}

#define OUTPUT_SSSBUFFER(NAME) out SSSBufferType0 MERGE_NAME(NAME, 0)

// URP version: simplified macro that takes SSSData directly
#define ENCODE_INTO_SSSBUFFER(SURFACE_DATA, UNPOSITIONSS, SSS_DATA, NAME) \
    EncodeIntoSSSBuffer(SSS_DATA, UNPOSITIONSS, NAME)

#define DECODE_FROM_SSSBUFFER(UNPOSITIONSS, SSS_DATA) \
    DecodeFromSSSBuffer(UNPOSITIONSS, SSS_DATA)

// In order to support subsurface scattering, we need to know which pixels have an SSS material.
// It can be accomplished by reading the stencil buffer.
// A faster solution (which avoids an extra texture fetch) is to simply make sure that
// all pixels which belong to an SSS material are not black (those that don't always are).
// We choose the blue color channel since it's perceptually the least noticeable.
float3 TagLightingForSSS(float3 subsurfaceLighting)
{
    subsurfaceLighting.b = max(subsurfaceLighting.b, HALF_MIN);
    return subsurfaceLighting;
}

// See TagLightingForSSS() for details.
bool TestLightingForSSS(float3 subsurfaceLighting)
{
    return subsurfaceLighting.b > 0;
}

// ----------------------------------------------------------------------------
// Helper functions to use SSS/Transmission with a material
// ----------------------------------------------------------------------------

// Following function allow to easily setup SSS and transmission inside a material.
// User can request either SSS functions, or Transmission functions, or both, by defining MATERIAL_INCLUDE_SUBSURFACESCATTERING and/or MATERIAL_INCLUDE_TRANSMISSION
// before including this file.
// + It require that the material follow naming convention for properties inside BSDFData

// struct BSDFData
// {
//     (...)
//     // Share for SSS and Transmission
//     uint materialFeatures;
//     uint diffusionProfile;
//     // For SSS
//     float3 diffuseColor;
//     float3 fresnel0;
//     float subsurfaceMask;
//     // For transmission
//     float thickness;
//     bool useThickObjectMode;
//     float3 transmittance;
//     perceptualRoughness; // Only if user chose to support DisneyDiffuse
//     (...)
// }

// Note: Transmission functions for light evaluation are also included in LightEvaluation.hlsl file based on the MATERIAL_INCLUDE_TRANSMISSION
#define MATERIALFEATUREFLAGS_SSS_TRANSMISSION_START (1 << 16) // It should be safe to start these flags

#define MATERIALFEATUREFLAGS_SSS_OUTPUT_SPLIT_LIGHTING         ((MATERIALFEATUREFLAGS_SSS_TRANSMISSION_START) << 0)
#define MATERIALFEATUREFLAGS_SSS_TEXTURING_MODE_OFFSET FastLog2((MATERIALFEATUREFLAGS_SSS_TRANSMISSION_START) << 1) // Note: The texture mode is 2bit, thus go from '<< 1' to '<< 3'
// Flags used as a shortcut to know if we have thick mode transmission
// It is important to keep this flag pointing at the inverse of the current diffusion profile thickness mode, i.e. the
// current diffusion profile thickness mode is thin because we don't want to sample shadows for the default profile
// so this define is set to thick mode. It is important to keep it as is because when we initialize the BSDF datas
// we assume that all neutral values including the thickness mode are 0 (so by default when we shade a material that
// doesn't have transmission on a tile with the material feature transmission enabled, we don't evaluate the diffusion
// profile because the thick flag is not set (for pixels that have transmission, we force the flags in a per-pixel
// material feature)).
#define MATERIALFEATUREFLAGS_TRANSMISSION_MODE_THICK_OBJECT     ((MATERIALFEATUREFLAGS_SSS_TRANSMISSION_START) << 3)
#define MATERIALFEATUREFLAGS_SSS_DUAL_LOBE                      ((MATERIALFEATUREFLAGS_SSS_TRANSMISSION_START) << 4)
#define MATERIALFEATUREFLAGS_SSS_DIFFUSE_POWER                  ((MATERIALFEATUREFLAGS_SSS_TRANSMISSION_START) << 5)

// 15 degrees
#define TRANSMISSION_WRAP_ANGLE (PI/12)
#define TRANSMISSION_WRAP_LIGHT cos(PI/2 - TRANSMISSION_WRAP_ANGLE)

bool GetDualLobeParameters(uint diffusionProfileIndex, out float multiplierA, out float multiplierB, out float lobeMix)
{
    multiplierA = _DualLobeAndDiffusePower[diffusionProfileIndex].r;
    multiplierB = _DualLobeAndDiffusePower[diffusionProfileIndex].g;
    lobeMix     = _DualLobeAndDiffusePower[diffusionProfileIndex].b;
    return multiplierA != multiplierB; // if both multipliers are equal, there is no dual lobe
}

float GetDiffusePower(uint diffusionProfileIndex)
{
    return _DualLobeAndDiffusePower[diffusionProfileIndex].a;
}

#ifdef MATERIAL_INCLUDE_SUBSURFACESCATTERING

void FillMaterialSSS(uint diffusionProfileIndex, float subsurfaceMask, inout BSDFData bsdfData)
{
    bsdfData.diffusionProfileIndex = diffusionProfileIndex;
    bsdfData.fresnel0 = _TransmissionTintsAndFresnel0[diffusionProfileIndex].a;
    bsdfData.subsurfaceMask = subsurfaceMask;
    if (subsurfaceMask != 0)
        bsdfData.materialFeatures |= MATERIALFEATUREFLAGS_SSS_OUTPUT_SPLIT_LIGHTING;
    bsdfData.materialFeatures |= GetSubsurfaceScatteringTexturingMode(diffusionProfileIndex) << MATERIALFEATUREFLAGS_SSS_TEXTURING_MODE_OFFSET;
}

bool ShouldOutputSplitLighting(BSDFData bsdfData)
{
    return HasFlag(bsdfData.materialFeatures, MATERIALFEATUREFLAGS_SSS_OUTPUT_SPLIT_LIGHTING);
}

float3 GetModifiedDiffuseColorForSSS(BSDFData bsdfData)
{
    // Subsurface scattering mode
    if (bsdfData.subsurfaceMask != 0)
    {
        uint   texturingMode = (bsdfData.materialFeatures >> MATERIALFEATUREFLAGS_SSS_TEXTURING_MODE_OFFSET) & 3;
        return ApplySubsurfaceScatteringTexturingMode(texturingMode, bsdfData.diffuseColor);
    }
    else
    {
        return bsdfData.diffuseColor;
    }
}

#endif

#ifdef MATERIAL_INCLUDE_TRANSMISSION

// Assume that bsdfData.diffusionProfileIndex is init
void FillMaterialTransmission(uint diffusionProfileIndex, float thickness, float3 transmissionMask, inout BSDFData bsdfData)
{
    float2 remap = _WorldScalesAndFilterRadiiAndThicknessRemaps[diffusionProfileIndex].zw;

    bsdfData.diffusionProfileIndex = diffusionProfileIndex;
    bsdfData.fresnel0              = _TransmissionTintsAndFresnel0[diffusionProfileIndex].a;
    bsdfData.thickness             = remap.x + remap.y * thickness;

    // The difference between the thin and the regular (a.k.a. auto-thickness) modes is the following:
    // * in the thin object mode, we assume that the geometry is thin enough for us to safely share
    // the shadowing information between the front and the back faces;
    // * the thin mode uses baked (textured) thickness for all transmission calculations;
    // * the thin mode uses wrapped diffuse lighting for the NdotL;
    // * the auto-thickness mode uses the baked (textured) thickness to compute transmission from
    // indirect lighting and non-shadow-casting lights; for shadowed lights, it calculates
    // the thickness using the distance to the closest occluder sampled from the shadow map.
    // If the distance is large, it may indicate that the closest occluder is not the back face of
    // the current object. That's not a problem, since large thickness will result in low intensity.
    bool useThickObjectMode = ((_TransmissionFlags >> diffusionProfileIndex) & 1u) == 0;

    bsdfData.materialFeatures |= useThickObjectMode ? MATERIALFEATUREFLAGS_TRANSMISSION_MODE_THICK_OBJECT : 0;

    // Compute transmittance using baked thickness here. It may be overridden for direct lighting
    // in the auto-thickness mode (but is always used for indirect lighting).
    bsdfData.transmittance = ComputeTransmittanceDisney(_ShapeParamsAndMaxScatterDists[diffusionProfileIndex].rgb,
                                                        _TransmissionTintsAndFresnel0[diffusionProfileIndex].rgb,
                                                        bsdfData.thickness) * transmissionMask;
}

void FillMaterialTransmission(uint diffusionProfileIndex, float thickness, inout BSDFData bsdfData)
{
    FillMaterialTransmission(diffusionProfileIndex, thickness, 1.0f, bsdfData);
}

#endif

// ----------------------------------------------------------------------------
// Diffusion Profile Lookup Functions
// ----------------------------------------------------------------------------

// Find diffusion profile index by hash (uint version)
uint FindDiffusionProfileIndex(uint diffusionProfileHash)
{
    if (diffusionProfileHash == 0)
        return 0;

    uint diffusionProfileIndex = 0;
    
    // Fetch the 4 bit index number by looking for the diffusion profile unique ID
    for (uint i = 0; i < _DiffusionProfileCount && i < 16; i++)
    {
        // Only use .x component of uint4 (HDRP approach)
        if (_DiffusionProfileHashTable[i].x == diffusionProfileHash)
        {
            diffusionProfileIndex = i;
            break;
        }
    }

    return diffusionProfileIndex;
}

// Find diffusion profile index by hash (float version - for material properties)
uint GetDiffusionProfileIndex(float diffusionProfileHash)
{
    return FindDiffusionProfileIndex(asuint(diffusionProfileHash));
}

// ----------------------------------------------------------------------------
// Diffusion Profile Data Accessors
// ----------------------------------------------------------------------------

// Get shape parameter by index
float3 GetShapeParam(uint index)
{
    return _ShapeParamsAndMaxScatterDists[index].rgb;
}

// Get shape parameter by hash
float3 GetShapeParamByHash(float hash)
{
    uint index = GetDiffusionProfileIndex(hash);
    return GetShapeParam(index);
}

// Get max scatter distance by index
float GetMaxScatterDist(uint index)
{
    return _ShapeParamsAndMaxScatterDists[index].a;
}

// Get max scatter distance by hash
float GetMaxScatterDistByHash(float hash)
{
    uint index = GetDiffusionProfileIndex(hash);
    return GetMaxScatterDist(index);
}

// Get transmission tint by index
float3 GetTransmissionTint(uint index)
{
    return _TransmissionTintsAndFresnel0[index].rgb;
}

// Get transmission tint by hash
float3 GetTransmissionTintByHash(float hash)
{
    uint index = GetDiffusionProfileIndex(hash);
    return GetTransmissionTint(index);
}

// Get world scale by index
float GetWorldScale(uint index)
{
    return _WorldScalesAndFilterRadiiAndThicknessRemaps[index].x;
}

// Get world scale by hash
float GetWorldScaleByHash(float hash)
{
    uint index = GetDiffusionProfileIndex(hash);
    return GetWorldScale(index);
}

// Get filter radius by index
float GetFilterRadius(uint index)
{
    return _WorldScalesAndFilterRadiiAndThicknessRemaps[index].y;
}

// Get filter radius by hash
float GetFilterRadiusByHash(float hash)
{
    uint index = GetDiffusionProfileIndex(hash);
    return GetFilterRadius(index);
}

// Get thickness remap by index
float2 GetThicknessRemap(uint index)
{
    float4 data = _WorldScalesAndFilterRadiiAndThicknessRemaps[index];
    return float2(data.z, data.w); // min, range
}

// Get thickness remap by hash
float2 GetThicknessRemapByHash(float hash)
{
    uint index = GetDiffusionProfileIndex(hash);
    return GetThicknessRemap(index);
}

// ----------------------------------------------------------------------------
// URP-specific helper functions (not in HDRP)
// ----------------------------------------------------------------------------

// Check if subsurface scattering should be applied (simplified version for non-BSDFData materials)
bool ShouldOutputSplitLighting(float subsurfaceMask)
{
    return subsurfaceMask > 0.0 && _EnableSubsurfaceScattering != 0;
}

// Get texturing mode for a diffusion profile
uint GetSubsurfaceScatteringTexturingModeByIndex(uint diffusionProfileIndex)
{
    // Extract bit flag from _TexturingModeFlags
    return (_TexturingModeFlags >> diffusionProfileIndex) & 1u;
}

// Get modified diffuse color for SSS rendering (simplified version for non-BSDFData materials)
float3 GetModifiedDiffuseColorForSSS(float3 diffuseColor, float subsurfaceMask, uint diffusionProfileIndex)
{
    if (subsurfaceMask > 0.0 && _EnableSubsurfaceScattering != 0)
    {
        uint texturingMode = GetSubsurfaceScatteringTexturingMode(diffusionProfileIndex);
        return ApplySubsurfaceScatteringTexturingMode(texturingMode, diffuseColor);
    }
    else
    {
        return diffuseColor;
    }
}

#endif // SUBSURFACE_SCATTERING_INCLUDED

