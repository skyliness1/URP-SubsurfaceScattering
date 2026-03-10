#ifndef SSSLIT_FORWARD_PASS_INCLUDED
#define SSSLIT_FORWARD_PASS_INCLUDED

// Include SSSLitInput first (contains UnityPerMaterial CBUFFER for SRP Batcher)
#include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/SubsurfaceScattering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/BSSRDF.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#if defined(_PARALLAXMAP)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

struct SSSLitAttributes
{
    #if defined(COMPRESS_VERTEX_BUFFER) && !defined(_SUPPORT_COMPRESS_VERTEX_OFF)
    float4 positionOS            : POSITION;
    half4 tbn                  : NORMAL;
    #else
    float4 positionOS           : POSITION;
    half3 normalOS              : NORMAL;
    half4 tangentOS             : TANGENT;
    #endif
    float2 texcoord             : TEXCOORD0;
    float2 staticLightmapUV     : TEXCOORD1;
    float2 texcoord2             : TEXCOORD2;
    float4 color                 : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SSSLitVaryings
{
    float4 positionCS               : SV_POSITION;
    float2 uv                       : TEXCOORD0;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 1);
    float3 positionWS               : TEXCOORD3;
    half2  fogData                  : TEXCOORD6;
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD7;
    #endif
    float3 normalWS                  : TEXCOORD4;
    float positionVSZ                  : TEXCOORD10;  //相机空间Z分量，控制高光距离衰减
    half4 tangentWS                 : TEXCOORD5;    // xyz: tangent, w: sign
    #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                 : TEXCOORD8;
    #endif
    half3 viewDirWS : TEXCOORD11;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

void InitializeSSSInputData(SSSLitVaryings input, half3 normalTS, half2 normalizedScreenSpaceUV, out InputData inputData)
{
    inputData = (InputData)0;   
    inputData.positionWS = input.positionWS;
    inputData.vertexLighting = 0;
    
    half3 viewDirWS = normalize(input.viewDirWS);    
    #if defined(_NORMALMAP)
        half sgn = input.tangentWS.w;      // should be either +1 or -1
        half3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
        half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
        inputData.tangentToWorld = tangentToWorld;
        inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
    #else
        inputData.normalWS = input.normalWS;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    // mobile no realtime scene shadow
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
    #elif defined(SOUL_DEFERRED_RENDERING) || defined(CONSTANT_EDITOR)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
    inputData.shadowCoord = 0;
    #endif
    #else
    inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    //Baked Date
    float2 staticLightmapUV = 0;
    half3 vertexSH = 0;
    #if defined(LIGHTMAP_ON)
    staticLightmapUV = input.staticLightmapUV;
    #else
    vertexSH = input.vertexSH;
    #endif
    GetInputDataBakedData(staticLightmapUV, vertexSH, input.normalWS, inputData.normalWS, inputData, 1);

    //Fog
    inputData.fogCoord = GetExponentialHeightFogColor(-inputData.viewDirectionWS, input.fogData.x, input.fogData.y);

    inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;

    //Debug
    #if defined(DEBUG_DISPLAY)
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
SSSLitVaryings SSSBufferVertex(SSSLitAttributes input)
{
    SSSLitVaryings output = (SSSLitVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    
#if defined(COMPRESS_VERTEX_BUFFER) && !defined(_SUPPORT_COMPRESS_VERTEX_OFF)
    float3 inputPositionOS = SoulUnpackPosition(input.positionOS,_LocalMeshBoundsCenter.rgb,_LocalMeshBoundsSize.rgb);
#else
    float3 inputPositionOS = input.positionOS.xyz;
#endif
    VertexPositionInputs vertexInput = GetVertexPositionInputs(inputPositionOS);
    float inputTangentOSSign;
#if defined(COMPRESS_VERTEX_BUFFER) && !defined(_SUPPORT_COMPRESS_VERTEX_OFF)
    VertexNormalInputs normalInput = GetVertexNormalInputsUnpack(input.tbn, inputTangentOSSign);
#else
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    inputTangentOSSign = input.tangentOS.w;
#endif
    
    half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    output.fogData = GetExponentialHeightFogFactor(-viewDirWS);
    output.uv = input.texcoord;
    output.normalWS = normalInput.normalWS;
    output.positionVSZ = vertexInput.positionVS.z;
    
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    real sign = inputTangentOSSign * GetOddNegativeScale();
    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    output.tangentWS = tangentWS;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
    output.viewDirTS = viewDirTS;
#endif

#if defined(LIGHTMAP_ON)
    output.staticLightmapUV.xy = input.staticLightmapUV.xy * unity_LightmapST.xy + unity_LightmapST.zw;
#else
    output.vertexSH.xyz = SampleSHVertex(output.normalWS.xyz);
#endif
    
    output.positionWS = vertexInput.positionWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;
    
    output.viewDirWS = viewDirWS;
    return output;
}

// Overload for Light struct
void LightingPhysicallyBasedSplit(BRDFData brdfData, BRDFData brdfDataClearCoat, Light diffuselight, Light transmissionLight,
    half3 normalWS, half3 viewDirectionWS, half clearCoatMask, bool specularHighlightsOff,
    half3 transmittance, uint diffusionProfileIndex, half subsurfaceMask, 
    out half3 diffuse, out half3 specular)
{
    LightingPhysicallyBasedSplit(brdfData, brdfDataClearCoat,
        diffuselight.color, diffuselight.direction, diffuselight.distanceAttenuation * diffuselight.shadowAttenuation,
        transmissionLight.shadowAttenuation * transmissionLight.distanceAttenuation,
        normalWS, viewDirectionWS, clearCoatMask, specularHighlightsOff, transmittance,
        diffusionProfileIndex, subsurfaceMask, diffuse, specular);
}


//=============================================================================
// Multi-Layer Surface Data Initialization
//=============================================================================
inline void InitializeStandardSSSLitSurfaceData(
    float2 uv, 
    float3 viewDirTS_detail, 
    out SurfaceData outSurfaceData, 
    out half subsurfaceMask, 
    out half parallaxMask,
    out half layer4Mask)  // 改为输出遮罩而不是颜色
{
    // Base Layer
    half2 baseUV = uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
    half2 baseNormalUV = uv * _Layer1NormalTilingOffset.xy + _Layer1NormalTilingOffset.zw;
    half4 albedoAlpha = SampleAlbedoAlpha(baseUV, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
    
    half3 baseAlbedo = albedoAlpha.rgb * _BaseColor.rgb;
    baseAlbedo = AlphaModulate(baseAlbedo, outSurfaceData.alpha);
    
    // Sample base MAHS
    MAHSData baseMAHS = SampleBaseMAHS(baseUV);
    
    half3 baseNormalTS = SampleNormal(baseNormalUV, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    
    // Initialize with base layer values
    half3 finalAlbedo = baseAlbedo;
    half3 finalNormalTS = baseNormalTS;
    half finalMetallic = baseMAHS.metallic;
    half finalOcclusion = baseMAHS.occlusion;
    half finalSmoothness = baseMAHS.smoothness;
    half finalSubsurfaceMask = _BaseLayerSubsurfaceScattering;
    
    half layer2MaskForDetail = 0;
    half layer3MaskForDetail = 0;
    half layer4MaskForDetail = 0;
    parallaxMask = 0;
    
    //=========================================================================
    // Second Layer Blending
    //=========================================================================
#if defined(_USE_SECOND_LAYER)
    LayerData secondLayer = SampleSecondLayer(uv);
    
    if (secondLayer.mask > 0.001)
    {
        finalAlbedo = lerp(finalAlbedo, secondLayer.albedo, secondLayer.mask);
        finalNormalTS = BlendNormals_Linear(finalNormalTS, secondLayer.normalTS, secondLayer.mask);
        finalMetallic = lerp(finalMetallic, secondLayer.metallic, secondLayer.mask);
        finalOcclusion = lerp(finalOcclusion, secondLayer.occlusion, secondLayer.mask);
        finalSmoothness = lerp(finalSmoothness, secondLayer.smoothness, secondLayer.mask);
        finalSubsurfaceMask = lerp(finalSubsurfaceMask, _SecondLayerSubsurfaceScattering, secondLayer.mask);
        layer2MaskForDetail = secondLayer.mask;
    }
#endif

    //=========================================================================
    // Third Layer Blending
    //=========================================================================
#if defined(_USE_THIRD_LAYER)
    LayerData thirdLayer = SampleThirdLayer(uv);
    
    if (thirdLayer.mask > 0.001)
    {
        finalAlbedo = lerp(finalAlbedo, thirdLayer.albedo, thirdLayer.mask);
        finalNormalTS = BlendNormals_Linear(finalNormalTS, thirdLayer.normalTS, thirdLayer.mask);
        finalMetallic = lerp(finalMetallic, thirdLayer.metallic, thirdLayer.mask);
        finalOcclusion = lerp(finalOcclusion, thirdLayer.occlusion, thirdLayer.mask);
        finalSmoothness = lerp(finalSmoothness, thirdLayer.smoothness, thirdLayer.mask);
        finalSubsurfaceMask = lerp(finalSubsurfaceMask, _ThirdLayerSubsurfaceScattering, thirdLayer.mask);
        layer3MaskForDetail = thirdLayer.mask;
    }
#endif

    // Apply color adjustment
    finalAlbedo *= _OverallColorAdjustment.rgb;
   
    // Apply overall scales
    finalSmoothness *= _OverallSmoothnessScale;
    finalOcclusion *= _OverallOcclusionScale;

    // =========================================================================
    // Parallax Detail - 使用 Lerp 混合（新版本）
    // =========================================================================
    #if defined(_USE_PARALLAX_DETAIL)
    {
        // 使用新的 Lerp 混合方式
        ParallaxDetailResult parallaxResult = GetParallaxDetailResult(
            uv,
            viewDirTS_detail,
            finalNormalTS,
            finalAlbedo,  // 传入当前 albedo 用于混合
            layer2MaskForDetail,
            layer3MaskForDetail,
            0
        );
        
        // 直接使用混合后的颜色作为最终 albedo
        finalAlbedo = parallaxResult.color;
        
        // 输出视差遮罩
        parallaxMask = parallaxResult.mask;
        subsurfaceMask = lerp(subsurfaceMask, 0, saturate(parallaxMask));
    }
    #endif

    // Blend overlay normal
    float2 overlayUV = uv * _OverlayTilingOffset.xy + _OverlayTilingOffset.zw;
    half3 overlayNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_OverlayNormalMap, sampler_BumpMap, overlayUV), _OverlayNormalScale);

    half3 finalNormalTS_NoBlend = BlendNormalRNM(finalNormalTS, overlayNormal);

    //=========================================================================
    // Fourth Layer Blending
    //=========================================================================
#if defined(_USE_FOURTH_LAYER)
    LayerData fourthLayer = SampleFourthLayer(uv);
    
    if (fourthLayer.mask > 0.001)
    {
        finalAlbedo = lerp(finalAlbedo, fourthLayer.albedo, fourthLayer.mask);
        finalNormalTS = BlendNormals_Linear(finalNormalTS, fourthLayer.normalTS, fourthLayer.mask);
        finalNormalTS_NoBlend = BlendNormals_Linear(finalNormalTS_NoBlend, fourthLayer.normalTS, fourthLayer.mask);
        finalMetallic = lerp(finalMetallic, fourthLayer.metallic, fourthLayer.mask);
        finalOcclusion = lerp(finalOcclusion, fourthLayer.occlusion, fourthLayer.mask);
        finalSmoothness = lerp(finalSmoothness, fourthLayer.smoothness, fourthLayer.mask);
        finalSubsurfaceMask = lerp(finalSubsurfaceMask, _FourthLayerSubsurfaceScattering, fourthLayer.mask);
        layer4MaskForDetail = fourthLayer.mask;
    }
#endif
    
    half3 finalNormalTS_Blend = BlendNormalRNM(finalNormalTS, overlayNormal);
    finalNormalTS = lerp(finalNormalTS_NoBlend, finalNormalTS_Blend, _FourthLayerNormalBlendIntensity);

    subsurfaceMask = finalSubsurfaceMask;
    layer4Mask = layer4MaskForDetail;

    //=========================================================================
    // Output Surface Data
    //=========================================================================

    half albedoLuminance = Luminance(finalAlbedo);
    half maxAlbedoLuminance = 0.5;  

    if (albedoLuminance > maxAlbedoLuminance)
    {
        finalAlbedo *= maxAlbedoLuminance / albedoLuminance;
    }

    outSurfaceData.albedo = finalAlbedo;
    
#if _SPECULAR_SETUP
    outSurfaceData.metallic = half(1.0);
    outSurfaceData.specular = _SpecColor.rgb;
#else
    outSurfaceData.metallic = finalMetallic;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
#endif

    outSurfaceData.smoothness = finalSmoothness;
    outSurfaceData.normalTS = normalize(finalNormalTS);
    outSurfaceData.occlusion = finalOcclusion;
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half2 clearCoat = SampleClearCoat(uv);
    outSurfaceData.clearCoatMask       = clearCoat.r;
    outSurfaceData.clearCoatSmoothness = clearCoat.g;
#else
    outSurfaceData.clearCoatMask       = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
#endif
}

// Used in Standard (Physically Based) shader with split lighting output
void SSSBufferFragment(
    SSSLitVaryings input
    , out half4 outSpecular : SV_Target0      // Specular lighting (no SSS blur)
    , out half4 outDiffuse : SV_Target1       // Diffuse lighting (for SSS blur)
    , out SSSBufferType outSSSBuffer : SV_Target2  // SSS material data (diffusion profile, thickness, etc.)
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target3
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    
    /*
#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif
    */

    float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    // 获取切线空间视角方向
    half3 biTangentWS = cross(input.normalWS.xyz, input.tangentWS. xyz) * input.tangentWS.w;
    half3x3 TBN = half3x3(input.tangentWS.xyz, biTangentWS, input.normalWS.xyz);
    half3 viewDirTS_detail = TransformWorldToTangent(input.viewDirWS, TBN);
    viewDirTS_detail = normalize(viewDirTS_detail);

    half subsurfaceMask;
    half layer4Mask;
    half parallaxMask = 0;  // 改为接收遮罩
    
    SurfaceData surfaceData;
    InitializeStandardSSSLitSurfaceData(input.uv, viewDirTS_detail, surfaceData, subsurfaceMask, parallaxMask, layer4Mask);

    // =========================================================================
    // Debug Mode 处理
    // =========================================================================
    if (IsInSSSDebugMode())
    {
        // 采样 Debug 数据
        MultiLayerSSSDebugData debugData = SampleMultiLayerSSSDebugData(input.uv);
        
        // 获取 Debug 输出颜色
        half3 debugColor = GetMultiLayerSSSDebugOutput(debugData);
        
        // Debug 模式下的输出
        outSpecular = half4(0, 0, 0, 1);  // 关闭镜面反射
        outDiffuse = half4(debugColor, 1);  // Debug 颜色输出到 Diffuse
        
        // 仍然需要输出有效的 SSSBuffer
        SSSData sssData;
        sssData.diffuseColor = debugColor;
        sssData.subsurfaceMask = debugData.combinedSSSMask;
        sssData.diffusionProfileIndex = GetDiffusionProfileIndex(_DiffusionProfileHash);
        ENCODE_INTO_SSSBUFFER(surfaceData, input.positionCS. xy, sssData, outSSSBuffer);
        
        #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
        #endif
        return;
    }
    
    half transmissionMask = SampleTransmissionMask(input.uv);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeSSSInputData(input, surfaceData.normalTS, normalizedScreenSpaceUV,inputData);
    
    SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv, _BaseMap);
    

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    // Calculate split lighting
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif

    // Get diffusion profile index first
    uint diffusionProfileIndex = GetDiffusionProfileIndex(_DiffusionProfileHash);
    
    // Initialize BRDF data with SSS-specific handling (overrides f0 from diffusion profile)
    // This also applies SSS texturing mode to modify diffuse color (following HDRP FillMaterialSSS)
    BRDFData brdfData;
    InitializeBRDFDataSSS(surfaceData, diffusionProfileIndex, subsurfaceMask, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        outSpecular = debugColor;
        outDiffuse = half4(0, 0, 0, 1.0);
        
        // Still need to output valid SSS buffer in debug mode
        SSSData sssData;
        sssData.diffuseColor = brdfData.diffuse;
        sssData.subsurfaceMask = subsurfaceMask;
        sssData.diffusionProfileIndex = GetDiffusionProfileIndex(_DiffusionProfileHash);
        ENCODE_INTO_SSSBUFFER(surfaceData, input.positionCS.xy, sssData, outSSSBuffer);
        
        return;
    }
    #endif

    // Clear-coat calculation...
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);

    //计算全局光照 (GI / Static Lighting)
    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 ambientShadowMask = CalculateShadowMask(inputData);
    half3 bakedDir = ambientShadowMask.yzw;

    Light mainLight = GetMainLight(inputData, ambientShadowMask, aoFactor);
    half nDotLUnSat = dot(mainLight.direction,inputData.normalWS);
    half nDotL = saturate(nDotLUnSat);
    half NoV = dot(inputData.normalWS, inputData.viewDirectionWS);
    
    //Trick 单独控制室外烘焙阴影强度
    #if defined(LIGHTMAP_OUTDOOR) && defined(LIGHTMAP_ON)
    #if defined(CONSTANT_EDITOR)
    half sceneShadowIntensity = lerp(1, _SoulSceneShadowIntensity,  _SoulCustomLightEnable);
    #else
    half sceneShadowIntensity = _SoulSceneShadowIntensity;
    #endif    
    #else
    half sceneShadowIntensity = 1;
    #endif

    mainLight.color *= inputData.ao;
    BRDFData brdfDataForGI = brdfData;
    
    half diffuseLuminance = Luminance(brdfData.diffuse);
    half targetMaxLuminance = _GIDiffuseScale;

    if (diffuseLuminance > targetMaxLuminance)
    {
        half3 normalizedColor = brdfData.diffuse / max(diffuseLuminance, 0.001);  // 提取色彩方向
        brdfDataForGI.diffuse = normalizedColor * targetMaxLuminance;              // 用目标亮度重建
    } 
    
    mainLight.shadowAttenuation = lerp(1.0, mainLight.shadowAttenuation, _ShadowIntensity * sceneShadowIntensity);
    MixRealtimeAndBakedGIOpt(mainLight, nDotL, inputData.bakedGI);
    
    half3 staticLighting = GlobalIlluminationSceneOpt(brdfDataForGI, brdfDataClearCoat, surfaceData.clearCoatMask,
                                          inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                          inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV, bakedDir, NoV,
                                          mainLight.shadowAttenuation, 1, surfaceData.metallic);

    #if defined(_USE_FOURTH_LAYER)
    if (layer4Mask > 0.001)
    {
        BRDFData snowBrdfForGI;
        InitializeBRDFData(surfaceData, snowBrdfForGI);
        
        half snowDiffuseLum = Luminance(snowBrdfForGI.diffuse);
        BRDFData snowBrdfForGIClamped = snowBrdfForGI;
        if (snowDiffuseLum > _GIDiffuseScale)
        {
            half3 normalizedColor = snowBrdfForGI.diffuse / max(snowDiffuseLum, 0.001);
            snowBrdfForGIClamped.diffuse = normalizedColor * _GIDiffuseScale;
        }
        
        half3 snowStaticLighting = GlobalIlluminationSceneOpt(snowBrdfForGIClamped, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV, bakedDir, NoV,
                                              mainLight.shadowAttenuation, 1, surfaceData.metallic);
        
        staticLighting = lerp(staticLighting, snowStaticLighting, layer4Mask);
    }
    #endif

    // Calculate transmittance for SSS (following HDRP approach)
    // Ref: HDRP FillMaterialTransmission() in SubsurfaceScattering.hlsl
    float2 thicknessRemap = _WorldScalesAndFilterRadiiAndThicknessRemaps[diffusionProfileIndex].zw;
    
    float thickness = SampleThickness(input.uv);
    half thicknessPow = clamp(10 - _ThicknessRange, 0.1, 5.0);
    thickness = pow(saturate(thickness), thicknessPow);
    half thicknessSharp = saturate(0.5 - _ThicknessPower);
    thickness = smoothstep(0.5 - thicknessSharp, 0.5 + thicknessSharp, thickness);
    thickness = thicknessRemap.x + thicknessRemap.y * thickness;
    
    // Compute transmittance using baked thickness here. It may be overridden for direct lighting
    // in the auto-thickness mode (but is always used for indirect lighting).
    half3 transmittance = ComputeTransmittanceDisney(_ShapeParamsAndMaxScatterDists[diffusionProfileIndex].rgb,
                                                     _TransmissionTintsAndFresnel0[diffusionProfileIndex].rgb,
                                                     thickness) * transmissionMask;

    // Split lighting following HDRP naming convention
    // diffuseLighting = direct diffuse + indirect diffuse + emission
    // specularLighting = direct specular + indirect specular
    half3 diffuseLighting = 0;
    half3 specularLighting = 0;

    half4 shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    Light mainLightWithShadow = GetMainLightWithCustomRealTimeShadowIntensity(shadowCoord, inputData.positionWS, inputData.shadowMask, 1);
    mainLightWithShadow.color *= aoFactor.directAmbientOcclusion;;

    // GI - Split into indirect diffuse and indirect specular
    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    // Use simplified fresnel term for environment BRDF (URP standard approach)
    // The actual f0 value is already correctly set in brdfData.specular from diffusion profile
    half fresnelTerm = Pow4(1.0 - NoV);
    
    // Indirect diffuse (from baked GI / light probes)
    diffuseLighting += staticLighting;
    
    // Indirect specular (environment reflection)
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, inputData.positionWS, 
                                                         brdfData.perceptualRoughness, 1.0h, 
                                                         inputData.normalizedScreenSpaceUV);
    
