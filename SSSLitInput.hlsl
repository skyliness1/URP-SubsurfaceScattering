#ifndef SSSLIT_INPUT_INCLUDED
#define SSSLIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Soul/TA/TACommon.hlsl"
#include "Packages/com.unity.render-pipelines.core@14.0.11/ShaderLibrary/GlobalSamplers.hlsl"

#if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
#define _DETAIL
#endif

// NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts. 
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _DetailAlbedoMap_ST;
    half4 _BaseColor;
    half4 _SpecColor;
    half4 _EmissionColor;
    half _Cutoff;
    half _Smoothness;
    half _Metallic;
    half _BumpScale;
    half _Parallax;
    half _OcclusionStrength;
    half _ClearCoatMask;
    half _ClearCoatSmoothness;
    half _Surface;
    half _TransmissionMask;
    half _Thickness;
    float _DiffusionProfileHash;
    float _BaseLayerSubsurfaceScattering;

    half4 _Layer1NormalTilingOffset;

    half _ThicknessRange;
    half _ThicknessPower;

    // Dual Specular Lobe Parameters
    half _DualSpecularLobe0Roughness;
    half _DualSpecularLobe1Roughness;
    half _DualSpecularLobeMix;

    half _GIDiffuseScale;

    // Second Layer Parameters
    half4 _SecondLayerTilingOffset;
    half _SecondLayerRange;
    half _SecondLayerPower;
    half _SecondLayerIntensity;
    half4 _SecondLayerDiffuseColor;
    half _SecondLayerNormalScale;
    half _SecondLayerSmoothnessScale;
    half _SecondLayerOcclusionScale;
    half _SecondLayerMetallicScale;
    half _SecondLayerSubsurfaceScattering;

    // Third Layer Parameters
    half4 _ThirdLayerTilingOffset;
    half _ThirdLayerRange;
    half _ThirdLayerPower;
    half _ThirdLayerIntensity;
    half4 _ThirdLayerDiffuseColor;
    half _ThirdLayerNormalScale;
    half _ThirdLayerSmoothnessScale;
    half _ThirdLayerOcclusionScale;
    half _ThirdLayerMetallicScale;
    half _ThirdLayerSubsurfaceScattering;

    // Fourth Layer Parameters
    half4 _FourthLayerTilingOffset;
    half _FourthLayerRange;
    half _FourthLayerPower;
    half _FourthLayerIntensity;
    half4 _FourthLayerDiffuseColor;
    half _FourthLayerNormalScale;
    half _FourthLayerNormalBlendIntensity;
    half _FourthLayerSmoothnessScale;
    half _FourthLayerOcclusionScale;
    half _FourthLayerMetallicScale;
    half _FourthLayerSubsurfaceScattering;
    //half _FourthLayerReflectionIntensity;

    // Overall Adjustments
    half4 _OverallColorAdjustment;
    half _OverlayNormalScale;
    half4 _OverlayTilingOffset;
    half _OverallSmoothnessScale;
    half _OverallOcclusionScale;

    // Parallax Detail Parameters
    half _RefractRatio;
        
    // Surface Detail (R Channel)
    half _SurfaceDetailHeightScale;
    half _SurfaceDetailTiling;
    half _SurfaceDetailScale;
    half _SurfaceDetailStrength;
    half4 _SurfaceDetailColor;
    half4 _SurfaceDetailLayerMask;  // x=Layer1, y=Layer2, z=Layer3, w=Layer4
        
    // Inner Detail (G Channel)
    half _InnerDetailHeightScale;
    half _InnerDetailTiling;
    half _InnerDetailScale;
    half _InnerDetailStrength;
    half4 _InnerDetailColor;
    half4 _InnerDetailLayerMask;
        
    // Third Detail (B Channel)
    half _ThirdDetailHeightScale;
    half _ThirdDetailTiling;
    half _ThirdDetailScale;
    half _ThirdDetailStrength;
    half4 _ThirdDetailColor;
    half4 _ThirdDetailLayerMask;

    // ++ [zoeyyzhong] - Dither
    //half4 _DitherTex_ST;
    half _DitherScale;
    half _DitherThreshold; //0.85
    half _EnableDither_All;
    half _EnableDither_Part;
    half _ChaToCamOffset;
    half _DitherStartTime;
    half _Dither_Fade;
    // -- [zoeyyzhong] - Dither

CBUFFER_END

// Textures - Base Layer
TEXTURE2D(_ParallaxMap);        SAMPLER(sampler_ParallaxMap);
TEXTURE2D(_MahsMap);            // Combined Metallic(R), AO(G), Smoothness(A)
TEXTURE2D(_ClearCoatMap);       SAMPLER(sampler_ClearCoatMap);

