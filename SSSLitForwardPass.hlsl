#ifndef SSSLIT_FORWARD_PASS_INCLUDED
#define SSSLIT_FORWARD_PASS_INCLUDED

// Include SSSLitInput first (contains UnityPerMaterial CBUFFER for SRP Batcher)
#include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/SubsurfaceScattering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/BSSRDF.hlsl"

#include "Packages/com.unity.render-pipelines.universal/Shaders/Soul/TA/DarkScene/DarkScene.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Soul/TA/SignalZone/SignalZone.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Soul/TA/PoisionScene/PoisionScene.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Soul/TA/AdaptiveExposure/AdaptiveExposure.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Soul/TA/Dither/DitherFloat.hlsl"

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

half AlphaDither(half albedoAlpha, half4 color, half cutoff, half ditherClipValue, half leafClipValue)
{
    #if !defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) && !defined(_GLOSSINESS_FROM_BASE_ALPHA)
    half alpha = albedoAlpha * color.a;
    #else
    half alpha = color.a;
    #endif

    #if defined(_DEBUG_CLIP_ALL_ON)
    clip(-1);
    return alpha;
    #endif

    half FinalClipValue = 1;

    #if defined(_ALPHATEST_ON)
    #if defined(_DITHER_SWITCH_ON)
    FinalClipValue = min(alpha - (cutoff + leafClipValue), ditherClipValue);
    clip(FinalClipValue);
    #else
    clip(alpha - (cutoff + leafClipValue));
    #endif
    #else
    #if defined(_DITHER_SWITCH_ON)
    clip(ditherClipValue);
    #else
    #endif
    #endif

    return alpha;
}

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
void LightingPhysicallyBasedSplit(BRDFData brdfData, BRDFData brdfDataClearCoat, Light mainlight,
    half3 normalWS, half3 viewDirectionWS, half clearCoatMask, bool specularHighlightsOff,
    half3 transmittance, uint diffusionProfileIndex, half subsurfaceMask, 
    out half3 diffuse, out half3 specular)
{
    LightingPhysicallyBasedSplit(brdfData, brdfDataClearCoat,
        mainlight.color, mainlight.direction,
        mainlight.distanceAttenuation * mainlight.shadowAttenuation,
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
    out half layer4Mask,
    half ditherClipValue)  // 改为输出遮罩而不是颜色
{
    // Base Layer
    half2 baseUV = uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
    half2 baseNormalUV = uv * _Layer1NormalTilingOffset.xy + _Layer1NormalTilingOffset.zw;
    half4 albedoAlpha = SampleAlbedoAlpha(baseUV, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = AlphaDither( albedoAlpha.a, _BaseColor, _Cutoff, ditherClipValue, 0);
    
    half3 finalAlbedo = albedoAlpha.rgb * _BaseColor.rgb;
    
    // Sample base MAHS
    MAHSData baseMAHS = SampleBaseMAHS(baseUV);
    
    half3 finalNormalTS = SampleNormal(baseNormalUV, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    
    // Initialize with base layer values
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
    
    finalAlbedo = lerp(finalAlbedo, secondLayer.albedo, secondLayer.mask);
    finalNormalTS = BlendNormals_Linear(finalNormalTS, secondLayer.normalTS, secondLayer.mask);
    finalMetallic = lerp(finalMetallic, secondLayer.metallic, secondLayer.mask);
    finalOcclusion = lerp(finalOcclusion, secondLayer.occlusion, secondLayer.mask);
    finalSmoothness = lerp(finalSmoothness, secondLayer.smoothness, secondLayer.mask);
    finalSubsurfaceMask = lerp(finalSubsurfaceMask, _SecondLayerSubsurfaceScattering, secondLayer.mask);
    layer2MaskForDetail = secondLayer.mask;
#endif

    //=========================================================================
    // Third Layer Blending
    //=========================================================================
#if defined(_USE_THIRD_LAYER)
    LayerData thirdLayer = SampleThirdLayer(uv);
    
    finalAlbedo = lerp(finalAlbedo, thirdLayer.albedo, thirdLayer.mask);
    finalNormalTS = BlendNormals_Linear(finalNormalTS, thirdLayer.normalTS, thirdLayer.mask);
    finalMetallic = lerp(finalMetallic, thirdLayer.metallic, thirdLayer.mask);
    finalOcclusion = lerp(finalOcclusion, thirdLayer.occlusion, thirdLayer.mask);
    finalSmoothness = lerp(finalSmoothness, thirdLayer.smoothness, thirdLayer.mask);
    finalSubsurfaceMask = lerp(finalSubsurfaceMask, _ThirdLayerSubsurfaceScattering, thirdLayer.mask);
    layer3MaskForDetail = thirdLayer.mask;
    
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
        finalSubsurfaceMask *= saturate(1.0 - parallaxMask);
    }
    #endif

    // Blend overlay normal
    float2 overlayUV = uv * _OverlayTilingOffset.xy + _OverlayTilingOffset.zw;
    half3 overlayNormal = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_OverlayNormalMap, sampler_BumpMap, overlayUV), _OverlayNormalScale);

    //=========================================================================
    // Fourth Layer Blending
    //=========================================================================
#if defined(_USE_FOURTH_LAYER)
    half3 normalWithOverlay = BlendNormalRNM(finalNormalTS, overlayNormal);
    LayerData fourthLayer = SampleFourthLayer(uv);
    
    finalAlbedo = lerp(finalAlbedo, fourthLayer.albedo, fourthLayer.mask);
    finalMetallic = lerp(finalMetallic, fourthLayer.metallic, fourthLayer.mask);
    finalOcclusion = lerp(finalOcclusion, fourthLayer.occlusion, fourthLayer.mask);
    finalSmoothness = lerp(finalSmoothness, fourthLayer.smoothness, fourthLayer.mask);
    finalSubsurfaceMask = lerp(finalSubsurfaceMask, _FourthLayerSubsurfaceScattering, fourthLayer.mask);
    layer4MaskForDetail = fourthLayer.mask;

    half3 fourthBlendedNormal = BlendNormals_Linear(normalWithOverlay, fourthLayer.normalTS, fourthLayer.mask);
    half3 fourthNoOverlay = BlendNormals_Linear(finalNormalTS, fourthLayer.normalTS, fourthLayer.mask);
    fourthNoOverlay = BlendNormalRNM(fourthNoOverlay, overlayNormal);
    finalNormalTS = lerp(fourthNoOverlay, fourthBlendedNormal, _FourthLayerNormalBlendIntensity);
    #else
    finalNormalTS = BlendNormalRNM(finalNormalTS, overlayNormal);
    layer4MaskForDetail = 0;
    
#endif
    
    subsurfaceMask = finalSubsurfaceMask;
    layer4Mask = layer4MaskForDetail;

    //=========================================================================
    // Output Surface Data
    //=========================================================================

    // 优化：使用 Luminance() 内置函数并合并 min/max 操作
    half albedoLum = Luminance(finalAlbedo);
    // 当 albedoLum > 0.5 时需要缩放，等效于 min(1, 0.5/lum)
    half albedoScale = saturate(0.5h * rcp(max(albedoLum, 0.001h)));
    finalAlbedo *= albedoScale;

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
    
    #if defined(_EMISSION)
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
    #else
    outSurfaceData.emission = half3(0.0, 0.0, 0.0);
    #endif

    
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
    #ifdef SOUL_RENDERING_DEPTH_MRT
    , out half outDepth : SV_Target3
    #endif
    #ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target4
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

    // Dither handling
    half DitherClipValue = 1.0;
    #if defined(_DITHER_SWITCH_ON)
    half3 input_positionWS = input.positionWS.xyz;
    DitherClipValue = LitDitherValue(input.positionCS, input_positionWS, _DitherStartTime, _Dither_Fade);
    half ScreenOutlineMaskOutDither = 1; // 场景无描边时需要删除
    Dither_OutlineMaskout(normalizedScreenSpaceUV, input_positionWS, ScreenOutlineMaskOutDither);
    #endif

    // 获取切线空间视角方向
    #if defined(_USE_PARALLAX_DETAIL)
    half3 biTangentWS = cross(input.normalWS.xyz, input.tangentWS. xyz) * input.tangentWS.w;
    half3x3 TBN = half3x3(input.tangentWS.xyz, biTangentWS, input.normalWS.xyz);
    half3 viewDirTS_detail = TransformWorldToTangent(input.viewDirWS, TBN);
    viewDirTS_detail = normalize(viewDirTS_detail);
    #else
    half3 viewDirTS_detail = half3(0, 0, 1);
    #endif

    half subsurfaceMask;
    half layer4Mask;
    half parallaxMask = 0;  // 改为接收遮罩
    
    SurfaceData surfaceData;
    InitializeStandardSSSLitSurfaceData(input.uv, viewDirTS_detail, surfaceData, subsurfaceMask, parallaxMask, layer4Mask, DitherClipValue);

    /*
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
    */
    
    half transmissionMask = SampleTransmissionMask(input.uv);

    /*
#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif
    */

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

    // 优化：合并BRDF初始化，减少结构体复制
    uint diffusionProfileIndex = GetDiffusionProfileIndex(_DiffusionProfileHash);
    
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);
    
    // 优化：直接在 brdfData 基础上构建 SSS 专用 BRDF，避免完整结构复制
    BRDFData brdfDataForSSS = brdfData;
    float fresnel0 = _TransmissionTintsAndFresnel0[diffusionProfileIndex].a;
    brdfDataForSSS.specular = fresnel0;
    brdfDataForSSS.reflectivity = fresnel0;
    brdfDataForSSS.grazingTerm = saturate(surfaceData.smoothness + fresnel0);
    brdfDataForSSS.diffuse = GetModifiedDiffuseColorForSSS(brdfData.diffuse, subsurfaceMask, diffusionProfileIndex);

    /*
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
    */

    // Clear-coat calculation...
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);

    //计算全局光照 (GI / Static Lighting)
    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfDataForSSS);
    half4 ambientShadowMask = CalculateShadowMask(inputData);
    half3 bakedDir = ambientShadowMask.yzw;

    half4 shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    Light mainLight = GetMainLightWithCustomRealTimeShadowIntensity(shadowCoord, inputData.positionWS, inputData.shadowMask, 1);
    half nDotL = saturate(dot(mainLight.direction, inputData.normalWS));
    half NoV = dot(inputData.normalWS, inputData.viewDirectionWS);

    mainLight.color *= inputData.ao;
    
    // 优化：避免复制整个 BRDFData 结构，直接计算 GI diffuse 颜色
    half giDiffuseMultiplier = saturate(_GIDiffuseScale * rcp(max(Luminance(brdfDataForSSS.diffuse), 0.001h)));
    half3 giDiffuseColor = brdfDataForSSS.diffuse * giDiffuseMultiplier;
    
    // 优化：临时替换 diffuse 用于 GI 计算，之后恢复
    half3 originalDiffuse = brdfDataForSSS.diffuse;
    brdfDataForSSS.diffuse = giDiffuseColor;
    
    MixRealtimeAndBakedGIOpt(mainLight, nDotL, inputData.bakedGI);
    
    half3 staticLighting = GlobalIlluminationSceneOpt(brdfDataForSSS, brdfDataClearCoat, surfaceData.clearCoatMask,
                                          inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                          inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV, bakedDir, NoV,
                                          mainLight.shadowAttenuation, 1, surfaceData.metallic);

    // 恢复原始 diffuse 用于后续直接光照计算
    brdfDataForSSS.diffuse = originalDiffuse;

    // 优化：合并 thickness 处理链，预计算常量，减少 ALU
    float4 profileScaleData = _WorldScalesAndFilterRadiiAndThicknessRemaps[diffusionProfileIndex];
    float2 thicknessRemap = profileScaleData.zw;
    
    float thickness = SampleThickness(input.uv);
    // 优化：预计算 pow 指数和 smoothstep 参数
    half thicknessPow = clamp(10 - _ThicknessRange, 0.1, 5.0);
    half thicknessSharp = saturate(0.5 - _ThicknessPower);
    half thicknessLow = 0.5 - thicknessSharp;
    half thicknessHigh = 0.5 + thicknessSharp;
    thickness = smoothstep(thicknessLow, thicknessHigh, pow(saturate(thickness), thicknessPow));
    thickness = thicknessRemap.x + thicknessRemap.y * thickness;
    
    // 优化：预取 diffusion profile 数据，避免重复索引
    float3 shapeParams = _ShapeParamsAndMaxScatterDists[diffusionProfileIndex].rgb;
    float3 transmissionTint = _TransmissionTintsAndFresnel0[diffusionProfileIndex].rgb;
    half3 transmittance = ComputeTransmittanceDisney(shapeParams, transmissionTint, thickness) * transmissionMask;

    // 优化：直接赋值而非 +=0
    half3 diffuseLighting = staticLighting;
    
    // GI - Indirect specular
    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    half fresnelTerm = Pow4(1.0 - NoV);
    
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, inputData.positionWS, 
                                                         brdfDataForSSS.perceptualRoughness, 1.0h, 
                                                         inputData.normalizedScreenSpaceUV);
    
    half3 specularLighting = indirectSpecular * EnvironmentBRDFSpecular(brdfDataForSSS, fresnelTerm);

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
        LightingPhysicallyBasedSplit(brdfDataForSSS, brdfDataClearCoat, mainLight,
                                     inputData.normalWS, inputData.viewDirectionWS,
                                     surfaceData.clearCoatMask, specularHighlightsOff,
                                     transmittance, diffusionProfileIndex,
                                     subsurfaceMask, sssDiffuse, sssSpecular);
        
