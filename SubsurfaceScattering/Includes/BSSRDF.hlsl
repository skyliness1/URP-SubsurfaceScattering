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
    half VoL;
    half NoH;
    half VoH;
};

void InitSSSBxDFContext(inout SSSBxDFContext Context, half3 N, half3 V, half3 L)
{
    Context.NoL = dot(N, L);
    Context.NoV = dot(N, V);
    Context.VoL = dot(V, L);
    float InvLenH = rsqrt(2.0 + 2.0 * Context.VoL);
    Context.NoH = saturate((Context.NoL + Context.NoV) * InvLenH);
    Context.VoH = saturate(InvLenH + InvLenH * Context.VoL);
}

//=============================================================================
// Burley Diffuse - [Burley 2012, "Physically-Based Shading at Disney"]
// Reference: UE5 BRDF. ush Diffuse_Burley
//=============================================================================
half3 Diffuse_Burley_SSS(half3 DiffuseColor, half Roughness, half NoV, half NoL, half VoH)
{
    half FD90 = 0.5 + 2.0 * VoH * VoH * Roughness;
    half FdV = 1.0 + (FD90 - 1.0) * Pow5(1.0 - NoV);
    half FdL = 1.0 + (FD90 - 1.0) * Pow5(1.0 - NoL);
    return DiffuseColor * ((1.0 / PI) * FdV * FdL);
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
    half Vis_SmithV = NoL * (NoV * (1.0 - a) + a);
    half Vis_SmithL = NoV * (NoL * (1.0 - a) + a);
    return 0.5 * rcp(Vis_SmithV + Vis_SmithL);
}

// F_Schlick - Following UE5 BRDF.ush
half3 F_Schlick_SSS(half3 SpecularColor, half VoH)
{
    half Fc = Pow5(1.0 - VoH);
    // Anything less than 2% is physically impossible and is instead considered to be shadowing
    return saturate(50.0 * SpecularColor. g) * Fc + (1.0 - Fc) * SpecularColor;
}

//=============================================================================
// Energy Conservation - Following UE5 ShadingEnergyConservation. ush
// Reference:  Karis 2013, "Real Shading in Unreal Engine 4"
//=============================================================================

// Approximate directional-hemispherical reflectance (integral of F * G * D over hemisphere)
// This is used to compute energy that should be removed from diffuse
half3 EnvBRDFApprox_SSS(half3 SpecularColor, half Roughness, half NoV)
{
    // Lazarov 2013, "Getting More Physical in Call of Duty: Black Ops II"
    const half4 c0 = half4(-1.0, -0.0275, -0.572, 0.022);
    const half4 c1 = half4(1.0, 0.0425, 1.04, -0.04);
    half4 r = Roughness * c0 + c1;
    half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    half2 AB = half2(-1.04, 1.04) * a004 + r.zw;
    
    // F90 approximation for energy conservation
    half F90 = saturate(50.0 * SpecularColor.g);
    return SpecularColor * AB.x + F90 * AB.y;
}

// Compute energy preservation factor for diffuse (1 - specular reflection)
// This ensures energy conservation:  diffuse is attenuated by what specular reflects
half3 ComputeEnergyPreservation_SSS(half3 SpecularColor, half Roughness, half NoV)
{
    half3 specularReflectance = EnvBRDFApprox_SSS(SpecularColor, Roughness, NoV);
    return 1.0 - specularReflectance;
}

// Compute energy conservation factor for specular (multiple scattering compensation)
// Reference: Turquin 2019, "Practical multiple scattering compensation for microfacet models"
half3 ComputeEnergyConservation_SSS(half3 SpecularColor, half Roughness, half NoV)
{
    half3 specularReflectance = EnvBRDFApprox_SSS(SpecularColor, Roughness, NoV);
    // Approximate multiple scattering by boosting specular based on what's missing
    // This is a simplified version of the full multiple scattering compensation
    half3 multiScatterCompensation = 1.0 + SpecularColor * (1.0 / max(specularReflectance, 0.001) - 1.0);
    return min(multiScatterCompensation, 2.0); // Clamp to avoid extreme values
}