// SSS-specific textures
TEXTURE2D(_TransmissionMaskMap); SAMPLER(sampler_TransmissionMaskMap);
TEXTURE2D(_ThicknessMap);        SAMPLER(sampler_ThicknessMap);

// Second Layer Textures
TEXTURE2D(_SecondLayerMaskMap);     
TEXTURE2D(_SecondLayerDiffuseMap);  
TEXTURE2D(_SecondLayerNormalMap);   
TEXTURE2D(_SecondLayerMAHSMap);     

// Third Layer Textures
TEXTURE2D(_ThirdLayerMaskMap);     
TEXTURE2D(_ThirdLayerDiffuseMap);   
TEXTURE2D(_ThirdLayerNormalMap);   
TEXTURE2D(_ThirdLayerMAHSMap);

// Third Layer Textures
TEXTURE2D(_FourthLayerMaskMap);     
TEXTURE2D(_FourthLayerDiffuseMap);   
TEXTURE2D(_FourthLayerNormalMap);   
TEXTURE2D(_FourthLayerMAHSMap);   

// Volumetric Ice Textures
TEXTURE2D(_OverlayNormalMap);

TEXTURE2D(_ParallaxDetailTexture);   SAMPLER(sampler_ParallaxDetailTexture);

//=============================================================================
// MAHS Map Sampling - R:  Metallic, G: AO, A: Smoothness
//=============================================================================
struct MAHSData
{
    half metallic;
    half occlusion;
    half smoothness;
};

// Base layer MAHS sampling
MAHSData SampleBaseMAHS(float2 uv)
{
    MAHSData data;
#if defined(_MAHSMAP)
    half4 mahs = SAMPLE_TEXTURE2D(_MahsMap, sampler_LinearRepeat, uv);
    data.metallic = mahs.r * _Metallic;
    data.occlusion = LerpWhiteTo(mahs.g, _OcclusionStrength);
    data.smoothness = mahs.a * _Smoothness;
#else
    data.metallic = _Metallic;
    data.occlusion = 1.0;
    data.smoothness = _Smoothness;
#endif
    return data;
}

//=============================================================================
// Volumetric Ice - Overlay Blend for mask calculation
//=============================================================================
float OverlayBlend(float base, float blend)
{
    if (blend < 0.5)
    {
        return base * 2.0 * blend;
    }
    else
    {
        return 1.0 - (1.0 - base) * 2.0 * (1.0 - blend);
    }
}

// Sample layer mask - handles both standard mask and volumetric ice OA map
half SampleLayerMask(float2 uv, TEXTURE2D_PARAM(maskMap, sampler_maskMap), TEXTURE2D_PARAM(oaMap, sampler_oaMap), half intensity, bool useVolumetricIce)
{
    return SAMPLE_TEXTURE2D(maskMap, sampler_maskMap, uv).r * intensity;
}

//=============================================================================
// Layer Data Structure
//=============================================================================
struct LayerData
{
    half3 albedo;
    half3 normalTS;
    half metallic;
    half occlusion;
    half smoothness;
    half mask;
};

// 优化：更精简的层遮罩处理，减少 ALU 指令
half ProcessLayerMask(half rawMask, half range, half power, half intensity)
{
    half maskPow = clamp(10 - range, 0.1, 5.0);
    half processedMask = pow(saturate(rawMask), maskPow);
    half sharp = saturate(0.5 - power);
    // 优化：预计算 smoothstep 边界
    half low = 0.5 - sharp;
    half high = 0.5 + sharp;
    return smoothstep(low, high, processedMask) * intensity;
}

// Sample Second Layer
LayerData SampleSecondLayer(float2 uv)
{
    LayerData layer = (LayerData)0;
    
#if defined(_USE_SECOND_LAYER)
    half2 layer2UV = uv * _SecondLayerTilingOffset.xy + _SecondLayerTilingOffset.zw;
    // Sample mask
    half rawMask = SAMPLE_TEXTURE2D(_SecondLayerMaskMap, sampler_LinearClamp, uv).r;
    layer.mask = ProcessLayerMask(rawMask, _SecondLayerRange, _SecondLayerPower, _SecondLayerIntensity);
    
    layer.albedo = SAMPLE_TEXTURE2D(_SecondLayerDiffuseMap, sampler_BaseMap, layer2UV).rgb
                   * _SecondLayerDiffuseColor.rgb * _BaseColor.rgb;
    layer.normalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_SecondLayerNormalMap, sampler_BumpMap, layer2UV), _SecondLayerNormalScale);
    
    half4 mahs = SAMPLE_TEXTURE2D(_SecondLayerMAHSMap, sampler_LinearRepeat, layer2UV);
    layer.metallic = mahs.r * _SecondLayerMetallicScale;
    layer.occlusion = LerpWhiteTo(mahs.g, _SecondLayerOcclusionScale);
    layer.smoothness = mahs.a * _SecondLayerSmoothnessScale;
