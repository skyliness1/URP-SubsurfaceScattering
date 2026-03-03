using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;

namespace SoulRender
{
    /// <summary>
    /// Custom ShaderGUI for SSSLit shader with Diffusion Profile support
    /// </summary>
    public class SSSLitShaderGUI : BaseShaderGUI
    {
        public static new class Styles
        {
            // Subsurface Scattering section
            public static readonly GUIContent sssHeader = EditorGUIUtility.TrTextContent("次表面散射",
                "Configure subsurface scattering and transmission properties using a Diffusion Profile.");

            public static readonly GUIContent diffusionProfileText = EditorGUIUtility.TrTextContent("Diffusion Profile",
                "Specifies the Diffusion Profile asset that defines the scattering shape and transmission properties.");

            public static readonly GUIContent subsurfaceMaskText = EditorGUIUtility.TrTextContent("次表面散射遮罩",
                "Specifies the Subsurface mask map (R) for this Material and controls the overall strength of the subsurface scattering effect.");

            public static readonly GUIContent transmissionMaskText = EditorGUIUtility.TrTextContent("透射区域遮罩 | 当贴图槽为空时，可用作透射强度",
                "Specifies the Transmission mask map (R) for this Material and controls the overall strength of the transmission effect.");

            public static readonly GUIContent thicknessText = EditorGUIUtility.TrTextContent("厚度图",
                "Controls the strength of the Thickness Map, low values allow some light to transmit through the object.");

            // Advanced Lighting section
            public static readonly GUIContent advancedLightingHeader = EditorGUIUtility.TrTextContent("高级光照设置",
                "Configure advanced lighting options including Burley Diffuse and Dual Specular Lobe.");

            public static readonly GUIContent useBurleyDiffuseText = EditorGUIUtility.TrTextContent("Use Burley Diffuse",
                "When enabled, uses Disney's Burley diffuse model instead of Lambert.");

            public static readonly GUIContent useDualSpecularLobeText = EditorGUIUtility.TrTextContent("使用双叶高光",
                "When enabled, uses two specular lobes for more realistic skin/SSS specular highlights.");

            public static readonly GUIContent dualSpecularLobe0RoughnessText = EditorGUIUtility.TrTextContent("尖锐叶高光粗糙度倍数",
                "Roughness multiplier for the first (sharper) specular lobe.Default:  0.5");

            public static readonly GUIContent dualSpecularLobe1RoughnessText = EditorGUIUtility.TrTextContent("平滑叶高光粗糙度倍数",
                "Roughness multiplier for the second (broader) specular lobe.Default: 2.0");

            public static readonly GUIContent dualSpecularLobeMixText = EditorGUIUtility.TrTextContent("双叶高光混合系数",
                "Blend factor between the two specular lobes.Default: 0.15");

            // Multi-Layer section
            public static readonly GUIContent multiLayerHeader = EditorGUIUtility.TrTextContent("多层细节设置",
                "Configure additional material layers for complex surface effects.");

            public static readonly GUIContent useSecondLayerText = EditorGUIUtility.TrTextContent("启用第二层细节",
                "Enable the second material layer.");

            public static readonly GUIContent useThirdLayerText = EditorGUIUtility.TrTextContent("启用第三层细节",
                "Enable the third material layer.");
            
            public static readonly GUIContent useFourthLayerText = EditorGUIUtility.TrTextContent("启用第四层细节",
                "Enable the fourth material layer.");

            public static readonly GUIContent layerMaskMapText = EditorGUIUtility.TrTextContent("细节遮罩图",
                "Mask map (R channel) controlling where this layer is visible.");

            public static readonly GUIContent layerIntensityText = EditorGUIUtility.TrTextContent("细节强度",
                "Overall intensity of this layer.");

            public static readonly GUIContent layerDiffuseMapText = EditorGUIUtility.TrTextContent("基本色图",
                "Albedo/Diffuse texture for this layer.");

            public static readonly GUIContent layerDiffuseColorText = EditorGUIUtility.TrTextContent("基本色调整",
                "Tint color for this layer's diffuse.");

            public static readonly GUIContent layerNormalMapText = EditorGUIUtility.TrTextContent("法线贴图",
                "Normal map for this layer.");

            public static readonly GUIContent layerNormalScaleText = EditorGUIUtility.TrTextContent("法线缩放",
                "Normal map intensity for this layer.");

            public static readonly GUIContent layerMAHSMapText = EditorGUIUtility.TrTextContent("MAHS 图",
                "Combined map:  R=Metallic, G=AO, A=Smoothness");

            public static readonly GUIContent layerSmoothnessScaleText = EditorGUIUtility.TrTextContent("光滑度缩放",
                "Smoothness multiplier for this layer.");

            public static readonly GUIContent layerOcclusionScaleText = EditorGUIUtility.TrTextContent("遮蔽度缩放",
                "Occlusion strength for this layer.");

            public static readonly GUIContent overallHeader = EditorGUIUtility.TrTextContent("整体控制",
                "Overall Properties for materials.");

            public static readonly GUIContent layerMetallicScaleText = EditorGUIUtility.TrTextContent("金属度缩放",
                "Metallic multiplier for this layer.");

            public static readonly GUIContent colorAdjustmentText = EditorGUIUtility.TrTextContent("整体颜色调整",
                "Color multiplier applied to the final result.");

            public static readonly GUIContent overlayNormalMapText = EditorGUIUtility.TrTextContent("附加层法线贴图",
                "Additional normal map blended on top of the final normal.");

            public static readonly GUIContent overlayNormalScaleText = EditorGUIUtility.TrTextContent("附加层法线缩放",
                "Intensity of the overlay normal map.");

            public static readonly GUIContent overallSmoothnessScaleText = EditorGUIUtility.TrTextContent("整体光滑度缩放",
                "Final smoothness multiplier.");

            public static readonly GUIContent overallOcclusionScaleText = EditorGUIUtility.TrTextContent("整体遮蔽度缩放",
                "Final occlusion multiplier.");

            // Surface Inputs
            public static readonly GUIContent mahsMapText = EditorGUIUtility.TrTextContent("MAHS 图",
                "Combined map: R=Metallic, G=AO, A=Smoothness.  When enabled, replaces separate metallic and occlusion maps.");

            public static readonly GUIContent useMAHSMapText = EditorGUIUtility.TrTextContent("启用 MAHS 工作流",
                "Use combined MAHS map instead of separate maps.");

            public static readonly GUIContent metallicText = EditorGUIUtility.TrTextContent("金属度缩放",
                "Metallic value when not using MAHS map.");

            public static readonly GUIContent smoothnessText = EditorGUIUtility.TrTextContent("光滑度缩放",
                "Smoothness value.");

            public static readonly GUIContent occlusionStrengthText = EditorGUIUtility.TrTextContent("遮蔽度缩放",
                "Occlusion strength multiplier.");
            
            // Debug section
            public static readonly GUIContent debugModeText = EditorGUIUtility.TrTextContent("次表面散射 Debug模式",
                "Select which layer mask or data to visualize for debugging.");
            
            public static readonly string[] debugModeOptions = new string[]
            {
                "不启用",
                "基础层",
                "第二层细节遮罩",
                "第三层细节遮罩",
                "第四层细节遮罩",
                "整体SSS强度",
                "所有层遮罩",
                "所有层遮罩（锐化）"
            };
            
            public static readonly string debugModeHelp = 
                "Debug Mode Colors:\n" +
                "• 基础层: 蓝色\n" +
                "• 第二层: 绿色\n" +
                "• 第三层: 红色\n" +
                "• 第四层: 黄色";
            
            
            // Parallax Detail section
            public static readonly GUIContent parallaxDetailHeader = EditorGUIUtility.TrTextContent("视差细节设置",
                "Configure parallax/relief mapping detail effects for volumetric appearance.");

            public static readonly GUIContent useParallaxDetailText = EditorGUIUtility.TrTextContent("启用视差细节",
                "Enable parallax detail effects using relief mapping.");

            public static readonly GUIContent parallaxDetailTextureText = EditorGUIUtility.TrTextContent("视差细节图",
                "Parallax detail texture.  R=Surface, G=Inner, B=Third depth layer.");

            public static readonly GUIContent refractRatioText = EditorGUIUtility.TrTextContent("视差细节折射率",
                "Amount of refraction distortion based on view angle.");

            public static readonly GUIContent detailHeightScaleText = EditorGUIUtility.TrTextContent("视差细节深度",
                "Parallax depth/height scale for this detail layer.");

            public static readonly GUIContent detailTilingText = EditorGUIUtility.TrTextContent("视差细节 Tiling",
                "UV tiling for this detail layer.");

            public static readonly GUIContent detailUVScaleText = EditorGUIUtility.TrTextContent("视差细节扭曲",
                "Additional detail scale based on normal.");

            public static readonly GUIContent detailStrengthText = EditorGUIUtility.TrTextContent("视差细节强度",
                "Intensity/brightness of this detail layer.");

            public static readonly GUIContent detailColorText = EditorGUIUtility.TrTextContent("视差细节颜色",
                "Tint color for this detail layer.");

            public static readonly GUIContent detailLayerMaskText = EditorGUIUtility.TrTextContent("视差细节图层遮罩 | 显示在下方勾选的细节层上",
                "Select which material layers this detail channel appears on.");

            // Warnings
            public static readonly string diffusionProfileNotAssigned = "The Diffusion Profile on this material is not assigned.  Please assign a Diffusion Profile asset. ";
        }