    half3 specularLightingForIce = indirectSpecular * EnvironmentBRDFSpecular(brdfData, fresnelTerm);
    
    //half3 specularLightingForSnow = indirectSpecular * _FourthLayerReflectionIntensity;
    //specularLighting = lerp(specularLightingForIce, specularLightingForSnow, layer4Mask);
    specularLighting = specularLightingForIce;

    /*
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    // Clear coat indirect specular
    half3 coatIndirectSpecular = GlossyEnvironmentReflection(reflectVector, inputData.positionWS, 
                                                             brdfDataClearCoat.perceptualRoughness, 1.0h,
                                                             inputData.normalizedScreenSpaceUV);
    half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * Pow4(1.0 - NoV);
    specularLighting = specularLighting * (1.0 - surfaceData.clearCoatMask * coatFresnel) + 
                       coatIndirectSpecular * EnvironmentBRDFSpecular(brdfDataClearCoat, fresnelTerm) * surfaceData.clearCoatMask;
#endif
    */

    // Main light - Direct lighting
    half3 mainDiffuse = 0, mainSpecular = 0;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        // === 原始 SSS 光照路径（用于非雪区域）===
        half3 sssDiffuse, sssSpecular;
        LightingPhysicallyBasedSplit(brdfData, brdfDataClearCoat, mainLightWithShadow, mainLight,
                                     inputData.normalWS, inputData.viewDirectionWS,
                                     surfaceData.clearCoatMask, specularHighlightsOff,
                                     transmittance, diffusionProfileIndex,
                                     subsurfaceMask, sssDiffuse, sssSpecular);
        
#if defined(_USE_FOURTH_LAYER)
        if (layer4Mask > 0.001)
        {
           
            BRDFData snowBrdfData;
            InitializeBRDFData(surfaceData, snowBrdfData);
            
            half3 terrainDiffuse, terrainSpecular;
            LightingPhysicallyBasedSplit_TerrainStyle(snowBrdfData, brdfDataClearCoat,
                mainLightWithShadow.color, mainLightWithShadow.direction,
                mainLightWithShadow.distanceAttenuation * mainLightWithShadow.shadowAttenuation,
                inputData.normalWS, inputData.viewDirectionWS,
                surfaceData.clearCoatMask, specularHighlightsOff,
                surfaceData.metallic,
                terrainDiffuse, terrainSpecular);
            
            // 饱和度增强（与山石一致）
            half nDotLForSat = saturate(dot(mainLightWithShadow.direction, inputData.normalWS));
            #if defined(LIGHTMAP_ON)
                nDotLForSat *= inputData.shadowMask.x;
            #endif
            half3 terrainSatColor = SceneSaturation(terrainDiffuse + terrainSpecular);
            half3 terrainTotal = lerp(terrainDiffuse + terrainSpecular, terrainSatColor, nDotLForSat);
            terrainDiffuse = terrainTotal * (Luminance(terrainDiffuse) / max(Luminance(terrainDiffuse + terrainSpecular), 0.001));
            terrainSpecular = terrainTotal - terrainDiffuse;
            
            // 按 layer4Mask 混合 SSS 路径和山石路径
            mainDiffuse = lerp(sssDiffuse, terrainDiffuse, layer4Mask);
            mainSpecular = lerp(sssSpecular, terrainSpecular, layer4Mask);
        }
        else
#endif
        {
            mainDiffuse = sssDiffuse;
            mainSpecular = sssSpecular;
        }
        