#endif
    
    return layer;
}

// Sample Third Layer
LayerData SampleThirdLayer(float2 uv)
{
    LayerData layer = (LayerData)0;
    
#if defined(_USE_THIRD_LAYER)
    half2 layer3UV = uv * _ThirdLayerTilingOffset.xy + _ThirdLayerTilingOffset.zw;
    // Sample mask
    half rawMask = SAMPLE_TEXTURE2D(_ThirdLayerMaskMap, sampler_LinearClamp, uv).r;
    layer.mask = ProcessLayerMask(rawMask, _ThirdLayerRange, _ThirdLayerPower, _ThirdLayerIntensity);
    
    layer.albedo = SAMPLE_TEXTURE2D(_ThirdLayerDiffuseMap, sampler_BaseMap, layer3UV).rgb
                   * _ThirdLayerDiffuseColor.rgb * _BaseColor.rgb;
    layer.normalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_ThirdLayerNormalMap, sampler_BumpMap, layer3UV), _ThirdLayerNormalScale);
    
    half4 mahs = SAMPLE_TEXTURE2D(_ThirdLayerMAHSMap, sampler_LinearRepeat, layer3UV);
    layer.metallic = mahs.r * _ThirdLayerMetallicScale;
    layer.occlusion = LerpWhiteTo(mahs.g, _ThirdLayerOcclusionScale);
    layer.smoothness = mahs.a * _ThirdLayerSmoothnessScale;
#endif
    
    return layer;
}

LayerData SampleFourthLayer(float2 uv)
{
    LayerData layer = (LayerData)0;
    
    #if defined(_USE_FOURTH_LAYER)
    half2 layer4UV = uv * _FourthLayerTilingOffset.xy + _FourthLayerTilingOffset.zw;
    // Sample mask
    half rawMask = SAMPLE_TEXTURE2D(_FourthLayerMaskMap, sampler_LinearClamp, uv).r;
    layer.mask = ProcessLayerMask(rawMask, _FourthLayerRange, _FourthLayerPower, _FourthLayerIntensity);
    
    layer.albedo = SAMPLE_TEXTURE2D(_FourthLayerDiffuseMap, sampler_BaseMap, layer4UV).rgb
                   * _FourthLayerDiffuseColor.rgb;
    layer.normalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_FourthLayerNormalMap, sampler_BumpMap, layer4UV), _FourthLayerNormalScale);
    
    half4 mahs = SAMPLE_TEXTURE2D(_FourthLayerMAHSMap, sampler_LinearRepeat, layer4UV);
    layer.metallic = mahs.r * _FourthLayerMetallicScale;
    layer.occlusion = LerpWhiteTo(mahs.g, _FourthLayerOcclusionScale);
    layer.smoothness = mahs.a * _FourthLayerSmoothnessScale;
    #endif
    
    return layer;
}

//=============================================================================
// Normal Blending Utilities
//=============================================================================
half3 BlendNormals_RNM(half3 n1, half3 n2)
{
    // Reoriented Normal Mapping blend
    n1.z += 1.0;
    n2.xy = -n2.xy;
    return n1 * dot(n1, n2) / n1.z - n2;
}

half3 BlendNormals_Linear(half3 n1, half3 n2, half blend)
{
    return normalize(lerp(n1, n2, blend));
}

//=============================================================================
// Clear Coat Sampling
//=============================================================================
half2 SampleClearCoat(float2 uv)
{
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half2 clearCoatMaskSmoothness = half2(_ClearCoatMask, _ClearCoatSmoothness);

#if defined(_CLEARCOATMAP)
    clearCoatMaskSmoothness *= SAMPLE_TEXTURE2D(_ClearCoatMap, sampler_ClearCoatMap, uv).rg;
#endif

    return clearCoatMaskSmoothness;
#else
    return half2(0.0, 1.0);
#endif
}

//=============================================================================
// SSS-specific helper functions
//=============================================================================

half SampleTransmissionMask(float2 uv)
{
#ifdef _TRANSMISSION_MASK_MAP
    half maskFromMap = SAMPLE_TEXTURE2D(_TransmissionMaskMap, sampler_TransmissionMaskMap, uv).r;
    return maskFromMap * _TransmissionMask;
#else
    return _TransmissionMask;
#endif
}