//=============================================================================
// Get dual specular lobe parameters from material properties
// Parameters exposed in material panel instead of LUT sampling
//=============================================================================
void GetDualSpecularLobeParameters(half Roughness, half SubsurfaceMask,
    half Lobe0RoughnessMult, half Lobe1RoughnessMult, half LobeMixParam,
    out half Lobe0Roughness, out half Lobe1Roughness, out half LobeMix)
{
    // Smooth blend out dual specular when subsurface mask is low
    // Following UE5's opacity-based blending
    half BlendFactor = saturate((SubsurfaceMask - 0.01) * 10.0);
    
    // Apply blend factor to roughness multipliers
    half ActualLobe0Mult = lerp(1.0, Lobe0RoughnessMult, BlendFactor);
    half ActualLobe1Mult = lerp(1.0, Lobe1RoughnessMult, BlendFactor);
    
    // Compute final lobe roughnesses
    // Lobe0: sharper (lower roughness), clamped to minimum 0.02 to avoid specular explosion
    Lobe0Roughness = max(saturate(Roughness * ActualLobe0Mult), 0.02);
    // Lobe1: broader (higher roughness)
    Lobe1Roughness = saturate(Roughness * ActualLobe1Mult);
    
    // Mix factor also affected by blend
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
    half D0 = D_GGX_SSS(Lobe0Alpha2, Context. NoH);
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
    // Initialize BxDF Context - Following UE5 approach
    SSSBxDFContext Context;
    InitSSSBxDFContext(Context, normalWS, viewDirectionWS, lightDirectionWS);
    
    half NdotL = Context.NoL;
    half clampedNdotL = saturate(NdotL);
    half clampedNoV = saturate(abs(Context.NoV) + 1e-5);
    
    // Following HDRP:  compute wrapped NdotL for transmission (back-lighting)
    #define TRANSMISSION_WRAP_LIGHT 0.2588190451025207701
    half flippedNdotL = ComputeWrappedDiffuseLighting(-NdotL, TRANSMISSION_WRAP_LIGHT);
    
    // Factor out common light attenuation to avoid redundant per-component multiply
    half3 lightAtten = lightColor * diffuselightAttenuation;
    half3 radianceR = lightAtten * clampedNdotL;
    half3 radianceT = lightColor * (transmissionLightAttenuation * flippedNdotL);
    
    // Apply Diffuse Power modification (following HDRP)
    half diffuseNdotL = clampedNdotL;
    float diffusePower = GetDiffusePower(diffusionProfileIndex);
    if (diffusePower != 0.0)
    {
        diffuseNdotL = pow(diffuseNdotL, max(diffusePower + 1, 1.0));
        diffuseNdotL *= diffusePower * 0.5 + 1;
    }

    //=========================================================================
    // Specular Calculation First (needed for energy preservation)
    //=========================================================================
    specular = half3(0, 0, 0);
    half energyPreservationRoughness = brdfData.roughness; // Used for energy calc
    half3 specReflectance = half3(0, 0, 0);
    
#ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        SSSBxDFContext SpecContext = Context;
        SpecContext.NoV = clampedNoV;
        
        half3 specBRDF;
        #if defined(_USE_DUAL_SPECULAR_LOBE)
            // Get parameters from material properties
            half Lobe0Roughness, Lobe1Roughness, LobeMix;
            GetDualSpecularLobeParameters(
                brdfData.roughness, 
                subsurfaceMask,
                _DualSpecularLobe0Roughness,  // From material
                _DualSpecularLobe1Roughness,  // From material
                _DualSpecularLobeMix,         // From material
                Lobe0Roughness, 
                Lobe1Roughness, 
                LobeMix
            );
            
            half AverageRoughness;
            specBRDF = DualSpecularGGX_SSS(brdfData.specular, SpecContext, clampedNdotL,
                Lobe0Roughness, Lobe1Roughness, LobeMix, AverageRoughness);
            
            // Use average roughness for energy conservation
            energyPreservationRoughness = AverageRoughness;
        #else
            // Standard single lobe specular
            specBRDF = SingleSpecularGGX_SSS(brdfData.specular, SpecContext, clampedNdotL, brdfData.roughness);
        #endif

        // Compute EnvBRDFApprox ONCE - derive both energy conservation and preservation.
        // Equivalent to calling ComputeEnergyConservation_SSS + ComputeEnergyPreservation_SSS
        // separately, but avoids the redundant second EnvBRDFApprox_SSS evaluation.
        // energyConservation = multiScatterCompensation = 1 + F0 * (1/reflectance - 1), clamped to 2
        // energyPreservation = 1 - reflectance  (computed after this block)
        specReflectance = EnvBRDFApprox_SSS(brdfData.specular, energyPreservationRoughness, clampedNoV);
        half3 energyConservation = min(1.0 + brdfData.specular * (rcp(max(specReflectance, 0.001)) - 1.0), 2.0);
        specular = specBRDF * energyConservation * radianceR;

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
        half brdfCoat = kDielectricSpec. r * DirectBRDFSpecular(brdfDataClearCoat, normalWS, lightDirectionWS, viewDirectionWS);
        half NoV = saturate(dot(normalWS, viewDirectionWS));
        half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * Pow4(1.0 - NoV);
        specular = specular * (1.0 - clearCoatMask * coatFresnel) + brdfCoat * clearCoatMask * radianceR;
#endif
    }
    else
    {
        specReflectance = EnvBRDFApprox_SSS(brdfData.specular, energyPreservationRoughness, clampedNoV);
    }
#else
    specReflectance = EnvBRDFApprox_SSS(brdfData.specular, energyPreservationRoughness, clampedNoV);