        // Shader Property IDs
        public static class AdvancedLightingShaderIDs
        {
            public static readonly string UseBurleyDiffuse = "_UseBurleyDiffuse";
            public static readonly string UseDualSpecularLobe = "_UseDualSpecularLobe";
            public static readonly string DualSpecularLobe0Roughness = "_DualSpecularLobe0Roughness";
            public static readonly string DualSpecularLobe1Roughness = "_DualSpecularLobe1Roughness";
            public static readonly string DualSpecularLobeMix = "_DualSpecularLobeMix";
            public static readonly string BakedLightIntensity = "_BakedLightIntensity";
            public static readonly string GIDiffuseScale = "_GIDiffuseScale";
        }

        public static class MultiLayerShaderIDs
        {
            // Second Layer
            public static readonly string UseSecondLayer = "_UseSecondLayer";
            public static readonly string SecondLayerTilingOffset = "_SecondLayerTilingOffset";
            public static readonly string SecondLayerMaskMap = "_SecondLayerMaskMap";
            public static readonly string SecondLayerRange = "_SecondLayerRange";
            public static readonly string SecondLayerPower = "_SecondLayerPower";
            public static readonly string SecondLayerIntensity = "_SecondLayerIntensity";
            public static readonly string SecondLayerDiffuseMap = "_SecondLayerDiffuseMap";
            public static readonly string SecondLayerDiffuseColor = "_SecondLayerDiffuseColor";
            public static readonly string SecondLayerNormalMap = "_SecondLayerNormalMap";
            public static readonly string SecondLayerNormalScale = "_SecondLayerNormalScale";
            public static readonly string SecondLayerMAHSMap = "_SecondLayerMAHSMap";
            public static readonly string SecondLayerSmoothnessScale = "_SecondLayerSmoothnessScale";
            public static readonly string SecondLayerOcclusionScale = "_SecondLayerOcclusionScale";
            public static readonly string SecondLayerMetallicScale = "_SecondLayerMetallicScale";
            public static readonly string SecondLayerSubsurfaceScattering = "_SecondLayerSubsurfaceScattering";

            // Third Layer
            public static readonly string UseThirdLayer = "_UseThirdLayer";
            public static readonly string ThirdLayerTilingOffset = "_ThirdLayerTilingOffset";
            public static readonly string ThirdLayerMaskMap = "_ThirdLayerMaskMap";
            public static readonly string ThirdLayerRange = "_ThirdLayerRange";
            public static readonly string ThirdLayerPower = "_ThirdLayerPower";
            public static readonly string ThirdLayerIntensity = "_ThirdLayerIntensity";
            public static readonly string ThirdLayerDiffuseMap = "_ThirdLayerDiffuseMap";
            public static readonly string ThirdLayerDiffuseColor = "_ThirdLayerDiffuseColor";
            public static readonly string ThirdLayerNormalMap = "_ThirdLayerNormalMap";
            public static readonly string ThirdLayerNormalScale = "_ThirdLayerNormalScale";
            public static readonly string ThirdLayerMAHSMap = "_ThirdLayerMAHSMap";
            public static readonly string ThirdLayerSmoothnessScale = "_ThirdLayerSmoothnessScale";
            public static readonly string ThirdLayerOcclusionScale = "_ThirdLayerOcclusionScale";
            public static readonly string ThirdLayerMetallicScale = "_ThirdLayerMetallicScale";
            public static readonly string ThirdLayerSubsurfaceScattering = "_ThirdLayerSubsurfaceScattering";
            
            // Fourth Layer
            public static readonly string UseFourthLayer = "_UseFourthLayer";
            public static readonly string FourthLayerTilingOffset = "_FourthLayerTilingOffset";
            public static readonly string FourthLayerMaskMap = "_FourthLayerMaskMap";
            public static readonly string FourthLayerRange = "_FourthLayerRange";
            public static readonly string FourthLayerPower = "_FourthLayerPower";
            public static readonly string FourthLayerIntensity = "_FourthLayerIntensity";
            public static readonly string FourthLayerDiffuseMap = "_FourthLayerDiffuseMap";
            public static readonly string FourthLayerDiffuseColor = "_FourthLayerDiffuseColor";
            public static readonly string FourthLayerNormalMap = "_FourthLayerNormalMap";
            public static readonly string FourthLayerNormalScale = "_FourthLayerNormalScale";
            public static readonly string FourthLayerNormalBlendIntensity = "_FourthLayerNormalBlendIntensity";
            public static readonly string FourthLayerMAHSMap = "_FourthLayerMAHSMap";
            public static readonly string FourthLayerSmoothnessScale = "_FourthLayerSmoothnessScale";
            public static readonly string FourthLayerOcclusionScale = "_FourthLayerOcclusionScale";
            public static readonly string FourthLayerMetallicScale = "_FourthLayerMetallicScale";
            public static readonly string FourthLayerSubsurfaceScattering = "_FourthLayerSubsurfaceScattering";
            public static readonly string FourthLayerReflectionIntensity = "_FourthLayerReflectionIntensity";
            
            // Debug Mode
            public static readonly string SSSDebugMode = "_SSSDebugMode";
        }

        public static class VolumetricIceShaderIDs
        {
            public static readonly string OverallColorAdjustment = "_OverallColorAdjustment";
            public static readonly string OverlayNormalMap = "_OverlayNormalMap";
            public static readonly string OverlayTilingOffset = "_OverlayTilingOffset";
            public static readonly string OverlayNormalScale = "_OverlayNormalScale";
            public static readonly string OverallSmoothnessScale = "_OverallSmoothnessScale";
            public static readonly string OverallOcclusionScale = "_OverallOcclusionScale";
        }

        public static class SurfaceInputShaderIDs
        {
            public static readonly string UseMAHSMap = "_UseMAHSMap";
            public static readonly string MAHSMap = "_MAHSMap";
            public static readonly string Metallic = "_Metallic";
            public static readonly string Smoothness = "_Smoothness";
            public static readonly string OcclusionStrength = "_OcclusionStrength";
            public static readonly string baseLayerSubsurfaceScattering = "_BaseLayerSubsurfaceScattering";
        }
        
        public static class ParallaxDetailShaderIDs
        {
            public static readonly string UseParallaxDetail = "_UseParallaxDetail";
            public static readonly string ParallaxDetailTexture = "_ParallaxDetailTexture";
            public static readonly string RefractRatio = "_RefractRatio";
            public static readonly string ParallaxDetailScatteringRange = "_ParallaxDetailScatteringRange";
            
            // Surface Detail
            public static readonly string SurfaceDetailHeightScale = "_SurfaceDetailHeightScale";
            public static readonly string SurfaceDetailTiling = "_SurfaceDetailTiling";
            public static readonly string SurfaceDetailScale = "_SurfaceDetailScale";
            public static readonly string SurfaceDetailStrength = "_SurfaceDetailStrength";
            public static readonly string SurfaceDetailColor = "_SurfaceDetailColor";
            public static readonly string SurfaceDetailLayerMask = "_SurfaceDetailLayerMask";
            
            // Inner Detail
            public static readonly string InnerDetailHeightScale = "_InnerDetailHeightScale";
            public static readonly string InnerDetailTiling = "_InnerDetailTiling";
            public static readonly string InnerDetailScale = "_InnerDetailScale";
            public static readonly string InnerDetailStrength = "_InnerDetailStrength";
            public static readonly string InnerDetailColor = "_InnerDetailColor";
            public static readonly string InnerDetailLayerMask = "_InnerDetailLayerMask";
            
