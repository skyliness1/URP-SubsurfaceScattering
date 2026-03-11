#ifndef SSS_BSSRDF_INCLUDED
#define SSS_BSSRDF_INCLUDED

//=============================================================================
// SSS BRDF Helper Functions - Following UE5 approach
//=============================================================================

// BxDF Context for SSS calculations - Following UE5 BxDFContext
struct SSSBxDFContext
{
    half NoV;
    half NoL;
    half NoH;
    half VoH;
};

void InitSSSBxDFContext(inout SSSBxDFContext Context, half3 N, half3 V, half3 L)
{
    Context.NoL = dot(N, L);
    Context.NoV = dot(N, V);
    half VoL = dot(V, L);
    half InvLenH = rsqrt(2.0 + 2.0 * VoL);
    Context.NoH = saturate((Context.NoL + Context.NoV) * InvLenH);
    Context.VoH = saturate(InvLenH + InvLenH * VoL);
}

// Standard Lambert Diffuse
half3 Diffuse_Lambert_SSS(half3 DiffuseColor)
{
    return DiffuseColor * (1.0 / PI);
}

//=============================================================================
// Dual Specular GGX Helper Functions - Following UE5 ShadingModels. ush
//=============================================================================

// D_GGX - Following UE5 BRDF. ush
half D_GGX_SSS(half a2, half NoH)
{
    half d = (NoH * a2 - NoH) * NoH + 1.0;
    return a2 / (PI * d * d);
}

// Vis_SmithJointApprox - Following UE5 BRDF.ush
half Vis_SmithJointApprox_SSS(half a2, half NoV, half NoL)
{
    half a = sqrt(a2);
    half oneMinusA = 1.0 - a;
    half Vis_SmithV = NoL * (NoV * oneMinusA + a);
    half Vis_SmithL = NoV * (NoL * oneMinusA + a);
    return 0.5 * rcp(Vis_SmithV + Vis_SmithL);
}

// F_Schlick - Following UE5 BRDF.ush
half3 F_Schlick_SSS(half3 SpecularColor, half VoH)
{
    half Fc = Pow5(1.0 - VoH);
    // Anything less than 2% is physically impossible and is instead considered to be shadowing
    return saturate(50.0 * SpecularColor.g) * Fc + (1.0 - Fc) * SpecularColor;
}

//=============================================================================
// Energy Conservation - Following UE5 ShadingEnergyConservation. ush
// Reference:  Karis 2013, "Real Shading in Unreal Engine 4"
//=============================================================================

// Unified energy terms computation: outputs both preservation and conservation
// in a single pass, avoiding duplicate EnvBRDFApprox calculations
void ComputeEnergyTerms_SSS(half3 SpecularColor, half Roughness, half NoV,
    out half3 energyPreservation, out half3 energyConservation)
{
    const half4 c0 = half4(-1.0, -0.0275, -0.572, 0.022);
    const half4 c1 = half4(1.0, 0.0425, 1.04, -0.04);
    half4 r = Roughness * c0 + c1;
    half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    half2 AB = half2(-1.04, 1.04) * a004 + r.zw;

    half F90 = saturate(50.0 * SpecularColor.g);
    half3 specReflectance = SpecularColor * AB.x + F90 * AB.y;

    energyPreservation = 1.0 - specReflectance;
    energyConservation = min(1.0 + SpecularColor * (rcp(max(specReflectance, 0.001)) - 1.0), 2.0);
}

// Lightweight version: only outputs energyPreservation (for paths needing only diffuse compensation)
half3 ComputeEnergyPreservation_SSS(half3 SpecularColor, half Roughness, half NoV)
{
    const half4 c0 = half4(-1.0, -0.0275, -0.572, 0.022);
    const half4 c1 = half4(1.0, 0.0425, 1.04, -0.04);
    half4 r = Roughness * c0 + c1;
    half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    half2 AB = half2(-1.04, 1.04) * a004 + r.zw;

    half F90 = saturate(50.0 * SpecularColor.g);
    half3 specReflectance = SpecularColor * AB.x + F90 * AB.y;
    return 1.0 - specReflectance;
}

//=============================================================================
// Get dual specular lobe parameters from material properties
// Parameters exposed in material panel instead of LUT sampling
//=============================================================================
void GetDualSpecularLobeParameters(half Roughness, half SubsurfaceMask,
    half Lobe0RoughnessMult, half Lobe1RoughnessMult, half LobeMixParam,
    out half Lobe0Roughness, out half Lobe1Roughness, out half LobeMix)
{
    half BlendFactor = saturate((SubsurfaceMask - 0.01) * 10.0);

    Lobe0Roughness = max(saturate(Roughness * lerp(1.0, Lobe0RoughnessMult, BlendFactor)), 0.02);
    Lobe1Roughness = saturate(Roughness * lerp(1.0, Lobe1RoughnessMult, BlendFactor));
    LobeMix = LobeMixParam * BlendFactor;
}