#endif

    //=========================================================================
    // Diffuse Calculation with Energy Preservation
    //=========================================================================
    // Derive energy preservation from cached specReflectance (avoids redundant EnvBRDFApprox call)
    half3 energyPreservation = 1.0 - specReflectance;
    
    half3 diffuseReflection;
    #if defined(_USE_BURLEY_DIFFUSE)
        // Burley Diffuse [Disney 2012]
        diffuseReflection = Diffuse_Burley_SSS(brdfData.diffuse, brdfData.roughness, 
            clampedNoV, clampedNdotL, Context.VoH);
    #else
        // Standard Lambert
        diffuseReflection = Diffuse_Lambert_SSS(brdfData.diffuse);
    #endif
    
    // Apply energy preservation to diffuse
    diffuseReflection *= energyPreservation;
    
    // Diffuse split:  reflection + transmission (reuse pre-computed lightAtten)
    half3 diffR = diffuseReflection * lightAtten * diffuseNdotL;
    half3 transmissionBxDF = brdfData.diffuse; 
    half3 diffT = transmissionBxDF * radianceT * transmittance;
    diffuse = diffR + diffT;
}

// ============================================================================
// 新增：山石风格光照计算（用于雪层覆盖区域，与 TerrainFar 保持一致）
// 对应 Lighting.hlsl 中 BRDFPhysicallyBased + LightingPhysicallyBasedSceneOpt
// ============================================================================
half3 BRDFPhysicallyBased_TerrainStyle(BRDFData brdfData, half ndotL, half3 lightDirectionWS,
    half3 normalWS, half3 viewDirectionWS,
    bool specularHighlightsOff,
    half metallic, half NoH2, half LoH2)
{
    // Diffuse —— 与山石一致，使用 _SoulCustomLightDiffuseIntensity
    #if defined(CONSTANT_EDITOR)
        half diffuseIntensity = lerp(1, _SoulCustomLightDiffuseIntensity, _SoulCustomLightEnable);
    #else
        half diffuseIntensity = _SoulCustomLightDiffuseIntensity;
    #endif
    
    half3 diffuse = brdfData.diffuse * diffuseIntensity;
    half3 brdf = diffuse;

    // Specular —— 与山石一致，使用 DirectBRDFSpecularOpt + fastRemap 截断
    [branch] if (!specularHighlightsOff)
    {
        half directSpecular = DirectBRDFSpecularOpt(brdfData, normalWS, lightDirectionWS, viewDirectionWS, NoH2, LoH2);
        
        // 高光快速映射截断（与山石完全一致）
        half fastRemapA = lerp(_NoMetalDirectSpecularDetail, _MetalDirectSpecularDetail, metallic);
        half fastRemapB = lerp(_NoMetalDirectSpecularIntensity, _MetalDirectSpecularIntensity, metallic);
        half directSpecularClamped = fastRemap(directSpecular, fastRemapA, fastRemapB);
        #if defined(CONSTANT_EDITOR)
            directSpecular = lerp(directSpecular, directSpecularClamped, _SoulDirectSpecularMaxEnable);
        #else
            directSpecular = directSpecularClamped;
        #endif
        
        // Custom Specular 强度（与山石一致）
        #if defined(CONSTANT_EDITOR)
            half specularIntensity = lerp(1, _SoulCustomLightSpecularIntensity, _SoulCustomLightEnable);
        #else
            half specularIntensity = _SoulCustomLightSpecularIntensity;
        #endif
        brdf += brdfData.specular * (directSpecular * specularIntensity);
    }
    return brdf;
}

// 新增：山石风格拆分光照（替代 LightingPhysicallyBasedSplit 用于雪层区域）
void LightingPhysicallyBasedSplit_TerrainStyle(BRDFData brdfData, BRDFData brdfDataClearCoat,
    half3 lightColor, half3 lightDirectionWS, float lightAttenuation,
    half3 normalWS, half3 viewDirectionWS,
    half clearCoatMask, bool specularHighlightsOff,
    half metallic,
    out half3 diffuse, out half3 specular)
{
    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    
    // 预计算 NoH2, LoH2 用于 DirectBRDFSpecularOpt
    half NoH2, LoH2;
    DirectBRDFSpecularData(normalWS, lightDirectionWS, viewDirectionWS, NoH2, LoH2);
    
    half3 brdf = BRDFPhysicallyBased_TerrainStyle(brdfData, NdotL, lightDirectionWS,
        normalWS, viewDirectionWS, specularHighlightsOff, metallic, NoH2, LoH2);
    
    half3 radiance = lightColor * (lightAttenuation * NdotL);
    
    // 将 brdf 拆分为 diffuse 和 specular 部分
    // diffuse 部分 = brdfData.diffuse * diffuseIntensity
    #if defined(CONSTANT_EDITOR)
        half diffuseIntensity = lerp(1, _SoulCustomLightDiffuseIntensity, _SoulCustomLightEnable);
    #else
        half diffuseIntensity = _SoulCustomLightDiffuseIntensity;
    #endif
    
    diffuse = brdfData.diffuse * diffuseIntensity * radiance;
    specular = (brdf - brdfData.diffuse * diffuseIntensity) * radiance;
}


#endif