        diffuseLighting += mainDiffuse;
        specularLighting += mainSpecular;
    }

    // === 视线补光（与山石一致，仅雪覆盖区域）===
#if defined(_USE_FOURTH_LAYER)
    if (layer4Mask > 0.001)
    {
        #if defined(SOUL_VIEW_SPECULAR)
            #if defined(LIGHTMAP_OUTDOOR) || defined(LIGHTMAP_ON)
                BRDFData snowBrdfForView;
                InitializeBRDFData(surfaceData, snowBrdfForView);
                
                half3 viewLighting = ViewLightingWithBakedGIScene(snowBrdfForView.roughness,
                       inputData.normalWS,
                       inputData.viewDirectionWS,
                       mainLight.direction,
                       surfaceData.metallic,
                       inputData.bakedGI,
                       snowBrdfForView.specular);
                specularLighting += viewLighting * layer4Mask;
            #endif
        #endif
    }
#endif


    /*
     *
    // Additional lights
   #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    // Forward+ 方向附加光循环（与山石一致使用 USE_FORWARD_PLUS）
    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            half3 addDiffuse, addSpecular;

#if defined(_USE_FOURTH_LAYER)
            if (layer4Mask > 0.001)
            {
                // 雪覆盖区域：与山石一致的附加光处理
                BRDFData snowBrdfAdd;
                InitializeBRDFData(surfaceData, snowBrdfAdd);
                
                // 山石对附加光使用 LightingPhysicallyBased（非Scene版本）
                half3 terrainAddLight = LightingPhysicallyBased(snowBrdfAdd, brdfDataClearCoat, light,
                    inputData.normalWS, inputData.viewDirectionWS,
                    surfaceData.clearCoatMask, specularHighlightsOff);

                // SSS 路径的附加光（修复：传入正确的 light）
                half3 sssAddDiffuse, sssAddSpecular;
                LightingPhysicallyBasedSplit(brdfData, brdfDataClearCoat, light, light,
                    inputData.normalWS, inputData.viewDirectionWS,
                    surfaceData.clearCoatMask, specularHighlightsOff,
                    transmittance, diffusionProfileIndex,
                    subsurfaceMask, sssAddDiffuse, sssAddSpecular);

                // 按 layer4Mask 混合
                // 山石的附加光不区分 diffuse/specular，统一输出
                // 这里将山石结果按比例拆分到 diffuse 和 specular
                half totalLum = max(Luminance(sssAddDiffuse + sssAddSpecular), 0.001);
                half diffuseRatio = Luminance(sssAddDiffuse) / totalLum;
                
                addDiffuse = lerp(sssAddDiffuse, terrainAddLight * diffuseRatio, layer4Mask);
                addSpecular = lerp(sssAddSpecular, terrainAddLight * (1.0 - diffuseRatio), layer4Mask);
            }
            else
#endif
            {
                // 非雪区域：标准 SSS 附加光（修复：传入正确的 light）
                LightingPhysicallyBasedSplit(brdfData, brdfDataClearCoat, light, light,
                    inputData.normalWS, inputData.viewDirectionWS,
                    surfaceData.clearCoatMask, specularHighlightsOff,
                    transmittance, diffusionProfileIndex,
                    subsurfaceMask, addDiffuse, addSpecular);
            }

            diffuseLighting += addDiffuse;
            specularLighting += addSpecular;
        }
    }
    #endif

    //聚光灯循环
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            half3 addDiffuse, addSpecular;

#if defined(_USE_FOURTH_LAYER)
            if (layer4Mask > 0.001)
            {
                // 雪覆盖区域：与山石一致的附加光处理
                BRDFData snowBrdfAdd;
                InitializeBRDFData(surfaceData, snowBrdfAdd);
                
                half3 terrainAddLight = LightingPhysicallyBased(snowBrdfAdd, brdfDataClearCoat, light,
                    inputData.normalWS, inputData.viewDirectionWS,
                    surfaceData.clearCoatMask, specularHighlightsOff);

                half3 sssAddDiffuse, sssAddSpecular;
                LightingPhysicallyBasedSplit(brdfData, brdfDataClearCoat, light, light,
                    inputData.normalWS, inputData.viewDirectionWS,
                    surfaceData.clearCoatMask, specularHighlightsOff,
                    transmittance, diffusionProfileIndex,
                    subsurfaceMask, sssAddDiffuse, sssAddSpecular);

                half totalLum = max(Luminance(sssAddDiffuse + sssAddSpecular), 0.001);
                half diffuseRatio = Luminance(sssAddDiffuse) / totalLum;
                
                addDiffuse = lerp(sssAddDiffuse, terrainAddLight * diffuseRatio, layer4Mask);
                addSpecular = lerp(sssAddSpecular, terrainAddLight * (1.0 - diffuseRatio), layer4Mask);
            }
            else
#endif
            {
                // 非雪区域：标准 SSS 附加光（修复：传入正确的 light）
                LightingPhysicallyBasedSplit(brdfData, brdfDataClearCoat, light, light,
                    inputData.normalWS, inputData.viewDirectionWS,
                    surfaceData.clearCoatMask, specularHighlightsOff,
                    transmittance, diffusionProfileIndex,
                    subsurfaceMask, addDiffuse, addSpecular);
            }

            diffuseLighting += addDiffuse;
            specularLighting += addSpecular;
        }
    LIGHT_LOOP_END
    #endif

    // Vertex lighting (direct diffuse only)
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    diffuseLighting += inputData.vertexLighting * brdfData.diffuse;
    #endif

    */
    

    // Emission - Following HDRP, emissive is part of diffuse lighting
    diffuseLighting += surfaceData.emission;

    // Output split lighting following HDRP convention
    // Apply fog to specular output
    specularLighting = MixFogExp(specularLighting, inputData.fogCoord);
    
    outSpecular = half4(specularLighting, OutputAlpha(surfaceData.alpha, IsSurfaceTypeTransparent(_Surface)));
    outDiffuse = half4(TagLightingForSSS(diffuseLighting), 1.0);
    //outDiffuse = half4(TagLightingForSSS(inputData.bakedGI), 1.0);

    // Encode SSS Buffer (Target2) - Following HDRP convention
    // This buffer contains material information needed for the SSS blur pass
    // Note: thickness is NOT stored in SSS Buffer, it's read from thickness map during blur pass
    SSSData sssData;
    sssData.diffuseColor = brdfData.diffuse;
    sssData.subsurfaceMask = subsurfaceMask;
    sssData.diffusionProfileIndex = GetDiffusionProfileIndex(_DiffusionProfileHash);
    
    // Encode into SSS Buffer
    ENCODE_INTO_SSSBUFFER(surfaceData, input.positionCS.xy, sssData, outSSSBuffer);

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