//=============================================================================
// Dual Specular GGX - Following UE5 DualSpecularGGX with Energy Conservation
//=============================================================================
half3 DualSpecularGGX_SSS(half3 SpecularColor, SSSBxDFContext Context, half NoL,
    half Lobe0Roughness, half Lobe1Roughness, half LobeMix, out half AverageRoughness)
{
    AverageRoughness = lerp(Lobe0Roughness, Lobe1Roughness, LobeMix);
    half AverageAlpha2 = Pow4(AverageRoughness);
    half Lobe0Alpha2 = Pow4(Lobe0Roughness);
    half Lobe1Alpha2 = Pow4(Lobe1Roughness);
    
    // Dual lobe NDF - lerp between two D_GGX with different roughnesses
    half D0 = D_GGX_SSS(Lobe0Alpha2, Context.NoH);
    half D1 = D_GGX_SSS(Lobe1Alpha2, Context.NoH);
    half D = lerp(D0, D1, LobeMix);
    
    // Average visibility well approximates using two separate ones (one per lobe)
    // Following UE5 comment in DualSpecularGGX
    half Vis = Vis_SmithJointApprox_SSS(AverageAlpha2, Context.NoV, NoL);
    
    // Fresnel
    half3 F = F_Schlick_SSS(SpecularColor, Context.VoH);
    
    return (D * Vis) * F;
}

// Single lobe specular for comparison
half3 SingleSpecularGGX_SSS(half3 SpecularColor, SSSBxDFContext Context, half NoL, half Roughness)
{
    half a2 = Pow4(Roughness);
    half D = D_GGX_SSS(a2, Context.NoH);
    half Vis = Vis_SmithJointApprox_SSS(a2, Context.NoV, NoL);
    half3 F = F_Schlick_SSS(SpecularColor, Context.VoH);
    return (D * Vis) * F;
}

// Custom InitializeBRDFData for SSS that overrides fresnel0 from diffusion profile
// Following HDRP approach: use IOR-based fresnel0 from diffusion profile instead of material specular color
// Ref: HDRP FillMaterialSSS() in SubsurfaceScattering.hlsl
inline void InitializeBRDFDataSSS(inout SurfaceData surfaceData, uint diffusionProfileIndex, float subsurfaceMask, out BRDFData brdfData)
{
    // First, initialize BRDF data using standard URP method
    InitializeBRDFData(surfaceData, brdfData);
    
    // Then override specular (f0) with fresnel0 from diffusion profile (following HDRP approach)
    // This ensures all BRDF calculations use the correct f0 value derived from the profile's IOR
    float fresnel0 = _TransmissionTintsAndFresnel0[diffusionProfileIndex].a;
    brdfData.specular = fresnel0;
    
    // Recalculate reflectivity and grazingTerm based on new specular value
    brdfData.reflectivity = fresnel0;
    brdfData.grazingTerm = saturate(surfaceData.smoothness + fresnel0);
    
    // Apply SSS texturing mode to diffuse color using the existing helper function
    brdfData.diffuse = GetModifiedDiffuseColorForSSS(brdfData.diffuse, subsurfaceMask, diffusionProfileIndex);
}