half SampleThickness(float2 uv)
{
#ifdef _THICKNESS_MAP
    half thicknessFromMap = SAMPLE_TEXTURE2D(_ThicknessMap, sampler_ThicknessMap, uv).r;
    return thicknessFromMap * _Thickness;
#else
    return _Thickness;
#endif
}

/*
// Debug 输出数据结构
struct MultiLayerSSSDebugData
{
    half layer1Mask;        // Base layer SSS mask
    half layer2Mask;        // Second layer mask (处理后)
    half layer3Mask;        // Third layer mask (处理后)
    half layer4Mask;        // Fourth layer mask (处理后)
    
    half layer1SSSIntensity;
    half layer2SSSIntensity;
    half layer3SSSIntensity;
    half layer4SSSIntensity;
    
    half combinedSSSMask;   // 最终混合的 SSS Mask
    uint dominantLayerIndex; // 主导层索引 (0-3)
    
    half4 layerWeights;     // 归一化后的各层权重
};

// 采样多层 SSS Debug 数据
MultiLayerSSSDebugData SampleMultiLayerSSSDebugData(float2 uv)
{
    MultiLayerSSSDebugData debugData = (MultiLayerSSSDebugData)0;
    
    // -------------------------------------------------------------------------
    // Layer 1: Base Layer
    // -------------------------------------------------------------------------
    debugData.layer1Mask = 1.0; // Base layer 默认覆盖整个表面
    debugData.layer1SSSIntensity = _BaseLayerSubsurfaceScattering;
    
    // -------------------------------------------------------------------------
    // Layer 2: Second Layer
    // -------------------------------------------------------------------------
#if defined(_USE_SECOND_LAYER)
    half layer2RawMask = SAMPLE_TEXTURE2D(_SecondLayerMaskMap, sampler_LinearClamp, uv).r;
    half secondMaskPow = clamp(10 - _SecondLayerRange, 0.1, 5.0);
    layer2RawMask = pow(saturate(layer2RawMask), secondMaskPow);
    half secondMaskSharp = saturate(0.5 - _SecondLayerPower);
    layer2RawMask = smoothstep(0.5 - secondMaskSharp, 0.5 + secondMaskSharp, layer2RawMask);
    debugData.layer2Mask = layer2RawMask * _SecondLayerIntensity;
    debugData.layer2SSSIntensity = _SecondLayerSubsurfaceScattering;
#else
    debugData.layer2Mask = 0;
    debugData.layer2SSSIntensity = 0;
#endif

    // -------------------------------------------------------------------------
    // Layer 3: Third Layer
    // -------------------------------------------------------------------------
#if defined(_USE_THIRD_LAYER)
    half layer3RawMask = SAMPLE_TEXTURE2D(_ThirdLayerMaskMap, sampler_LinearClamp, uv).r;
    half thirdMaskPow = clamp(10 - _ThirdLayerRange, 0.1, 5.0);
    layer3RawMask = pow(saturate(layer3RawMask), thirdMaskPow);
    half thirdMaskSharp = saturate(0.5 - _ThirdLayerPower);
    layer3RawMask = smoothstep(0.5 - thirdMaskSharp, 0.5 + thirdMaskSharp, layer3RawMask);
    debugData.layer3Mask = layer3RawMask * _ThirdLayerIntensity;
    debugData.layer3SSSIntensity = _ThirdLayerSubsurfaceScattering;
#else
    debugData.layer3Mask = 0;
    debugData.layer3SSSIntensity = 0;
#endif

    // -------------------------------------------------------------------------
    // Layer 4: Fourth Layer
    // -------------------------------------------------------------------------
#if defined(_USE_FOURTH_LAYER)
    half layer4RawMask = SAMPLE_TEXTURE2D(_FourthLayerMaskMap, sampler_LinearClamp, uv).r;
    half fourthMaskPow = clamp(10 - _FourthLayerRange, 0.1, 5.0);
    layer4RawMask = pow(saturate(layer4RawMask), fourthMaskPow);
    half fourthMaskSharp = saturate(0.5 - _FourthLayerPower);
    layer4RawMask = smoothstep(0.5 - fourthMaskSharp, 0.5 + fourthMaskSharp, layer4RawMask);
    debugData.layer4Mask = layer4RawMask * _FourthLayerIntensity;
    debugData.layer4SSSIntensity = _FourthLayerSubsurfaceScattering;
#else
    debugData.layer4Mask = 0;
    debugData.layer4SSSIntensity = 0;
#endif
    
    // 计算每层的有效贡献（考虑层级覆盖关系）
    half remainingWeight = 1.0;
    
    // Layer 4 覆盖
    half layer4Contribution = debugData.layer4Mask;
    remainingWeight = saturate(1.0 - layer4Contribution);
    
    // Layer 3 覆盖（在 Layer 4 之后）
    half layer3Contribution = debugData.layer3Mask * remainingWeight;
    remainingWeight = saturate(remainingWeight - layer3Contribution);
    
    // Layer 2 覆盖（在 Layer 3 之后）
    half layer2Contribution = debugData.layer2Mask * remainingWeight;
    remainingWeight = saturate(remainingWeight - layer2Contribution);
    
    // Layer 1 获得剩余权重
    half layer1Contribution = remainingWeight;
    
    // 归一化权重
    half totalContribution = layer1Contribution + layer2Contribution + layer3Contribution + layer4Contribution;
    totalContribution = max(totalContribution, 0.001);
    
    debugData.layerWeights = half4(
        layer1Contribution / totalContribution,
        layer2Contribution / totalContribution,
        layer3Contribution / totalContribution,
        layer4Contribution / totalContribution
    );
    
    // 计算混合后的 SSS Mask（与 InitializeStandardSSSLitSurfaceData 逻辑一致）
    half finalSSS = debugData.layer1SSSIntensity;
    
#if defined(_USE_SECOND_LAYER)
    finalSSS = lerp(finalSSS, debugData.layer2SSSIntensity, debugData.layer2Mask);
#endif

#if defined(_USE_THIRD_LAYER)
    finalSSS = lerp(finalSSS, debugData.layer3SSSIntensity, debugData.layer3Mask);
#endif

#if defined(_USE_FOURTH_LAYER)
    finalSSS = lerp(finalSSS, debugData.layer4SSSIntensity, debugData.layer4Mask);
#endif
    
    debugData.combinedSSSMask = finalSSS;
    
    // 找到主导层（贡献最大的层）
    debugData.dominantLayerIndex = 0;
    half maxWeight = debugData.layerWeights.x;
    
    if (debugData.layerWeights.y > maxWeight)
    {
        maxWeight = debugData.layerWeights.y;
        debugData.dominantLayerIndex = 1;
    }
    if (debugData.layerWeights.z > maxWeight)
    {
        maxWeight = debugData.layerWeights.z;
        debugData.dominantLayerIndex = 2;
    }
    if (debugData.layerWeights.w > maxWeight)
    {
        debugData.dominantLayerIndex = 3;
    }
    
    return debugData;
}

// Debug 颜色映射 - 将各层映射到不同颜色
half3 GetLayerDebugColor(uint layerIndex)
{
    // Layer 0 (Base): 蓝色
    // Layer 1 (Second): 绿色
    // Layer 2 (Third): 红色
    // Layer 3 (Fourth): 黄色
    const half3 layerColors[4] = {
        half3(0.2, 0.5, 1.0),   // Blue
        half3(0.2, 1.0, 0.3),   // Green
        half3(1.0, 0.3, 0.2),   // Red
        half3(1.0, 0.9, 0.2)    // Yellow
    };
    
    return layerColors[min(layerIndex, 3u)];
}

// 检查是否处于 Debug 模式
bool IsInSSSDebugMode()
{
#if defined(_SSSDEBUGMODE_LAYER1MASK) || defined(_SSSDEBUGMODE_LAYER2MASK) || defined(_SSSDEBUGMODE_LAYER3MASK) || defined(_SSSDEBUGMODE_LAYER4MASK) || defined(_SSSDEBUGMODE_COMBINEDSSSMASK) || defined(_SSSDEBUGMODE_LAYERWEIGHTS) || defined(_SSSDEBUGMODE_DOMINANTLAYER)
    return true;
#else
    return false;
#endif
}

// 生成 Debug 输出颜色
half3 GetMultiLayerSSSDebugOutput(MultiLayerSSSDebugData debugData)
{
    half3 debugColor = half3(0, 0, 0);
    
#if defined(_SSSDEBUGMODE_LAYER1MASK)
    // 显示 Base Layer 的 SSS 强度（因为 mask 始终为 1）
    debugColor = debugData.layer1SSSIntensity.xxx;
#elif defined(_SSSDEBUGMODE_LAYER2MASK)
    debugColor = debugData.layer2Mask.xxx;
#elif defined(_SSSDEBUGMODE_LAYER3MASK)
    debugColor = debugData.layer3Mask.xxx;
#elif defined(_SSSDEBUGMODE_LAYER4MASK)
    debugColor = debugData.layer4Mask.xxx;
#elif defined(_SSSDEBUGMODE_COMBINEDSSSMASK)
    debugColor = debugData.combinedSSSMask.xxx;
#elif defined(_SSSDEBUGMODE_LAYERWEIGHTS)
    // 使用颜色混合显示各层权重
    debugColor = 
        GetLayerDebugColor(0) * debugData.layerWeights.x +
        GetLayerDebugColor(1) * debugData.layerWeights.y +
        GetLayerDebugColor(2) * debugData.layerWeights.z +
        GetLayerDebugColor(3) * debugData.layerWeights.w;
#elif defined(_SSSDEBUGMODE_DOMINANTLAYER)
    // 显示主导层颜色
    debugColor = GetLayerDebugColor(debugData.dominantLayerIndex);
#endif
    
    return debugColor;
}

*/