            // Third Detail
            public static readonly string ThirdDetailHeightScale = "_ThirdDetailHeightScale";
            public static readonly string ThirdDetailTiling = "_ThirdDetailTiling";
            public static readonly string ThirdDetailScale = "_ThirdDetailScale";
            public static readonly string ThirdDetailStrength = "_ThirdDetailStrength";
            public static readonly string ThirdDetailColor = "_ThirdDetailColor";
            public static readonly string ThirdDetailLayerMask = "_ThirdDetailLayerMask";
        }

        /// <summary>
        /// Container for SSS-specific properties. 
        /// </summary>
        public struct SSSProperties
        {
            public MaterialProperty diffusionProfileAsset;
            public MaterialProperty diffusionProfileHash;
            public MaterialProperty subsurfaceMask;
            public MaterialProperty subsurfaceMaskMap;
            public MaterialProperty transmissionMask;
            public MaterialProperty transmissionMaskMap;
            public MaterialProperty thickness;
            public MaterialProperty thicknessMap;
            public MaterialProperty thicknessRange;
            public MaterialProperty thicknessPower;

            public SSSProperties(MaterialProperty[] properties)
            {
                diffusionProfileAsset = BaseShaderGUI.FindProperty(SSSShaderIDs.DiffusionProfileAsset, properties, false);
                diffusionProfileHash = BaseShaderGUI.FindProperty(SSSShaderIDs.DiffusionProfileHash, properties, false);
                subsurfaceMask = BaseShaderGUI.FindProperty(SSSShaderIDs.SubsurfaceMask, properties, false);
                subsurfaceMaskMap = BaseShaderGUI.FindProperty(SSSShaderIDs.SubsurfaceMaskMap, properties, false);
                transmissionMask = BaseShaderGUI.FindProperty(SSSShaderIDs.TransmissionMask, properties, false);
                transmissionMaskMap = BaseShaderGUI.FindProperty(SSSShaderIDs.TransmissionMaskMap, properties, false);
                thickness = BaseShaderGUI.FindProperty(SSSShaderIDs.Thickness, properties, false);
                thicknessMap = BaseShaderGUI.FindProperty(SSSShaderIDs.ThicknessMap, properties, false);
                thicknessRange = BaseShaderGUI.FindProperty(SSSShaderIDs.ThicknessRange, properties, false);
                thicknessPower = BaseShaderGUI.FindProperty(SSSShaderIDs.ThicknessPower, properties, false);
            }
        }

        /// <summary>
        /// Container for Advanced Lighting properties.
        /// </summary>
        public struct AdvancedLightingProperties
        {
            public MaterialProperty useBurleyDiffuse;
            public MaterialProperty useDualSpecularLobe;
            public MaterialProperty dualSpecularLobe0Roughness;
            public MaterialProperty dualSpecularLobe1Roughness;
            public MaterialProperty dualSpecularLobeMix;
            public MaterialProperty bakedLightIntensity;
            public MaterialProperty giDiffuseScale;

            public AdvancedLightingProperties(MaterialProperty[] properties)
            {
                useBurleyDiffuse = BaseShaderGUI.FindProperty(AdvancedLightingShaderIDs.UseBurleyDiffuse, properties, false);
                useDualSpecularLobe = BaseShaderGUI.FindProperty(AdvancedLightingShaderIDs.UseDualSpecularLobe, properties, false);
                dualSpecularLobe0Roughness = BaseShaderGUI.FindProperty(AdvancedLightingShaderIDs.DualSpecularLobe0Roughness, properties, false);
                dualSpecularLobe1Roughness = BaseShaderGUI.FindProperty(AdvancedLightingShaderIDs.DualSpecularLobe1Roughness, properties, false);
                dualSpecularLobeMix = BaseShaderGUI.FindProperty(AdvancedLightingShaderIDs.DualSpecularLobeMix, properties, false);
                bakedLightIntensity = BaseShaderGUI.FindProperty(AdvancedLightingShaderIDs.BakedLightIntensity, properties, false);
                giDiffuseScale = BaseShaderGUI.FindProperty(AdvancedLightingShaderIDs.GIDiffuseScale, properties, false);
            }
        }

        /// <summary>
        /// Container for Multi-Layer properties. 
        /// </summary>
        public struct MultiLayerProperties
        {
            // Second Layer
            public MaterialProperty useSecondLayer;
            public MaterialProperty secondLayerTilingOffset;
            public MaterialProperty secondLayerMaskMap;
            public MaterialProperty secondLayerRange;
            public MaterialProperty secondLayerPower;
            public MaterialProperty secondLayerIntensity;
            public MaterialProperty secondLayerDiffuseMap;
            public MaterialProperty secondLayerDiffuseColor;
            public MaterialProperty secondLayerNormalMap;
            public MaterialProperty secondLayerNormalScale;
            public MaterialProperty secondLayerMAHSMap;
            public MaterialProperty secondLayerSmoothnessScale;
            public MaterialProperty secondLayerOcclusionScale;
            public MaterialProperty secondLayerMetallicScale;
            public MaterialProperty secondLayerSubsurfaceScattering;

            // Third Layer
            public MaterialProperty useThirdLayer;
            public MaterialProperty thirdLayerTilingOffset;
            public MaterialProperty thirdLayerMaskMap;
            public MaterialProperty thirdLayerRange;
            public MaterialProperty thirdLayerPower;
            public MaterialProperty thirdLayerIntensity;
            public MaterialProperty thirdLayerDiffuseMap;
            public MaterialProperty thirdLayerDiffuseColor;
            public MaterialProperty thirdLayerNormalMap;
            public MaterialProperty thirdLayerNormalScale;
            public MaterialProperty thirdLayerMAHSMap;
            public MaterialProperty thirdLayerSmoothnessScale;
            public MaterialProperty thirdLayerOcclusionScale;
            public MaterialProperty thirdLayerMetallicScale;
            public MaterialProperty thirdLayerSubsurfaceScattering;
            
            // Fourth Layer
            public MaterialProperty useFourthLayer;
            public MaterialProperty fourthLayerTilingOffset;
            public MaterialProperty fourthLayerMaskMap;
            public MaterialProperty fourthLayerRange;
            public MaterialProperty fourthLayerPower;
            public MaterialProperty fourthLayerIntensity;
            public MaterialProperty fourthLayerDiffuseMap;
            public MaterialProperty fourthLayerDiffuseColor;
            public MaterialProperty fourthLayerNormalMap;
            public MaterialProperty fourthLayerNormalScale;
            public MaterialProperty fourthLayerNormalBlendIntensity;
            public MaterialProperty fourthLayerMAHSMap;
            public MaterialProperty fourthLayerSmoothnessScale;
            public MaterialProperty fourthLayerOcclusionScale;
            public MaterialProperty fourthLayerMetallicScale;
            public MaterialProperty fourthLayerSubsurfaceScattering;
            public MaterialProperty fourthLayerReflectionIntensity;
            
            public MaterialProperty sssDebugMode;