//-----------------------------------------------------------------------------
// DarkBackFace Implementation
//-----------------------------------------------------------------------------
struct Attributes_DarkBackFace
{
    float4 positionOS   : POSITION;                
};

struct Varyings_DarkBackFace
{
    float4 positionCS : SV_POSITION;                
    #if defined(_DITHER_SWITCH_ON)
    float3 positionWS : TEXCOORD5;
    #endif
};

Varyings_DarkBackFace VertexDarkBackFace(Attributes_DarkBackFace input)
{
    Varyings_DarkBackFace OUT;
    #if defined(COMPRESS_VERTEX_BUFFER)
    float3 inputPositionOS = SoulUnpackPosition(input.positionOS);
    #else
    float3 inputPositionOS = input.positionOS.xyz;
    #endif
    VertexPositionInputs positionInputs = GetVertexPositionInputs(inputPositionOS);
    OUT.positionCS = positionInputs.positionCS;
        
    #if defined(_DITHER_SWITCH_ON)
    OUT.positionWS = positionInputs.positionWS;
    #endif
    return OUT;
}

half4 FragmentDarkBackFace(Varyings_DarkBackFace input) : SV_Target
{
    half ditherClipValue = 1.0;    
    #if defined(_DITHER_SWITCH_ON)
    ditherClipValue = LitDitherValue(input.positionCS, input.positionWS.xyz);
    clip(ditherClipValue);
    #endif
        
    half4 color;
    color.w = input.positionCS.z;
    color.rgb = 0.15f;
    return color;
}