#if defined(_USE_PARALLAX_DETAIL)

// 优化后的 Relief Mapping 函数：减少线性搜索步数，优化二分搜索
half2 ReliefMapping(
    TEXTURE2D_PARAM(heightMap, sampler_heightMap), 
    half2 inddx, 
    half2 inddy, 
    int channel,  // 0=R, 1=G, 2=B
    half2 uv, 
    half3 viewDirTS, 
    half2 offsetScale,  // x=unused, y=height scale
    half slicesMin, 
    half slicesMax, 
    half3 normalTS)
{
    // 优化：减少最大切片数，从 10-15 降低到 6-10
    int slicesNum = ceil(lerp(slicesMax, slicesMin, abs(viewDirTS.z)));
    float rcpSlices = rcp((float)slicesNum);
    float deltaHeight = rcpSlices;
    float safeDenominator = max(viewDirTS.z, 0.0001);
    float2 deltaUV = offsetScale.y * viewDirTS.xy * rcp(safeDenominator) * rcpSlices;
    
    // 根据通道采样高度
    float prevHeight = SAMPLE_TEXTURE2D_GRAD(heightMap, sampler_heightMap, uv, inddx, inddy)[channel];
    float2 currUVOffset = -deltaUV;
    float currHeight = SAMPLE_TEXTURE2D_GRAD(heightMap, sampler_heightMap, uv + currUVOffset, inddx, inddy)[channel];
    float rayHeight = 1.0 - deltaHeight;
    
    // Linear search
    [loop]
    for (int sliceIndex = 0; sliceIndex < slicesNum; sliceIndex++)
    {
        if (currHeight > rayHeight)
            break;
        prevHeight = currHeight;
        rayHeight -= deltaHeight;
        currUVOffset -= deltaUV;
        currHeight = SAMPLE_TEXTURE2D_GRAD(heightMap, sampler_heightMap, uv + currUVOffset, inddx, inddy)[channel];
    }
    
    // 优化：简化二分搜索，减少从3次到 2 次迭代（视觉差异可忽略）
    half2 halfDeltaUV = deltaUV * 0.5;
    half halfDeltaHeight = deltaHeight * 0.5;
    
    // Backward
    rayHeight += halfDeltaHeight;
    currUVOffset += halfDeltaUV;
    
    // Binary search refinement - 优化为 2 次迭代
    [unroll]
    for(int i = 0; i < 2; i++)
    {
        currHeight = SAMPLE_TEXTURE2D_GRAD(heightMap, sampler_heightMap, uv + currUVOffset, inddx, inddy)[channel];
        
        halfDeltaUV *= 0.5;
        halfDeltaHeight *= 0.5;
        
        // 优化：移除早期退出条件（abs+比较的开销 > 多一次迭代的开销）
        if(currHeight > rayHeight)
        {
            rayHeight += halfDeltaHeight;
            currUVOffset += halfDeltaUV;
        }
        else
        {
            rayHeight -= halfDeltaHeight;
            currUVOffset -= halfDeltaUV;
        }
    }
    
    return uv + currUVOffset; 
}