#if defined(_USE_FOURTH_LAYER)
        [branch] if (layer4Mask > 0.001)
        {
            
            half3 terrainDiffuse, terrainSpecular;
            LightingPhysicallyBasedSplit_TerrainStyle(brdfData, brdfDataClearCoat,
                mainLight.color, mainLight.direction,
                mainLight.distanceAttenuation * mainLight.shadowAttenuation,
                inputData.normalWS, inputData.viewDirectionWS,
                surfaceData.clearCoatMask, specularHighlightsOff,
                surfaceData.metallic,
                terrainDiffuse, terrainSpecular);
            
            // 饱和度增强（与山石一致）
            half nDotLForSat = saturate(dot(mainLight.direction, inputData.normalWS));
            #if defined(LIGHTMAP_ON)
                nDotLForSat *= inputData.shadowMask.x;
            #endif
            half3 terrainTotal = terrainDiffuse + terrainSpecular;
            half3 terrainSatColor = SceneSaturation(terrainTotal);
            terrainTotal = lerp(terrainTotal, terrainSatColor, nDotLForSat);
            
            half totalLum = max(Luminance(terrainDiffuse) + Luminance(terrainSpecular), 0.001);
            half diffRatio = Luminance(terrainDiffuse) / totalLum;
            terrainDiffuse = terrainTotal * diffRatio;
            terrainSpecular = terrainTotal * (1.0 - diffRatio);

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
    
    // Apply scene effects
    UNITY_BRANCH
    if (_Scene_Darked_On > 0)
        diffuseLighting.rgb = DarkSceneColor(diffuseLighting.rgb, input.positionWS);

    UNITY_BRANCH
    if (_SignalZone_On > 0)
        diffuseLighting.rgb = SignalZoneDarken(diffuseLighting.rgb, input.positionWS, input.normalWS);

    UNITY_BRANCH
    if (_AdaptiveExposure_Enabled > 0) 
        AdaptiveExposure(diffuseLighting.rgb);

    float depth;
    #ifdef SOUL_REVERSED_Z
    depth = 1 - input.positionCS.z;
    #else
    depth = input.positionCS.z;
    #endif

    #ifdef SOUL_RENDERING_DEPTH_MRT
    outDepth = depth;
    #endif
    #ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
    #endif

    diffuseLighting.rgb = ModifyColorInBasePass(diffuseLighting.rgb);

    // Output split lighting following HDRP convention
    // Apply fog to specular output
    specularLighting = MixFogExp(specularLighting, inputData.fogCoord);
    
    outSpecular = half4(specularLighting, OutputAlpha(surfaceData.alpha, IsSurfaceTypeTransparent(_Surface)));
    outDiffuse = half4(TagLightingForSSS(diffuseLighting), 1.0);

    // Encode SSS Buffer (Target2) - Following HDRP convention
    // This buffer contains material information needed for the SSS blur pass
    // Note: thickness is NOT stored in SSS Buffer, it's read from thickness map during blur pass
    SSSData sssData;
    sssData.diffuseColor = brdfDataForSSS.diffuse;
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
    InitializeStandardSSSLitSurfaceData(baseMapUV, viewDirTS_detail, surfaceData, subsurfaceMask, parallaxMask, layer4Mask, 1);
}

#endif