//Meta
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/MetaPass.hlsl"
struct Attributes_Meta
{
    float4 positionOS           : POSITION;
    half3 normalOS              : NORMAL;
    half4 tangentOS             : TANGENT;
    float2 texcoord0            : TEXCOORD0;
    float2 texcoord1            : TEXCOORD1;
    float2 texcoord2            : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings_Meta
{
    float4 positionCS   : SV_POSITION;
    float4 uv           : TEXCOORD0;
    #ifdef EDITOR_VISUALIZATION
    float2 VizUV        : TEXCOORD1;
    float4 LightCoord   : TEXCOORD2;
    #endif
    float3 normalWS     : TEXCOORD3;
    float3 positionWS     : TEXCOORD4;
    float4 tangentWS      : TEXCOORD5;
    float3 viewDirWS     : TEXCOORD6;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings_Meta UniversalVertexMeta(Attributes_Meta input)
{
    Varyings_Meta output = (Varyings_Meta)0;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.texcoord1, input.texcoord2);
    output.uv.xy = input.texcoord2;
    output.uv.zw = input.texcoord0;
    #ifdef EDITOR_VISUALIZATION
    UnityEditorVizData(input.positionOS.xyz, input.texcoord0, input.texcoord1, input.texcoord2, output.VizUV, output.LightCoord);
    #endif
    output.normalWS = TransformObjectToWorldDir(input.normalOS);
    output.tangentWS.xyz = TransformObjectToWorldDir(input.tangentOS.xyz);
    output.tangentWS.w = input.tangentOS.w;
    output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);
               
    return output;
}

void DoMetaPass(Varyings_Meta input, out SurfaceData surfaceData)
{
    float2 baseMapUV = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
    // 获取切线空间视角方向
    half3 biTangentWS = cross(input.normalWS.xyz, input.tangentWS. xyz) * input.tangentWS.w;
    half3x3 TBN = half3x3(input.tangentWS.xyz, biTangentWS, input.normalWS.xyz);
    half3 viewDirTS_detail = TransformWorldToTangent(input.viewDirWS, TBN);
    viewDirTS_detail = normalize(viewDirTS_detail);
    half subsurfaceMask;
    half parallaxMask = 0;
    half layer4Mask;
    InitializeStandardSSSLitSurfaceData(baseMapUV, viewDirTS_detail, surfaceData, subsurfaceMask, parallaxMask, layer4Mask);
}

#endif