// 视差细节结果结构
struct ParallaxDetailResult
{
    half3 color;           // 最终混合后的颜色 (用于替换 albedo)
    half  mask;            // 视差遮罩 (用于 SSS 控制)
    half3 additiveColor;   // 可选：额外的自发光/加法颜色
};

// 单通道视差细节结果
struct ChannelDetailResult
{
    half3 color;           // 该通道的颜色
    half  mask;            // 该通道的遮罩强度
    half  layerWeight;     // 该通道在当前像素的权重
};

ChannelDetailResult CalculateDetailChannelResult(
    half2 uv,
    half3 viewDirTS,
    half3 normalTS,
    int channel,
    half heightScale,
    half tiling,
    half uvScale,
    half strength,
    half3 detailColor,
    half4 layerMask,          // x=Layer1, y=Layer2, z=Layer3, w=Layer4
    half layer1Weight,        // 这是计算后的 Layer1 有效权重
    half layer2Mask,          // 这是原始的 Layer2 mask 值
    half layer3Mask,          // 这是原始的 Layer3 mask 值
    half layer4Mask)          // 这是原始的 Layer4 mask 值
{
    ChannelDetailResult result;
    result.color = half3(0, 0, 0);
    result.mask = 0;
    result.layerWeight = 0;
    
    // 优化：使用向量点积替代多个分支条件判断
    half4 maskWeights = half4(layer1Weight, layer2Mask, layer3Mask, layer4Mask);
    // 将 layerMask 转换为 0/1 权重 (> 0.5 为 1，否则为 0)
    half4 layerFlags = step(half4(0.5, 0.5, 0.5, 0.5), layerMask);
    half totalWeight = dot(maskWeights, layerFlags);
    
    // 如果该通道在当前像素没有任何贡献，直接返回
    if (totalWeight < 0.001)
        return result;
    
    result.layerWeight = saturate(totalWeight);
    
    half2 inddx = ddx(uv);
    half2 inddy = ddy(uv);
    
    half2 parallaxUV = ReliefMapping(
        TEXTURE2D_ARGS(_ParallaxDetailTexture, sampler_ParallaxDetailTexture),
        inddx, inddy,
        channel,
        uv,
        viewDirTS,
        half2(0, heightScale),
        4, 8,  // 优化：降低切片数从 10,15 到 8,12
        normalTS
    );
    
    parallaxUV *= tiling;
    
    half3 reflectDir = reflect(-viewDirTS, normalTS);
    half2 refractUV = parallaxUV + normalTS.xy * uvScale + reflectDir.xy * _RefractRatio;
    
    half4 detailSample = SAMPLE_TEXTURE2D(_ParallaxDetailTexture, sampler_ParallaxDetailTexture, refractUV);
    half detailValue;
    
    if (channel == 0)
        detailValue = detailSample.r;
    else if (channel == 1)
        detailValue = detailSample.g;
    else
        detailValue = detailSample.b;
    
    result.mask = saturate(detailValue * strength * result.layerWeight);
    result.color = detailColor;
    
    return result;
}