void LightingPhysicallyBasedSplit(BRDFData brdfData, BRDFData brdfDataClearCoat,
    half3 lightColor, half3 lightDirectionWS, float diffuselightAttenuation, float transmissionLightAttenuation,
    half3 normalWS, half3 viewDirectionWS,
    half clearCoatMask, bool specularHighlightsOff, half3 transmittance,
    uint diffusionProfileIndex, half subsurfaceMask, out half3 diffuse, out half3 specular)
{
    SSSBxDFContext Context;
    InitSSSBxDFContext(Context, normalWS, viewDirectionWS, lightDirectionWS);

    half NdotL = Context.NoL;
    half clampedNdotL = saturate(NdotL);
    half clampedNoV = saturate(abs(Context.NoV) + 1e-5);

    #define TRANSMISSION_WRAP_LIGHT 0.2588190451025207701
    half flippedNdotL = ComputeWrappedDiffuseLighting(-NdotL, TRANSMISSION_WRAP_LIGHT);

    half3 lightAtten = lightColor * diffuselightAttenuation;

    // Diffuse Power - use [branch] to skip unnecessary pow
    half diffuseNdotL = clampedNdotL;
    float diffusePower = GetDiffusePower(diffusionProfileIndex);
    [branch] if (diffusePower != 0.0)
    {
        half powExp = max(diffusePower + 1, 1.0);
        diffuseNdotL = pow(diffuseNdotL, powExp) * (diffusePower * 0.5 + 1);
    }

    //=========================================================================
    // Specular + energy terms unified computation
    //=========================================================================
    specular = half3(0, 0, 0);
    half3 energyPreservation = half3(1, 1, 1);

#ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        half energyRoughness;
        half3 specBRDF;
        SSSBxDFContext SpecContext;
        SpecContext.NoL = Context.NoL;
        SpecContext.NoV = clampedNoV;
        SpecContext.NoH = Context.NoH;
        SpecContext.VoH = Context.VoH;

        #if defined(_USE_DUAL_SPECULAR_LOBE)
            half L0R, L1R, LMix;
            GetDualSpecularLobeParameters(brdfData.roughness, subsurfaceMask,
                _DualSpecularLobe0Roughness, _DualSpecularLobe1Roughness, _DualSpecularLobeMix,
                L0R, L1R, LMix);
            half AverageRoughness;
            specBRDF = DualSpecularGGX_SSS(brdfData.specular, SpecContext, clampedNdotL,
                L0R, L1R, LMix, AverageRoughness);
            energyRoughness = AverageRoughness;
        #else
            specBRDF = SingleSpecularGGX_SSS(brdfData.specular, SpecContext, clampedNdotL, brdfData.roughness);
            energyRoughness = brdfData.roughness;
        #endif
        
        half3 energyConservation;
        ComputeEnergyTerms_SSS(brdfData.specular, energyRoughness, clampedNoV,
            energyPreservation, energyConservation);

        specular = specBRDF * energyConservation * (lightAtten * clampedNdotL);
    }
#endif

    // Diffuse: Lambert with energy preservation
    half3 diffuseBase = brdfData.diffuse * (1.0 / PI);
    half3 diffR = diffuseBase * energyPreservation * (lightAtten * diffuseNdotL);
    half3 diffT = brdfData.diffuse * (lightColor * (transmissionLightAttenuation * flippedNdotL)) * transmittance;
    diffuse = diffR + diffT;
}

// ============================================================================
// Terrain-style lighting (for snow-covered areas, consistent with TerrainFar)
// ============================================================================
half3 BRDFPhysicallyBased_TerrainStyle(BRDFData brdfData, half ndotL, half3 lightDirectionWS,
    half3 normalWS, half3 viewDirectionWS,
    bool specularHighlightsOff,
    half metallic, half NoH2, half LoH2)
{
    half3 brdf = brdfData.diffuse;

    [branch] if (!specularHighlightsOff)
    {
        half directSpecular = DirectBRDFSpecularOpt(brdfData, normalWS, lightDirectionWS, viewDirectionWS, NoH2, LoH2);
        
        half fastRemapA = lerp(_NoMetalDirectSpecularDetail, _MetalDirectSpecularDetail, metallic);
        half fastRemapB = lerp(_NoMetalDirectSpecularIntensity, _MetalDirectSpecularIntensity, metallic);
        directSpecular = fastRemap(directSpecular, fastRemapA, fastRemapB);
        
        brdf += brdfData.specular * directSpecular;
    }
    return brdf;
}

void LightingPhysicallyBasedSplit_TerrainStyle(BRDFData brdfData, BRDFData brdfDataClearCoat,
    half3 lightColor, half3 lightDirectionWS, float lightAttenuation,
    half3 normalWS, half3 viewDirectionWS,
    half clearCoatMask, bool specularHighlightsOff,
    half metallic,
    out half3 diffuse, out half3 specular)
{
    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    
    half NoH2, LoH2;
    DirectBRDFSpecularData(normalWS, lightDirectionWS, viewDirectionWS, NoH2, LoH2);
    
    half3 brdf = BRDFPhysicallyBased_TerrainStyle(brdfData, NdotL, lightDirectionWS,
        normalWS, viewDirectionWS, specularHighlightsOff, metallic, NoH2, LoH2);
    
    half3 radiance = lightColor * (lightAttenuation * NdotL);
    
    diffuse = brdfData.diffuse * radiance;
    specular = (brdf - brdfData.diffuse) * radiance;
}


#endif