            public MultiLayerProperties(MaterialProperty[] properties)
            {
                // Second Layer
                useSecondLayer = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.UseSecondLayer, properties, false);
                secondLayerTilingOffset = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerTilingOffset, properties, false);
                secondLayerMaskMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerMaskMap, properties, false);
                secondLayerRange = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerRange, properties, false);
                secondLayerPower = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerPower, properties, false);
                secondLayerIntensity = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerIntensity, properties, false);
                secondLayerDiffuseMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerDiffuseMap, properties, false);
                secondLayerDiffuseColor = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerDiffuseColor, properties, false);
                secondLayerNormalMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerNormalMap, properties, false);
                secondLayerNormalScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerNormalScale, properties, false);
                secondLayerMAHSMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerMAHSMap, properties, false);
                secondLayerSmoothnessScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerSmoothnessScale, properties, false);
                secondLayerOcclusionScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerOcclusionScale, properties, false);
                secondLayerMetallicScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerMetallicScale, properties, false);
                secondLayerSubsurfaceScattering = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SecondLayerSubsurfaceScattering, properties, false);

                // Third Layer
                useThirdLayer = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.UseThirdLayer, properties, false);
                thirdLayerTilingOffset = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerTilingOffset, properties, false);
                thirdLayerMaskMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerMaskMap, properties, false);
                thirdLayerRange = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerRange, properties, false);
                thirdLayerPower = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerPower, properties, false);
                thirdLayerIntensity = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerIntensity, properties, false);
                thirdLayerDiffuseMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerDiffuseMap, properties, false);
                thirdLayerDiffuseColor = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerDiffuseColor, properties, false);
                thirdLayerNormalMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerNormalMap, properties, false);
                thirdLayerNormalScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerNormalScale, properties, false);
                thirdLayerMAHSMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerMAHSMap, properties, false);
                thirdLayerSmoothnessScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerSmoothnessScale, properties, false);
                thirdLayerOcclusionScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerOcclusionScale, properties, false);
                thirdLayerMetallicScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerMetallicScale, properties, false);
                thirdLayerSubsurfaceScattering = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.ThirdLayerSubsurfaceScattering, properties, false);
                
                // Fourth Layer
                useFourthLayer = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.UseFourthLayer, properties, false);
                fourthLayerTilingOffset = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerTilingOffset, properties, false);
                fourthLayerMaskMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerMaskMap, properties, false);
                fourthLayerRange = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerRange, properties, false);
                fourthLayerPower = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerPower, properties, false);
                fourthLayerIntensity = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerIntensity, properties, false);
                fourthLayerDiffuseMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerDiffuseMap, properties, false);
                fourthLayerDiffuseColor = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerDiffuseColor, properties, false);
                fourthLayerNormalMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerNormalMap, properties, false);
                fourthLayerNormalScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerNormalScale, properties, false);
                fourthLayerNormalBlendIntensity = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerNormalBlendIntensity, properties, false);
                fourthLayerMAHSMap = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerMAHSMap, properties, false);
                fourthLayerSmoothnessScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerSmoothnessScale, properties, false);
                fourthLayerOcclusionScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerOcclusionScale, properties, false);
                fourthLayerMetallicScale = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerMetallicScale, properties, false);
                fourthLayerSubsurfaceScattering = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerSubsurfaceScattering, properties, false);
                fourthLayerReflectionIntensity = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.FourthLayerReflectionIntensity, properties, false);
                
                sssDebugMode = BaseShaderGUI.FindProperty(MultiLayerShaderIDs.SSSDebugMode, properties, false);
            }
        }
        
        public struct ParallaxDetailProperties
        {
            public MaterialProperty useParallaxDetail;
            public MaterialProperty parallaxDetailTexture;
            public MaterialProperty refractRatio;
            public MaterialProperty parallaxDetailScatteringRange;
            
            // Surface Detail
            public MaterialProperty surfaceDetailHeightScale;
            public MaterialProperty surfaceDetailTiling;
            public MaterialProperty surfaceDetailScale;
            public MaterialProperty surfaceDetailStrength;
            public MaterialProperty surfaceDetailColor;
            public MaterialProperty surfaceDetailLayerMask;
            
            // Inner Detail
            public MaterialProperty innerDetailHeightScale;
            public MaterialProperty innerDetailTiling;
            public MaterialProperty innerDetailScale;
            public MaterialProperty innerDetailStrength;
            public MaterialProperty innerDetailColor;
            public MaterialProperty innerDetailLayerMask;
            
            // Third Detail
            public MaterialProperty thirdDetailHeightScale;
            public MaterialProperty thirdDetailTiling;
            public MaterialProperty thirdDetailScale;
            public MaterialProperty thirdDetailStrength;
            public MaterialProperty thirdDetailColor;
            public MaterialProperty thirdDetailLayerMask;

            public ParallaxDetailProperties(MaterialProperty[] properties)
            {
                useParallaxDetail = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.UseParallaxDetail, properties, false);
                parallaxDetailTexture = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.ParallaxDetailTexture, properties, false);
                refractRatio = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.RefractRatio, properties, false);
                parallaxDetailScatteringRange = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.ParallaxDetailScatteringRange, properties, false);
                
                surfaceDetailHeightScale = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.SurfaceDetailHeightScale, properties, false);
                surfaceDetailTiling = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.SurfaceDetailTiling, properties, false);
                surfaceDetailScale = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.SurfaceDetailScale, properties, false);
                surfaceDetailStrength = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.SurfaceDetailStrength, properties, false);
                surfaceDetailColor = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.SurfaceDetailColor, properties, false);
                surfaceDetailLayerMask = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.SurfaceDetailLayerMask, properties, false);
                
                innerDetailHeightScale = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.InnerDetailHeightScale, properties, false);
                innerDetailTiling = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.InnerDetailTiling, properties, false);
                innerDetailScale = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.InnerDetailScale, properties, false);
                innerDetailStrength = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.InnerDetailStrength, properties, false);
                innerDetailColor = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.InnerDetailColor, properties, false);
                innerDetailLayerMask = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.InnerDetailLayerMask, properties, false);
                
                thirdDetailHeightScale = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.ThirdDetailHeightScale, properties, false);
                thirdDetailTiling = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.ThirdDetailTiling, properties, false);
                thirdDetailScale = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.ThirdDetailScale, properties, false);
                thirdDetailStrength = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.ThirdDetailStrength, properties, false);
                thirdDetailColor = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.ThirdDetailColor, properties, false);
                thirdDetailLayerMask = BaseShaderGUI.FindProperty(ParallaxDetailShaderIDs.ThirdDetailLayerMask, properties, false);
            }
        }

        /// <summary>
        /// Container for Volumetric Ice properties. 
        /// </summary>
        public struct OverallProperties
        {
            public MaterialProperty colorAdjustment;
            public MaterialProperty overlayNormalMap;
            public MaterialProperty overlayTilingOffset;
            public MaterialProperty overlayNormalScale;
            public MaterialProperty overallSmoothnessScale;
            public MaterialProperty overallOcclusionScale;

            public OverallProperties(MaterialProperty[] properties)
            {
                colorAdjustment = BaseShaderGUI.FindProperty(VolumetricIceShaderIDs.OverallColorAdjustment, properties, false);
                overlayNormalMap = BaseShaderGUI.FindProperty(VolumetricIceShaderIDs.OverlayNormalMap, properties, false);
                overlayTilingOffset = BaseShaderGUI.FindProperty(VolumetricIceShaderIDs.OverlayTilingOffset, properties, false);
                overlayNormalScale = BaseShaderGUI.FindProperty(VolumetricIceShaderIDs.OverlayNormalScale, properties, false);
                overallSmoothnessScale = BaseShaderGUI.FindProperty(VolumetricIceShaderIDs.OverallSmoothnessScale, properties, false);
                overallOcclusionScale = BaseShaderGUI.FindProperty(VolumetricIceShaderIDs.OverallOcclusionScale, properties, false);
            }
        }

        /// <summary>
        /// Container for Surface Input properties.
        /// </summary>
        public struct SurfaceInputProperties
        {
            public MaterialProperty useMAHSMap;
            public MaterialProperty mahsMap;
            public MaterialProperty metallic;
            public MaterialProperty smoothness;
            public MaterialProperty occlusionStrength;
            public MaterialProperty baseLayerSubsurfaceScattering;

            public SurfaceInputProperties(MaterialProperty[] properties)
            {
                useMAHSMap = BaseShaderGUI.FindProperty(SurfaceInputShaderIDs.UseMAHSMap, properties, false);
                mahsMap = BaseShaderGUI.FindProperty(SurfaceInputShaderIDs.MAHSMap, properties, false);
                metallic = BaseShaderGUI.FindProperty(SurfaceInputShaderIDs.Metallic, properties, false);
                smoothness = BaseShaderGUI.FindProperty(SurfaceInputShaderIDs.Smoothness, properties, false);
                occlusionStrength = BaseShaderGUI.FindProperty(SurfaceInputShaderIDs.OcclusionStrength, properties, false);
                baseLayerSubsurfaceScattering = BaseShaderGUI.FindProperty(SurfaceInputShaderIDs.baseLayerSubsurfaceScattering, properties, false);
            }
        }

        // Properties
        private SSSLitGUI.LitProperties litProperties;
        private SSSProperties sssProperties;
        private AdvancedLightingProperties advancedLightingProperties;
        private MultiLayerProperties multiLayerProperties;
        private OverallProperties overallProperties;
        private SurfaceInputProperties surfaceInputProperties;
        private ParallaxDetailProperties parallaxDetailProperties;

        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);
            litProperties = new SSSLitGUI.LitProperties(properties);
            sssProperties = new SSSProperties(properties);
            advancedLightingProperties = new AdvancedLightingProperties(properties);
            multiLayerProperties = new MultiLayerProperties(properties);
            overallProperties = new OverallProperties(properties);
            surfaceInputProperties = new SurfaceInputProperties(properties);
            parallaxDetailProperties = new ParallaxDetailProperties(properties);
        }

        public override void DrawSurfaceOptions(Material material)
        {
            base.DrawSurfaceOptions(material);
        }

        public override void DrawSurfaceInputs(Material material)
        {
            base.DrawSurfaceInputs(material);
            
            // Draw MAHS Map section
            DrawMAHSMapSection(material);
            
            // Draw normal map
            BaseShaderGUI.DrawNormalArea(materialEditor, litProperties.bumpMapProp, litProperties.bumpScaleProp);
            
            DrawEmissionProperties(material, true);
            DrawTileOffset(materialEditor, baseMapProp);
        }

        private void DrawMAHSMapSection(Material material)
        {
            if (surfaceInputProperties.useMAHSMap != null)
            {
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(surfaceInputProperties.useMAHSMap, Styles.useMAHSMapText);
                if (EditorGUI.EndChangeCheck())
                {
                    SetMAHSKeyword(material);
                }

                bool useMAHS = surfaceInputProperties.useMAHSMap.floatValue > 0.5f;

                if (useMAHS)
                {
                    if (surfaceInputProperties.mahsMap != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.mahsMapText, surfaceInputProperties.mahsMap);
                    }
                }

                // Always show these controls
                if (surfaceInputProperties.metallic != null)
                {
                    materialEditor.ShaderProperty(surfaceInputProperties.metallic, Styles.metallicText);
                }
                if (surfaceInputProperties.smoothness != null)
                {
                    materialEditor.ShaderProperty(surfaceInputProperties.smoothness, Styles.smoothnessText);
                }
                if (surfaceInputProperties.occlusionStrength != null)
                {
                    materialEditor.ShaderProperty(surfaceInputProperties.occlusionStrength, Styles.occlusionStrengthText);
                }

                if (surfaceInputProperties.baseLayerSubsurfaceScattering != null)
                {
                    materialEditor.ShaderProperty(surfaceInputProperties.baseLayerSubsurfaceScattering, new GUIContent("基础层次表面散射强度"));
                }
            }
        }

        public override void DrawAdvancedOptions(Material material)
        {
            base.DrawAdvancedOptions(material);
        }

        public override void FillAdditionalFoldouts(MaterialHeaderScopeList materialScopesList)
        {
            materialScopesList.RegisterHeaderScope(Styles.sssHeader, (uint)Expandable.Details + 1, DrawSSSInputs);
            materialScopesList.RegisterHeaderScope(Styles.advancedLightingHeader, (uint)Expandable.Details + 2, DrawAdvancedLightingInputs);
            materialScopesList.RegisterHeaderScope(Styles.multiLayerHeader, (uint)Expandable.Details + 3, DrawMultiLayerInputs);
            materialScopesList.RegisterHeaderScope(Styles.overallHeader, (uint)Expandable.Details + 4, DrawVolumetricIceInputs);
            materialScopesList.RegisterHeaderScope(Styles.parallaxDetailHeader, (uint)Expandable.Details + 5, DrawParallaxDetailInputs);
        }

        /// <summary>
        /// Draws the Subsurface Scattering section
        /// </summary>
        public void DrawSSSInputs(Material material)
        {
            if (sssProperties.diffusionProfileHash == null || sssProperties.diffusionProfileAsset == null)
            {
                return;
            }

            string guid = ConvertVector4ToGUID(sssProperties.diffusionProfileAsset.vectorValue);
            DiffusionProfileSettings profile = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(
                AssetDatabase.GUIDToAssetPath(guid)
            );

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = sssProperties.diffusionProfileAsset.hasMixedValue;
            
            var newProfile = (DiffusionProfileSettings)EditorGUILayout.ObjectField(
                Styles.diffusionProfileText,
                profile,
                typeof(DiffusionProfileSettings),
                false
            );
            
            EditorGUI.showMixedValue = false;

            if (EditorGUI.EndChangeCheck())
            {
                Vector4 newGuid = Vector4.zero;
                float hash = 0;

                if (newProfile != null)
                {
                    if (newProfile.profile == null)
                    {
                        newProfile.profile = new DiffusionProfile(true);
                    }
                    newProfile.profile.Validate();
                    newProfile.UpdateCache();

                    string assetPath = AssetDatabase.GetAssetPath(newProfile);
                    guid = AssetDatabase.AssetPathToGUID(assetPath);
                    newGuid = ConvertGUIDToVector4(guid);
                    hash = Asfloat(newProfile.profile.hash);
                }

                sssProperties.diffusionProfileAsset.vectorValue = newGuid;
                sssProperties.diffusionProfileHash.floatValue = hash;
                
                EditorUtility.SetDirty(material);
                materialEditor.Repaint();
            }

            DrawDiffusionProfileWarning(newProfile ??  profile);

            if (sssProperties.subsurfaceMask != null && sssProperties.subsurfaceMaskMap != null)
            {
                materialEditor.TexturePropertySingleLine(Styles.subsurfaceMaskText, sssProperties.subsurfaceMaskMap, sssProperties.subsurfaceMask);
            }
            
            if (sssProperties.transmissionMask != null && sssProperties.transmissionMaskMap != null)
            {
                materialEditor.TexturePropertySingleLine(Styles.transmissionMaskText, sssProperties.transmissionMaskMap, sssProperties.transmissionMask);
            }
            
            if (sssProperties.thickness != null && sssProperties.thicknessMap != null)
            {
                materialEditor.TexturePropertySingleLine(Styles.thicknessText, sssProperties.thicknessMap, sssProperties.thickness);
            }
            if (sssProperties.thicknessRange != null)
            {
                materialEditor.ShaderProperty(sssProperties.thicknessRange, new GUIContent("厚度范围调整"));
            }
            if (sssProperties.thicknessPower != null)
            {
                materialEditor.ShaderProperty(sssProperties.thicknessPower, new GUIContent("厚度边缘软硬调整"));
            }
        }

        /// <summary>
        /// Draws the Advanced Lighting section
        /// </summary>
        public void DrawAdvancedLightingInputs(Material material)
        {
            // Burley Diffuse Toggle
            if (advancedLightingProperties.useBurleyDiffuse != null)
            {
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(advancedLightingProperties.useBurleyDiffuse, Styles.useBurleyDiffuseText);
                if (EditorGUI.EndChangeCheck())
                {
                    SetAdvancedLightingKeywords(material);
                }
            }

            EditorGUILayout.Space(5);

            // Dual Specular Lobe Section
            if (advancedLightingProperties.useDualSpecularLobe != null)
            {
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(advancedLightingProperties.useDualSpecularLobe, Styles.useDualSpecularLobeText);
                if (EditorGUI.EndChangeCheck())
                {
                    SetAdvancedLightingKeywords(material);
                }

                bool dualSpecularEnabled = advancedLightingProperties.useDualSpecularLobe.floatValue > 0.5f;
                
                EditorGUI.BeginDisabledGroup(!dualSpecularEnabled);
                {
                    EditorGUI.indentLevel++;
                    
                    if (advancedLightingProperties.dualSpecularLobe0Roughness != null)
                    {
                        materialEditor.ShaderProperty(advancedLightingProperties.dualSpecularLobe0Roughness, Styles.dualSpecularLobe0RoughnessText);
                    }
                    
                    if (advancedLightingProperties.dualSpecularLobe1Roughness != null)
                    {
                        materialEditor.ShaderProperty(advancedLightingProperties.dualSpecularLobe1Roughness, Styles.dualSpecularLobe1RoughnessText);
                    }
                    
                    if (advancedLightingProperties.dualSpecularLobeMix != null)
                    {
                        materialEditor.ShaderProperty(advancedLightingProperties.dualSpecularLobeMix, Styles.dualSpecularLobeMixText);
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();

                if (advancedLightingProperties.giDiffuseScale != null)
                {
                    materialEditor.ShaderProperty(advancedLightingProperties.giDiffuseScale, new GUIContent("GI漫反射强度"));
                }

                EditorGUILayout.Space(2);
                
                if (advancedLightingProperties.bakedLightIntensity != null)
                {
                    materialEditor.ShaderProperty(advancedLightingProperties.bakedLightIntensity, new GUIContent("烘焙光照强度"));
                }

                if (dualSpecularEnabled)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.HelpBox(
                        "Dual Specular Lobe creates two specular highlights:\n" +
                        "• Lobe 0: Sharper highlight (lower roughness)\n" +
                        "• Lobe 1: Broader highlight (higher roughness)\n" +
                        "This simulates the complex specular response of skin and other SSS materials.",
                        MessageType.Info);
                }
            }
        }

        /// <summary>
        /// Draws the Multi-Layer section
        /// </summary>
        public void DrawMultiLayerInputs(Material material)
        {

            // Second Layer
            EditorGUILayout.LabelField("Second Layer", EditorStyles.boldLabel);
            
            if (multiLayerProperties.useSecondLayer != null)
            {
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(multiLayerProperties.useSecondLayer, Styles.useSecondLayerText);
                if (EditorGUI.EndChangeCheck())
                {
                    SetMultiLayerKeywords(material);
                }

                bool secondLayerEnabled = multiLayerProperties.useSecondLayer.floatValue > 0.5f;
                
                EditorGUI.BeginDisabledGroup(!secondLayerEnabled);
                {
                    EditorGUI.indentLevel++;
                    
                    if (multiLayerProperties.secondLayerMaskMap != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerMaskMapText, multiLayerProperties.secondLayerMaskMap);
                    }

                    if (multiLayerProperties.secondLayerRange != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.secondLayerRange, new GUIContent("第二层遮罩范围"));
                    }
                    
                    if (multiLayerProperties.secondLayerPower != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.secondLayerPower, new GUIContent("第二层遮罩边缘软硬"));
                    }

                    if (multiLayerProperties.secondLayerIntensity != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.secondLayerIntensity, Styles.layerIntensityText);
                    }
                    
                    EditorGUILayout.Space(2);

                    if (multiLayerProperties.secondLayerDiffuseMap != null && multiLayerProperties.secondLayerDiffuseColor != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerDiffuseMapText, multiLayerProperties.secondLayerDiffuseMap, multiLayerProperties.secondLayerDiffuseColor);
                    }

                    if (multiLayerProperties.secondLayerNormalMap != null && multiLayerProperties.secondLayerNormalScale != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerNormalMapText, multiLayerProperties.secondLayerNormalMap, multiLayerProperties.secondLayerNormalScale);
                    }
                    
                    EditorGUILayout.Space(2);

                    if (multiLayerProperties.secondLayerMAHSMap != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerMAHSMapText, multiLayerProperties.secondLayerMAHSMap);
                    }

                    if (multiLayerProperties.secondLayerMetallicScale != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.secondLayerMetallicScale, Styles.layerMetallicScaleText);
                    }

                    if (multiLayerProperties.secondLayerOcclusionScale != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.secondLayerOcclusionScale, Styles.layerOcclusionScaleText);
                    }

                    if (multiLayerProperties.secondLayerSmoothnessScale != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.secondLayerSmoothnessScale, Styles.layerSmoothnessScaleText);
                    }
                    
                    EditorGUILayout.Space(2);
                    
                    if (multiLayerProperties.secondLayerSubsurfaceScattering != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.secondLayerSubsurfaceScattering, new GUIContent("第二层表面散射强度"));
                    }
                    
                    EditorGUILayout.Space(2);
                    
                    if(multiLayerProperties.secondLayerTilingOffset != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.secondLayerTilingOffset, new GUIContent("第二层细节 Tiling Offset"));
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(10);

            // Third Layer
            EditorGUILayout.LabelField("Third Layer", EditorStyles.boldLabel);
            
            if (multiLayerProperties.useThirdLayer != null)
            {
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(multiLayerProperties.useThirdLayer, Styles.useThirdLayerText);
                if (EditorGUI.EndChangeCheck())
                {
                    SetMultiLayerKeywords(material);
                }

                bool thirdLayerEnabled = multiLayerProperties.useThirdLayer.floatValue > 0.5f;
                
                EditorGUI.BeginDisabledGroup(!thirdLayerEnabled);
                {
                    EditorGUI.indentLevel++;
                    if (multiLayerProperties.thirdLayerMaskMap != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerMaskMapText, multiLayerProperties.thirdLayerMaskMap);
                    }
                    
                    if (multiLayerProperties.thirdLayerRange != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.thirdLayerRange, new GUIContent("第三层遮罩范围"));
                    }
                    
                    if (multiLayerProperties.thirdLayerPower != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.thirdLayerPower, new GUIContent("第三层遮罩边缘软硬"));
                    }

                    if (multiLayerProperties.thirdLayerIntensity != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.thirdLayerIntensity, Styles.layerIntensityText);
                    }
                    
                    EditorGUILayout.Space(2);

                    if (multiLayerProperties.thirdLayerDiffuseMap != null && multiLayerProperties.thirdLayerDiffuseColor != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerDiffuseMapText, multiLayerProperties.thirdLayerDiffuseMap, multiLayerProperties.thirdLayerDiffuseColor);
                    }

                    if (multiLayerProperties.thirdLayerNormalMap != null && multiLayerProperties.thirdLayerNormalScale != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerNormalMapText, multiLayerProperties.thirdLayerNormalMap, multiLayerProperties.thirdLayerNormalScale);
                    }
                    
                    EditorGUILayout.Space(2);

                    if (multiLayerProperties.thirdLayerMAHSMap != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerMAHSMapText, multiLayerProperties.thirdLayerMAHSMap);
                    }

                    if (multiLayerProperties.thirdLayerMetallicScale != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.thirdLayerMetallicScale, Styles.layerMetallicScaleText);
                    }

                    if (multiLayerProperties.thirdLayerOcclusionScale != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.thirdLayerOcclusionScale, Styles.layerOcclusionScaleText);
                    }

                    if (multiLayerProperties.thirdLayerSmoothnessScale != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.thirdLayerSmoothnessScale, Styles.layerSmoothnessScaleText);
                    }
                    
                    EditorGUILayout.Space(2);
                    
                    if (multiLayerProperties.thirdLayerSubsurfaceScattering != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.thirdLayerSubsurfaceScattering, new GUIContent("第三层表面散射强度"));
                    }
                    
                    EditorGUILayout.Space(2);
                    
                    if (multiLayerProperties.thirdLayerTilingOffset != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.thirdLayerTilingOffset, new GUIContent("第三层细节 Tiling Offset"));
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();
            }
            
            EditorGUILayout.Space(10);
            
            // Fourth Layer
            EditorGUILayout.LabelField("Fourth Layer", EditorStyles.boldLabel);
            
            if (multiLayerProperties.useFourthLayer != null)
            {
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(multiLayerProperties.useFourthLayer, Styles.useFourthLayerText);
                if (EditorGUI.EndChangeCheck())
                {
                    SetMultiLayerKeywords(material);
                }

                bool fourthLayerEnabled = multiLayerProperties.useFourthLayer.floatValue > 0.5f;
                
                EditorGUI.BeginDisabledGroup(!fourthLayerEnabled);
                {
                    EditorGUI.indentLevel++;
                    
                    if (multiLayerProperties.fourthLayerMaskMap != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerMaskMapText, multiLayerProperties.fourthLayerMaskMap);
                    }
                    
                    if (multiLayerProperties.fourthLayerRange != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerRange, new GUIContent("第四层遮罩范围"));
                    }
                    
                    if (multiLayerProperties.fourthLayerPower != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerPower, new GUIContent("第四层遮罩边缘软硬"));
                    }

                    if (multiLayerProperties.fourthLayerIntensity != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerIntensity, Styles.layerIntensityText);
                    }
                    
                    EditorGUILayout.Space(2);

                    if (multiLayerProperties.fourthLayerDiffuseMap != null && multiLayerProperties.fourthLayerDiffuseColor != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerDiffuseMapText, multiLayerProperties.fourthLayerDiffuseMap, multiLayerProperties.fourthLayerDiffuseColor);
                    }

                    if (multiLayerProperties.fourthLayerNormalMap != null && multiLayerProperties.fourthLayerNormalScale != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerNormalMapText, multiLayerProperties.fourthLayerNormalMap, multiLayerProperties.fourthLayerNormalScale);
                    }

                    if (multiLayerProperties.fourthLayerNormalBlendIntensity != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerNormalBlendIntensity, new GUIContent("第四层法线混合强度"));
                    }
                    
                    EditorGUILayout.Space(2);

                    if (multiLayerProperties.fourthLayerMAHSMap != null)
                    {
                        materialEditor.TexturePropertySingleLine(Styles.layerMAHSMapText, multiLayerProperties.fourthLayerMAHSMap);
                    }

                    if (multiLayerProperties.fourthLayerMetallicScale != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerMetallicScale, Styles.layerMetallicScaleText);
                    }

                    if (multiLayerProperties.fourthLayerOcclusionScale != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerOcclusionScale, Styles.layerOcclusionScaleText);
                    }

                    if (multiLayerProperties.fourthLayerSmoothnessScale != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerSmoothnessScale, Styles.layerSmoothnessScaleText);
                    }
                    
                    EditorGUILayout.Space(2);
                    
                    if (multiLayerProperties.fourthLayerSubsurfaceScattering != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerSubsurfaceScattering, new GUIContent("第四层表面散射强度"));
                    }
                    
                    if (multiLayerProperties.fourthLayerReflectionIntensity != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerReflectionIntensity, new GUIContent("第四层反射强度"));
                    }
                    
                    EditorGUILayout.Space(2);
                    
                    if (multiLayerProperties.fourthLayerTilingOffset != null)
                    {
                        materialEditor.ShaderProperty(multiLayerProperties.fourthLayerTilingOffset, new GUIContent("第四层细节 Tiling Offset"));
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();
            }
            
            EditorGUILayout.Space(15);
            
            // Debug Mode Section
            EditorGUILayout.LabelField("Debug Mode", EditorStyles.boldLabel);

            if (multiLayerProperties.sssDebugMode != null)
            {
                EditorGUI.BeginChangeCheck();

                int currentMode = (int) multiLayerProperties.sssDebugMode.floatValue;
                int newMode = EditorGUILayout.Popup(Styles.debugModeText, currentMode, Styles.debugModeOptions);

                if (EditorGUI.EndChangeCheck())
                {
                    multiLayerProperties.sssDebugMode.floatValue = newMode;
                    SetDebugKeywords(material);
                }

                // 显示当前模式的说明
                if (newMode > 0)
                {
                    EditorGUILayout.Space(5);

                    // 显示颜色图例
                    if (newMode == 6 || newMode == 7) // LayerWeights or DominantLayer
                    {
                        EditorGUILayout.HelpBox(Styles.debugModeHelp, MessageType.Info);
                    }

                    // 显示警告
                    EditorGUILayout.HelpBox(
                        "Debug Mode is active. Normal rendering is replaced with debug visualization.\n" +
                        "Set to 'None' to restore normal rendering.",
                        MessageType.Warning);

                    // 快速重置按钮
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("Reset to Normal Rendering"))
                    {
                        multiLayerProperties.sssDebugMode.floatValue = 0;
                        SetDebugKeywords(material);
                    }
                }
            }
        }

        /// <summary>
        /// Draws the Volumetric Ice section
        /// </summary>
        public void DrawVolumetricIceInputs(Material material)
        {
            EditorGUI.indentLevel++;
                    
             if (overallProperties.colorAdjustment != null)
             {
                 materialEditor.ShaderProperty(overallProperties.colorAdjustment, Styles.colorAdjustmentText);
             }

             if (overallProperties.overlayNormalMap != null && overallProperties.overlayNormalScale != null)
             {
                 materialEditor.TexturePropertySingleLine(Styles.overlayNormalMapText, overallProperties.overlayNormalMap, overallProperties.overlayNormalScale);
             }

             if (overallProperties.overallSmoothnessScale != null)
             {
                 materialEditor.ShaderProperty(overallProperties.overallSmoothnessScale, Styles.overallSmoothnessScaleText);
             }

             if (overallProperties.overallOcclusionScale != null)
             {
                 materialEditor.ShaderProperty(overallProperties.overallOcclusionScale, Styles.overallOcclusionScaleText);
             }
             
             if(overallProperties.overlayTilingOffset != null)
             {
                 materialEditor.ShaderProperty(overallProperties.overlayTilingOffset, new GUIContent("附加层 Tiling Offset"));
             }

             EditorGUI.indentLevel--;
             
        }

        private void DrawDiffusionProfileWarning(DiffusionProfileSettings profile)
        {
            if (profile == null)
            {
                EditorGUILayout.HelpBox(Styles.diffusionProfileNotAssigned, MessageType.Error);
            }
        }

        public override void ValidateMaterial(Material material)
        {
            SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            if (material == null)
            {
                return;
            }

            material.shaderKeywords = null;
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        }

        public override void OnOpenGUI(Material material, MaterialEditor materialEditor)
        {
            base.OnOpenGUI(material, materialEditor);
        }
        
         /// <summary>
        /// Draws the Parallax Detail section
        /// </summary>
        public void DrawParallaxDetailInputs(Material material)
        {
            if (parallaxDetailProperties.useParallaxDetail == null)
            {
                return;
            }

            // 主开关
            EditorGUI.BeginChangeCheck();
            materialEditor.ShaderProperty(parallaxDetailProperties.useParallaxDetail, Styles.useParallaxDetailText);
            if (EditorGUI.EndChangeCheck())
            {
                SetParallaxDetailKeywords(material);
            }

            bool parallaxEnabled = parallaxDetailProperties.useParallaxDetail.floatValue > 0.5f;

            EditorGUI.BeginDisabledGroup(!parallaxEnabled);
            {
                EditorGUI.indentLevel++;

                // 通用设置
                if (parallaxDetailProperties.parallaxDetailTexture != null)
                {
                    materialEditor.TexturePropertySingleLine(Styles.parallaxDetailTextureText, parallaxDetailProperties.parallaxDetailTexture);
                }

                if (parallaxDetailProperties.refractRatio != null)
                {
                    materialEditor.ShaderProperty(parallaxDetailProperties.refractRatio, Styles.refractRatioText);
                }

                if (parallaxDetailProperties.parallaxDetailScatteringRange != null)
                {
                    materialEditor.ShaderProperty(parallaxDetailProperties.parallaxDetailScatteringRange, new GUIContent("视差细节次表面散射范围"));
                }

                EditorGUILayout.Space(10);

                // Surface Detail (R Channel)
                EditorGUILayout.LabelField("R通道细节设置", EditorStyles.boldLabel);
                DrawDetailChannelProperties(
                    parallaxDetailProperties.surfaceDetailHeightScale,
                    parallaxDetailProperties.surfaceDetailTiling,
                    parallaxDetailProperties.surfaceDetailScale,
                    parallaxDetailProperties.surfaceDetailStrength,
                    parallaxDetailProperties.surfaceDetailColor,
                    parallaxDetailProperties.surfaceDetailLayerMask,
                    material
                );

                EditorGUILayout.Space(10);

                // Inner Detail (G Channel)
                EditorGUILayout.LabelField("G通道细节设置", EditorStyles.boldLabel);
                DrawDetailChannelProperties(
                    parallaxDetailProperties.innerDetailHeightScale,
                    parallaxDetailProperties.innerDetailTiling,
                    parallaxDetailProperties.innerDetailScale,
                    parallaxDetailProperties.innerDetailStrength,
                    parallaxDetailProperties.innerDetailColor,
                    parallaxDetailProperties.innerDetailLayerMask,
                    material
                );

                EditorGUILayout.Space(10);

                // Third Detail (B Channel)
                EditorGUILayout.LabelField("B通道细节设置", EditorStyles.boldLabel);
                DrawDetailChannelProperties(
                    parallaxDetailProperties.thirdDetailHeightScale,
                    parallaxDetailProperties.thirdDetailTiling,
                    parallaxDetailProperties.thirdDetailScale,
                    parallaxDetailProperties.thirdDetailStrength,
                    parallaxDetailProperties.thirdDetailColor,
                    parallaxDetailProperties.thirdDetailLayerMask,
                    material
                );

                EditorGUI.indentLevel--;
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// 绘制单个细节通道的属性
        /// </summary>
        private void DrawDetailChannelProperties(
            MaterialProperty heightScale,
            MaterialProperty tiling,
            MaterialProperty uvScale,
            MaterialProperty strength,
            MaterialProperty color,
            MaterialProperty layerMask,
            Material material)
        {
            EditorGUI.indentLevel++;

            if (heightScale != null)
            {
                materialEditor.ShaderProperty(heightScale, Styles.detailHeightScaleText);
            }

            if (tiling != null)
            {
                materialEditor.ShaderProperty(tiling, Styles.detailTilingText);
            }

            if (uvScale != null)
            {
                materialEditor.ShaderProperty(uvScale, Styles.detailUVScaleText);
            }

            if (strength != null)
            {
                materialEditor.ShaderProperty(strength, Styles.detailStrengthText);
            }

            if (color != null)
            {
                materialEditor.ShaderProperty(color, Styles.detailColorText);
            }
               

            // 层级选择（多选）
            if (layerMask != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(Styles.detailLayerMaskText);
                
                EditorGUI.indentLevel++;
                
                Vector4 mask = layerMask.vectorValue;
                
                EditorGUI.BeginChangeCheck();
                
                // 注意：Vector4 的 xyzw 分别对应 Layer 1/2/3/4
                // x = Layer 1 (Base)
                // y = Layer 2 (Second)
                // z = Layer 3 (Third)
                // w = Layer 4 (Fourth)
                bool layer1 = EditorGUILayout.Toggle("基础层", mask.x > 0.5f);
                bool layer2 = EditorGUILayout.Toggle("第二层", mask.y > 0.5f);
                bool layer3 = EditorGUILayout.Toggle("第三层", mask.z > 0.5f);
                bool layer4 = EditorGUILayout.Toggle("第四层", mask.w > 0.5f);
                
                if (EditorGUI.EndChangeCheck())
                {
                    // 确保正确设置每个分量
                    Vector4 newMask = new Vector4(
                        layer1 ? 1f : 0f,  // x = Layer 1
                        layer2 ? 1f : 0f,  // y = Layer 2
                        layer3 ?  1f : 0f,  // z = Layer 3
                        layer4 ? 1f : 0f   // w = Layer 4
                    );
                    layerMask.vectorValue = newMask;
                    
                    // 立即应用更改
                    EditorUtility.SetDirty(material);
                }
                
                // 显示当前设置的提示
                string enabledLayers = "";
                if (mask.x > 0.5f)
                {
                    enabledLayers += "1 ";
                }

                if (mask.y > 0.5f)
                {
                    enabledLayers += "2 ";
                }

                if (mask.z > 0.5f)
                {
                    enabledLayers += "3 ";
                }

                if (mask.w > 0.5f)
                {
                    enabledLayers += "4 ";
                }

                if (string.IsNullOrEmpty(enabledLayers))
                {
                    enabledLayers = "None";
                }
                
                EditorGUILayout.HelpBox($"Currently enabled on Layer(s): {enabledLayers}", MessageType.None);
                
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        private static void SetParallaxDetailKeywords(Material material)
        {
            if (material.HasProperty(ParallaxDetailShaderIDs.UseParallaxDetail))
            {
                bool useParallax = material.GetFloat(ParallaxDetailShaderIDs.UseParallaxDetail) > 0.5f;
                CoreUtils.SetKeyword(material, "_USE_PARALLAX_DETAIL", useParallax);
            }
        }

        /// <summary>
        /// Sets up keywords for the material based on current property values. 
        /// </summary>
        public static void SetMaterialKeywords(Material material, System.Action<Material> shadingModelFunc = null)
        {
            BaseShaderGUI.SetMaterialKeywords(material, shadingModelFunc, SetAllCustomKeywords);
        }

        private static void SetAllCustomKeywords(Material material)
        {
            SetSSSKeywords(material);
            SetAdvancedLightingKeywords(material);
            SetMultiLayerKeywords(material);
            SetMAHSKeyword(material);
            SetDebugKeywords(material);
            SetParallaxDetailKeywords(material);
        }

        private static void SetSSSKeywords(Material material)
        {
            if (material.HasProperty("_SubsurfaceMaskMap"))
            {
                bool hasSubsurfaceMaskMap = material.GetTexture("_SubsurfaceMaskMap") != null;
                CoreUtils.SetKeyword(material, "_SUBSURFACE_MASK_MAP", hasSubsurfaceMaskMap);
            }

            if (material.HasProperty("_TransmissionMaskMap"))
            {
                bool hasTransmissionMaskMap = material.GetTexture("_TransmissionMaskMap") != null;
                CoreUtils.SetKeyword(material, "_TRANSMISSION_MASK_MAP", hasTransmissionMaskMap);
            }

            if (material.HasProperty("_ThicknessMap"))
            {
                bool hasThicknessMap = material.GetTexture("_ThicknessMap") != null;
                CoreUtils.SetKeyword(material, "_THICKNESS_MAP", hasThicknessMap);
            }
        }

        private static void SetAdvancedLightingKeywords(Material material)
        {
            if (material.HasProperty(AdvancedLightingShaderIDs.UseBurleyDiffuse))
            {
                bool useBurleyDiffuse = material.GetFloat(AdvancedLightingShaderIDs.UseBurleyDiffuse) > 0.5f;
                CoreUtils.SetKeyword(material, "_USE_BURLEY_DIFFUSE", useBurleyDiffuse);
            }

            if (material.HasProperty(AdvancedLightingShaderIDs.UseDualSpecularLobe))
            {
                bool useDualSpecularLobe = material.GetFloat(AdvancedLightingShaderIDs.UseDualSpecularLobe) > 0.5f;
                CoreUtils.SetKeyword(material, "_USE_DUAL_SPECULAR_LOBE", useDualSpecularLobe);
            }
        }

        private static void SetMultiLayerKeywords(Material material)
        {
            if (material.HasProperty(MultiLayerShaderIDs.UseSecondLayer))
            {
                bool useSecondLayer = material.GetFloat(MultiLayerShaderIDs.UseSecondLayer) > 0.5f;
                CoreUtils.SetKeyword(material, "_USE_SECOND_LAYER", useSecondLayer);
            }

            if (material.HasProperty(MultiLayerShaderIDs.UseThirdLayer))
            {
                bool useThirdLayer = material.GetFloat(MultiLayerShaderIDs.UseThirdLayer) > 0.5f;
                CoreUtils.SetKeyword(material, "_USE_THIRD_LAYER", useThirdLayer);
            }
            
            if (material.HasProperty(MultiLayerShaderIDs.UseFourthLayer))
            {
                bool useFourthLayer = material.GetFloat(MultiLayerShaderIDs.UseFourthLayer) > 0.5f;
                CoreUtils.SetKeyword(material, "_USE_FOURTH_LAYER", useFourthLayer);
            }
        }

        private static void SetMAHSKeyword(Material material)
        {
            if (material.HasProperty(SurfaceInputShaderIDs.UseMAHSMap))
            {
                bool useMAHS = material.GetFloat(SurfaceInputShaderIDs.UseMAHSMap) > 0.5f;
                CoreUtils.SetKeyword(material, "_MAHSMAP", useMAHS);
            }
        }
        
        private static void SetDebugKeywords(Material material)
        {
            if (! material.HasProperty(MultiLayerShaderIDs.SSSDebugMode))
            {
                return;
            }
            
            int debugMode = (int)material.GetFloat(MultiLayerShaderIDs.SSSDebugMode);
            
            // 设置 debug keywords
            CoreUtils.SetKeyword(material, "_SSSDEBUGMODE_LAYER1MASK", debugMode == 1);
            CoreUtils.SetKeyword(material, "_SSSDEBUGMODE_LAYER2MASK", debugMode == 2);
            CoreUtils.SetKeyword(material, "_SSSDEBUGMODE_LAYER3MASK", debugMode == 3);
            CoreUtils.SetKeyword(material, "_SSSDEBUGMODE_LAYER4MASK", debugMode == 4);
            CoreUtils.SetKeyword(material, "_SSSDEBUGMODE_COMBINEDSSSMASK", debugMode == 5);
            CoreUtils.SetKeyword(material, "_SSSDEBUGMODE_LAYERWEIGHTS", debugMode == 6);
            CoreUtils.SetKeyword(material, "_SSSDEBUGMODE_DOMINANTLAYER", debugMode == 7);
        }

        #region GUID Conversion Helpers

        private static Vector4 ConvertGUIDToVector4(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return Vector4.zero;
            }

            try
            {
                Vector4 vector;
                byte[] bytes = System.Guid.Parse(guid).ToByteArray();
                
                uint x = System.BitConverter.ToUInt32(bytes, 0);
                uint y = System.BitConverter.ToUInt32(bytes, 4);
                uint z = System.BitConverter.ToUInt32(bytes, 8);
                uint w = System.BitConverter.ToUInt32(bytes, 12);
                
                vector.x = Asfloat(x);
                vector.y = Asfloat(y);
                vector.z = Asfloat(z);
                vector.w = Asfloat(w);
                
                return vector;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to convert GUID to Vector4: {e.Message}");
                return Vector4.zero;
            }
        }

        private static string ConvertVector4ToGUID(Vector4 vector)
        {
            if (vector == Vector4.zero)
            {
                return string.Empty;
            }

            try
            {
                byte[] bytes = new byte[16];
                
                uint x = Asuint(vector.x);
                uint y = Asuint(vector.y);
                uint z = Asuint(vector.z);
                uint w = Asuint(vector.w);
                
                System.BitConverter.GetBytes(x).CopyTo(bytes, 0);
                System.BitConverter.GetBytes(y).CopyTo(bytes, 4);
                System.BitConverter.GetBytes(z).CopyTo(bytes, 8);
                System.BitConverter.GetBytes(w).CopyTo(bytes, 12);
                
                return new System.Guid(bytes).ToString("N");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to convert Vector4 to GUID: {e.Message}");
                return string.Empty;
            }
        }

        private static float Asfloat(uint value)
        {
            return System.BitConverter.ToSingle(System.BitConverter.GetBytes(value), 0);
        }

        private static uint Asuint(float value)
        {
            return System.BitConverter.ToUInt32(System.BitConverter.GetBytes(value), 0);
        }

        #endregion
    }
}