// 修改 GetParallaxDetailResult 函数
ParallaxDetailResult GetParallaxDetailResult(
    half2 uv, 
    half3 viewDirTS, 
    half3 normalTS,
    half3 baseAlbedo,
    half layer2Mask,      // 原始 mask 值，不要重新计算
    half layer3Mask,
    half layer4Mask)
{
    ParallaxDetailResult result;
    result.color = baseAlbedo;
    result.mask = 0;
    result.additiveColor = half3(0, 0, 0);
    
    half layer1Weight = saturate(1.0 - layer2Mask - layer3Mask - layer4Mask);
    
    half3 accumulatedColor = baseAlbedo;
    half  accumulatedMask = 0;
    
    // Surface Detail (R Channel)
    ChannelDetailResult surfaceResult = CalculateDetailChannelResult(
        uv, viewDirTS, normalTS,
        0,
        _SurfaceDetailHeightScale,
        _SurfaceDetailTiling,
        _SurfaceDetailScale,
        _SurfaceDetailStrength,
        _SurfaceDetailColor.rgb,
        _SurfaceDetailLayerMask,
        layer1Weight,   // Layer1 的有效权重
        layer2Mask,     // Layer2 的原始 mask
        layer3Mask,     // Layer3 的原始 mask
        0      // Layer4 的原始 mask
    );
    
    if (surfaceResult.layerWeight > 0.001)
    {
        accumulatedColor = lerp(accumulatedColor, surfaceResult.color, surfaceResult.mask);
        accumulatedMask = max(accumulatedMask, surfaceResult.mask);
    }
    
    // Inner Detail (G Channel)
    ChannelDetailResult innerResult = CalculateDetailChannelResult(
        uv, viewDirTS, normalTS,
        1,
        _InnerDetailHeightScale,
        _InnerDetailTiling,
        _InnerDetailScale,
        _InnerDetailStrength,
        _InnerDetailColor.rgb,
        _InnerDetailLayerMask,
        layer1Weight,
        layer2Mask,
        layer3Mask,
        0
    );
    
    if (innerResult.layerWeight > 0.001)
    {
        accumulatedColor = lerp(accumulatedColor, innerResult.color, innerResult.mask);
        accumulatedMask = max(accumulatedMask, innerResult.mask);
    }
    
    // Third Detail (B Channel)
    ChannelDetailResult thirdResult = CalculateDetailChannelResult(
        uv, viewDirTS, normalTS,
        2,
        _ThirdDetailHeightScale,
        _ThirdDetailTiling,
        _ThirdDetailScale,
        _ThirdDetailStrength,
        _ThirdDetailColor.rgb,
        _ThirdDetailLayerMask,
        layer1Weight,
        layer2Mask,
        layer3Mask,
        0
    );
    
    if (thirdResult.layerWeight > 0.001)
    {
        accumulatedColor = lerp(accumulatedColor, thirdResult.color, thirdResult.mask);
        accumulatedMask = max(accumulatedMask, thirdResult.mask);
    }
    
    result.color = accumulatedColor;
    result.mask = accumulatedMask;
    
    return result;
}

// 优化：计算单个细节通道的颜色贡献
half3 CalculateDetailChannelColor(
    half2 uv,
    half3 viewDirTS,
    half3 normalTS,
    int channel,              // 0=R(Surface), 1=G(Inner), 2=B(Third)
    half heightScale,
    half tiling,
    half uvScale,
    half strength,
    half3 detailColor,
    half4 layerMask,          // x=Layer1, y=Layer2, z=Layer3, w=Layer4
    half layer1Weight,
    half layer2Mask,
    half layer3Mask,
    half layer4Mask)
{
    // 优化：使用向量点积替代多个分支条件
    half4 maskWeights = half4(layer1Weight, layer2Mask, layer3Mask, layer4Mask);
    half4 layerFlags = step(half4(0.5, 0.5, 0.5, 0.5), layerMask);
    half totalWeight = dot(maskWeights, layerFlags);
    
    if (totalWeight < 0.001)
        return half3(0, 0, 0);
    
    // 执行 Relief Mapping
    half2 inddx = ddx(uv);
    half2 inddy = ddy(uv);
    
    half2 parallaxUV = ReliefMapping(
        TEXTURE2D_ARGS(_ParallaxDetailTexture, sampler_ParallaxDetailTexture),
        inddx, inddy,
        channel,
        uv,
        viewDirTS,
        half2(0, heightScale),
        8, 12,  // 优化：降低切片数
        normalTS
    );
    
    parallaxUV *= tiling;
    
    half3 reflectDir = reflect(-viewDirTS, normalTS);
    half2 refractUV = parallaxUV + normalTS.xy * uvScale + reflectDir.xy * _RefractRatio;
    
    half4 detailSample = SAMPLE_TEXTURE2D(_ParallaxDetailTexture, sampler_ParallaxDetailTexture, refractUV);
    half detailValue;
    
    if (channel == 0)
        detailValue = detailSample.r;
    else if (channel == 1)
        detailValue = detailSample.g;
    else
        detailValue = detailSample.b;
    
    return detailValue * detailColor * strength * saturate(totalWeight);
}

// 优化：合并 ddx/ddy 计算，在调用 GetParallaxDetailColor 前预算
half3 GetParallaxDetailColor(
    half2 uv, 
    half3 viewDirTS, 
    half3 normalTS,
    half layer2Mask,
    half layer3Mask,
    half layer4Mask)
{
    half layer1Weight = saturate(1.0 - layer2Mask - layer3Mask - layer4Mask);
    
    half3 totalDetail = half3(0, 0, 0);
    
    // Surface Detail (R Channel)
    totalDetail += CalculateDetailChannelColor(
        uv, viewDirTS, normalTS,
        0,
        _SurfaceDetailHeightScale,
        _SurfaceDetailTiling,
        _SurfaceDetailScale,
        _SurfaceDetailStrength,
        _SurfaceDetailColor.rgb,
        _SurfaceDetailLayerMask,
        layer1Weight, layer2Mask, layer3Mask, layer4Mask
    );
    
    // Inner Detail (G Channel)
    totalDetail += CalculateDetailChannelColor(
        uv, viewDirTS, normalTS,
        1,
        _InnerDetailHeightScale,
        _InnerDetailTiling,
        _InnerDetailScale,
        _InnerDetailStrength,
        _InnerDetailColor.rgb,
        _InnerDetailLayerMask,
        layer1Weight, layer2Mask, layer3Mask, layer4Mask
    );
    
    // Third Detail (B Channel)
    totalDetail += CalculateDetailChannelColor(
        uv, viewDirTS, normalTS,
        2,
        _ThirdDetailHeightScale,
        _ThirdDetailTiling,
        _ThirdDetailScale,
        _ThirdDetailStrength,
        _ThirdDetailColor.rgb,
        _ThirdDetailLayerMask,
        layer1Weight, layer2Mask, layer3Mask, layer4Mask
    );
    
    return totalDetail;
}

#endif // _USE_PARALLAX_DETAIL

#endif // SSSLIT_INPUT_INCLUDED