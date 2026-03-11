// SSS Lit Shader - Subsurface Scattering with Standard Lit Workflow

Shader "Universal Render Pipeline/SSSLit"
{
    Properties
    {
        [Header(Surface Options)]
        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull                       ("Culling", Float) = 2
        [Toggle(_ALPHATEST_ON)]
        _AlphaClip                  ("Alpha Clipping", Float) = 0.0
        _Cutoff                     ("     Threshold", Range(0.0, 1.0)) = 0.5
        [ToggleOff(_RECEIVE_SHADOWS_OFF)]
        _ReceiveShadows             ("Receive Shadows", Float) = 1.0

        [Header(Subsurface Scattering)]
        _DiffusionProfileAsset      ("Diffusion Profile", Vector) = (0, 0, 0, 0)
        _DiffusionProfileHash       ("Diffusion Profile Hash", Float) = 0
        _TransmissionMask           ("Transmission Mask", Range(0.0, 1.0)) = 0
        _TransmissionMaskMap        ("Transmission Mask Map", 2D) = "white" {}
        _Thickness                  ("Thickness", Range(0.0, 1.0)) = 1.0
        _ThicknessMap               ("Thickness Map", 2D) = "white" {}
        _ThicknessRange             ("Thickness Range", Range(0.01, 10)) = 5
        _ThicknessPower             ("Thickness Power", Range(0.01, 0.5)) = 0.01

        [Header(Surface Inputs)]
        _WorkflowMode               ("Workflow Mode", Float) = 1.0
        
        [Header(Advanced Lighting)]
        [Toggle(_USE_DUAL_SPECULAR_LOBE)] _UseDualSpecularLobe("Enable Dual Specular Lobe", Float) = 0
        _DualSpecularLobe0Roughness("Lobe0 Roughness Multiplier", Range(0.1, 1.0)) = 0.5
        _DualSpecularLobe1Roughness("Lobe1 Roughness Multiplier", Range(1.0, 4.0)) = 2.0
        _DualSpecularLobeMix("Lobe Mix", Range(0.0, 1.0)) = 0.15
        _GIDiffuseScale("GI Diffuse Scale", Range(0.0, 1.0)) = 1

        [MainColor]
        _BaseColor                  ("Color", Color) = (1,1,1,1)
        [MainTexture]
        _BaseMap                    ("Albedo", 2D) = "white" {}

        _Smoothness                 ("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic                   ("Metallic", Range(0.0, 1.0)) = 0.0
        _BaseLayerSubsurfaceScattering("Base Layer Subsurface Scattering", Range(0.0, 1.0)) = 1
        
        [Toggle(_MAHSMAP)]
        _UseMAHSMap                 ("Use MAHS Map", Float) = 0.0
        _MAHSMap                    ("MAHS Map (R:Metallic G:AO A:Smoothness)", 2D) = "white" {}

        _SpecColor                  ("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap               ("Specular Map", 2D) = "white" {}

        [Toggle(_NORMALMAP)]
        _ApplyNormal                ("Enable Normal Map", Float) = 0.0
        [NoScaleOffset]
        _BumpMap                    ("     Normal Map", 2D) = "bump" {}
        _BumpScale                  ("     Normal Scale", Float) = 1.0
        _Layer1NormalTilingOffset   ("Layer 1 Normal Tiling/Offset", Vector) = (1,1,0,0)

        _OcclusionStrength          ("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        [HDR]
        _EmissionColor              ("Emission Color", Color) = (0,0,0)
        [NoScaleOffset]
        _EmissionMap                ("Emission Map", 2D) = "white" {}
        
        [Header(Second Layer)]
        [Toggle(_USE_SECOND_LAYER)] _UseSecondLayer("Enable Second Layer", Float) = 0
        _SecondLayerTilingOffset   ("Second Layer Tiling/Offset", Vector) = (1,1,0,0)
        _SecondLayerMaskMap         ("Second Layer Mask Map", 2D) = "black" {}
        _SecondLayerRange           ("Second Layer Range", Range(0.01, 10)) = 8.5
        _SecondLayerPower           ("Second Layer Power", Range(0.01, 0.5)) = 0.01
        _SecondLayerIntensity       ("Second Layer Intensity", Range(0.0, 1.0)) = 1.0
        _SecondLayerDiffuseMap      ("Second Layer Diffuse Map", 2D) = "white" {}
        [HDR]_SecondLayerDiffuseColor    ("Second Layer Diffuse Color", Color) = (1,1,1,1)
        _SecondLayerNormalMap       ("Second Layer Normal Map", 2D) = "bump" {}
        _SecondLayerNormalScale     ("Second Layer Normal Scale", Float) = 1.0
        _SecondLayerMAHSMap         ("Second Layer MAHS Map", 2D) = "white" {}
        _SecondLayerSmoothnessScale ("Second Layer Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _SecondLayerOcclusionScale  ("Second Layer Occlusion Strength", Range(0.0, 1.0)) = 1.0
        _SecondLayerMetallicScale   ("Second Layer Metallic Scale", Range(0.0, 1.0)) = 1.0
        _SecondLayerSubsurfaceScattering("Second Layer Subsurface Scattering", Range(0.0, 1.0)) = 1
        
        [Header(Third Layer)]
        [Toggle(_USE_THIRD_LAYER)] _UseThirdLayer("Enable Third Layer", Float) = 0
        _ThirdLayerTilingOffset     ("Third Layer Tiling/Offset", Vector) = (1,1,0,0)
        _ThirdLayerMaskMap          ("Third Layer Mask Map", 2D) = "black" {}
        _ThirdLayerRange            ("Third Layer Range", Range(0.01, 10)) = 8.5
        _ThirdLayerPower            ("Third Layer Power", Range(0.01, 0.5)) = 0.01
        _ThirdLayerIntensity        ("Third Layer Intensity", Range(0.0, 1.0)) = 1.0
        _ThirdLayerDiffuseMap       ("Third Layer Diffuse Map", 2D) = "white" {}
        [HDR]_ThirdLayerDiffuseColor     ("Third Layer Diffuse Color", Color) = (1,1,1,1)
        _ThirdLayerNormalMap        ("Third Layer Normal Map", 2D) = "bump" {}
        _ThirdLayerNormalScale      ("Third Layer Normal Scale", Float) = 1.0
        _ThirdLayerMAHSMap          ("Third Layer MAHS Map", 2D) = "white" {}
        _ThirdLayerSmoothnessScale  ("Third Layer Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _ThirdLayerOcclusionScale   ("Third Layer Occlusion Strength", Range(0.0, 1.0)) = 1.0
        _ThirdLayerMetallicScale    ("Third Layer Metallic Scale", Range(0.0, 1.0)) = 1.0
        _ThirdLayerSubsurfaceScattering("Third Layer Subsurface Scattering", Range(0.0, 1.0)) = 1
        
        [Header(Fourth Layer)]
        [Toggle(_USE_FOURTH_LAYER)] _UseFourthLayer("Enable Fourth Layer", Float) = 0
        _FourthLayerTilingOffset     ("Fourth Layer Tiling/Offset", Vector) = (1,1,0,0)
        _FourthLayerMaskMap          ("Fourth Layer Mask Map", 2D) = "black" {}
        _FourthLayerRange            ("Fourth Layer Range", Range(0.01, 10)) = 8.5
        _FourthLayerPower            ("Fourth Layer Power", Range(0.01, 0.5)) = 0.01
        _FourthLayerIntensity        ("Fourth Layer Intensity", Range(0.0, 1.0)) = 1.0
        _FourthLayerDiffuseMap       ("Fourth Layer Diffuse Map", 2D) = "white" {}
        [HDR]_FourthLayerDiffuseColor     ("Fourth Layer Diffuse Color", Color) = (1,1,1,1)
        _FourthLayerNormalMap        ("Fourth Layer Normal Map", 2D) = "bump" {}
        _FourthLayerNormalScale      ("Fourth Layer Normal Scale", Float) = 1.0
        _FourthLayerNormalBlendIntensity("Fourth Layer Normal Blend Intensity", Range(0.0, 1.0)) = 1.0
        _FourthLayerMAHSMap          ("Fourth Layer MAHS Map", 2D) = "white" {}
        _FourthLayerSmoothnessScale  ("Fourth Layer Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _FourthLayerOcclusionScale   ("Fourth Layer Occlusion Strength", Range(0.0, 1.0)) = 1.0
        _FourthLayerMetallicScale    ("Fourth Layer Metallic Scale", Range(0.0, 1.0)) = 1.0
        _FourthLayerSubsurfaceScattering("Fourth Layer Subsurface Scattering", Range(0.01, 1.0)) = 1
        //_FourthLayerReflectionIntensity("Fourth Layer Reflection Intensity", Range(0.0, 1.0)) = 1.0
        
        [Header(Multi Layer Debug)]
        [KeywordEnum(None, Layer1Mask, Layer2Mask, Layer3Mask, Layer4Mask, CombinedSSSMask, LayerWeights, DominantLayer)]
        _SSSDebugMode               ("SSS Debug Mode", Float) = 0
         
        [HDR]_OverallColorAdjustment("Color Adjustment", Color) = (1,1,1,1)
        _OverlayNormalMap           ("Overlay Normal Map", 2D) = "bump" {}
        _OverlayNormalScale         ("Overlay Normal Scale", Float) = 1.0
        _OverlayTilingOffset        ("Overlay Tiling/Offset", Vector) = (1,1,0,0)
        _OverallSmoothnessScale     ("Overall Smoothness Scale", Range(0.0, 2.0)) = 1.0
        _OverallOcclusionScale      ("Overall Occlusion Strength", Range(0.0, 2.0)) = 1.0
        
        [Header(Parallax Detail)]
        [Toggle(_USE_PARALLAX_DETAIL)] _UseParallaxDetail("Enable Parallax Detail", Float) = 0
        _ParallaxDetailTexture      ("Parallax Detail Texture (RGB)", 2D) = "black" {}
        _RefractRatio               ("Refract Ratio", Range(0.0, 0.5)) = 0.1
        
        [Header(Surface Detail R Channel)]
        _SurfaceDetailHeightScale   ("Surface Height Scale", Range(0.0, 1.0)) = 0.1
        _SurfaceDetailTiling        ("Surface Tiling", Range(0.1, 10.0)) = 1.0
        _SurfaceDetailScale         ("Surface Scale", Range(0.0, 1)) = 0.1
        _SurfaceDetailStrength      ("Surface Strength", Range(0.0, 5.0)) = 1.0
        [HDR] _SurfaceDetailColor   ("Surface Color", Color) = (1,1,1,1)
        // Layer Mask:  x=Layer1, y=Layer2, z=Layer3, w=Layer4
        // 默认只在 Layer 1 上显示
        _SurfaceDetailLayerMask     ("Surface Layer Mask", Vector) = (1,0,0,0)
        
        [Header(Inner Detail G Channel)]
        _InnerDetailHeightScale     ("Inner Height Scale", Range(0.0, 1.0)) = 0.2
        _InnerDetailTiling          ("Inner Tiling", Range(0.1, 10.0)) = 1.0
        _InnerDetailScale           ("Inner Scale", Range(0.0, 1)) = 0.15
        _InnerDetailStrength        ("Inner Strength", Range(0.0, 5.0)) = 1.0
        [HDR] _InnerDetailColor     ("Inner Color", Color) = (0.8,0.9,1,1)
        // 默认只在 Layer 1 上显示
        _InnerDetailLayerMask       ("Inner Layer Mask", Vector) = (1,0,0,0)
        
        [Header(Third Detail B Channel)]
        _ThirdDetailHeightScale     ("Third Height Scale", Range(0.0, 1.0)) = 0.3
        _ThirdDetailTiling          ("Third Tiling", Range(0.1, 10.0)) = 1.0
        _ThirdDetailScale           ("Third Scale", Range(0.0, 1)) = 0.2
        _ThirdDetailStrength        ("Third Strength", Range(0.0, 5.0)) = 1.0
        [HDR] _ThirdDetailColor     ("Third Color", Color) = (0.6,0.8,1,1)
        // 默认只在 Layer 1 上显示
        _ThirdDetailLayerMask       ("Third Layer Mask", Vector) = (1,0,0,0)

        [Header(Advanced)]
        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)]
        _SpecularHighlights         ("Specular Highlights", Float) = 1.0
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)]
        _EnvironmentReflections     ("Environment Reflections", Float) = 1.0
        
        _ShadowIntensity            ("Shadow Intensity", Range(0.0, 1.0)) = 1.0

        [Header(Render Queue)]
        [IntRange] _QueueOffset     ("Queue Offset", Range(-50, 50)) = 0

        // Lightmapper and outline selection shader need _MainTex, _Color and _Cutoff
        [HideInInspector] _MainTex  ("Albedo", 2D) = "white" {}
        [HideInInspector] _Color    ("Color", Color) = (1,1,1,1)
        
        // Blending state
        [HideInInspector] _Surface  ("__surface", Float) = 0.0
        [HideInInspector] _Blend    ("__blend", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite   ("__zw", Float) = 1.0
        
        // ++ [zoeyyzhong] - Dither
        //[Header(Dither)]
        [HideInInspector][Toggle]_EnableDitherGUI("_EnableDitherGUI", Float) = 1.0  // 仅用于控制GUI
        // [Toggle]_DITHER_SWITCH("Debug: Dither可见(默认不勾)", Float) = 0.0
		 [HideInInspector][MaterialToggle(_DITHER_SWITCH_ON)] _DitherSwitch("Debug: Dither可见(默认不勾)", Float) = 0.0  // 原默认值1
		 [HideInInspector]_DitherTex("Dither Texture", 2D) = "white" {}
		 [HideInInspector]_DitherScale("Dither Texture Scale",Range(0.0, 1.0)) = 0.006
		 [HideInInspector]_DitherThreshold("Dither Threshold",Float) = 0.85
         [HideInInspector]_DitherRange("Dither Range挖洞范围",Float) = 4.5
         [HideInInspector] _DitherSmooth("Dither Smooth挖洞过渡平滑度",Float) = 1.25
         [HideInInspector]_If_TreeLeaf("开启/关闭 树叶",Range(0.0, 1.0)) = 0.0  //墙壁挂件替换为 判断是否树叶
         [HideInInspector] _TreeLeaf_RangeOffset("树叶 Range挖洞范围偏移",Float) = 0.0
         [HideInInspector]_ChaToCamOffset("_ChaToCamOffset", Float) = 0.0
         [HideInInspector]_EnableDither_All("开启/关闭Dither All",Range(0.0, 1.0)) = 0.0     //碰撞开启 不需要隐藏
         [HideInInspector]_EnableDither_Part("开启/关闭Dither Part",Range(0.0, 1.0)) = 0.0   //碰撞开启 不需要隐藏        
         [HideInInspector]_DitherStartTime("Dither StartTime",Float) = 0.0 
         [HideInInspector]_Dither_Fade("Dither Fade",Float) = 1.0
        // -- [zoeyyzhong] - Dither

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 600

        //  SSSBuffer Pass - Split Lighting Output -----------------------------------------------------
        Pass
        {
            Name "SSSBuffer"
            Tags
            {
                "LightMode" = "SSSBuffer"
            }

            Stencil {
                Ref   4
                ReadMask 255
                WriteMask 255
                Comp  Always
                Pass  Replace
                Fail  Keep
                ZFail Keep
            }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex SSSBufferVertex
            #pragma fragment SSSBufferFragment

            // -------------------------------------
            // Material Keywords
            #define _NORMALMAP 1
            #define _EMISSION 1
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _MAHSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _SUBSURFACE_MASK_MAP
            #pragma shader_feature_local_fragment _TRANSMISSION_MASK_MAP
            #pragma shader_feature_local_fragment _THICKNESS_MAP
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            
            // Multi-Layer Keywords
            #pragma shader_feature_local_fragment _USE_SECOND_LAYER
            #pragma shader_feature_local_fragment _USE_THIRD_LAYER
            #pragma shader_feature_local_fragment _USE_FOURTH_LAYER

            // Debug Mode Keywords
            #pragma shader_feature_local_fragment _ _SSSDEBUGMODE_LAYER1MASK _SSSDEBUGMODE_LAYER2MASK _SSSDEBUGMODE_LAYER3MASK _SSSDEBUGMODE_LAYER4MASK _SSSDEBUGMODE_COMBINEDSSSMASK _SSSDEBUGMODE_LAYERWEIGHTS _SSSDEBUGMODE_DOMINANTLAYER
            
            // Advanced Lighting Keywords
            #pragma shader_feature_local_fragment _USE_DUAL_SPECULAR_LOBE
            #pragma shader_feature_local_fragment _USE_PARALLAX_DETAIL

            // -------------------------------------
            // Universal Pipeline keywords
            //#pragma enable_d3d11_debug_symbols
            // -------------------------------------
            // Universal Pipeline keywords
            #define _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #define DIRLIGHTMAP_COMBINED
            #define MAIN_LIGHT_CALCULATE_SHADOWS
           
            #pragma multi_compile _ LIGHTMAP_ON

            //只有室外
            #define LIGHTMAP_OUTDOOR
            //#pragma multi_compile_fragment _ LIGHTMAP_OUTDOOR
            #pragma shader_feature _ USE_LIGHTPROBE
            
            //#pragma shader_feature _ USE_LIGHTPROBE
            #pragma multi_compile_fragment _ LOD_DEBUG // 用于显示LODDebug信息
            #pragma multi_compile_fragment _ SLOPE_DEBUG // 用于可视化坡度数据Debug信息

            #pragma multi_compile_vertex _ COMPRESS_VERTEX_BUFFER  //mesh vertex buffer压缩，类似于lightmap on，由引擎控制开启关闭
            //不支持mesh压缩的mesh(skin mesh)材质开启
            #pragma shader_feature_local_vertex _ _SUPPORT_COMPRESS_VERTEX_OFF
            
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma multi_compile_fragment _ CONSTANT_EDITOR // 用于在编辑器环境Shader常量开关
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // Define SHADERPASS for texturing mode logic
            #include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/ShaderPass.cs.hlsl"

            #define SHADERPASS SHADERPASS_FORWARD_SSSBUFFER

            #include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitForwardPass.hlsl"

            ENDHLSL
        }

         Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords

            //--------------------------------------

            // -------------------------------------
            // Universal Pipeline keywords

            // ------------------------------------
            
            #pragma multi_compile_vertex _ COMPRESS_VERTEX_BUFFER
            //不支持mesh压缩的mesh(skin mesh)材质开启
            #pragma shader_feature_local_vertex _ _SUPPORT_COMPRESS_VERTEX_OFF

             // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal@14.0.11/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma multi_compile _ _DITHER_SWITCH_ON

            // -------------------------------------

            //--------------------------------------
            // GPU Instancing
            
            #pragma multi_compile_vertex _ COMPRESS_VERTEX_BUFFER

             // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            
            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal@14.0.11/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ++ [zoeyyzhong] - 单面建筑的背面效果 结合dither使用
        Pass
        {
            Name "DarkBackface"
            Tags { "LightMode" = "DarkBackface" }    //"LightMode" = "DarkBackface" 用于标识该Pass
            Cull Front

            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_vertex _ COMPRESS_VERTEX_BUFFER
            //不支持mesh压缩的mesh(skin mesh)材质开启
            #pragma shader_feature_local_vertex _ _SUPPORT_COMPRESS_VERTEX_OFF

            // ++ [zoeyyzhong] - Dither
            #pragma multi_compile _ _DITHER_SWITCH_ON
            // #pragma shader_feature _ _DITHERTEX        // 仅用于GUI显示
            // -- [zoeyyzhong] - Dither
            
            #include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitForwardPass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Soul/TA/Dither/DitherBackFacePass.hlsl"
            
            ENDHLSL
        }
		// -- [zoeyyzhong] - 单面建筑的背面效果
        
        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _EMISSION            
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature EDITOR_VISUALIZATION
            //#pragma shader_feature_local_fragment  _MAHS_MAP

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal@14.0.11/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitForwardPass.hlsl"

            half4 UniversalFragmentMetaLit(Varyings_Meta input) : SV_Target
            {
                SurfaceData surfaceData;
                DoMetaPass(input, surfaceData);

                BRDFData brdfData;
                InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular,
                     surfaceData.smoothness, surfaceData.alpha, brdfData);

                UnityMetaInput metaInput;
                metaInput.Albedo = half3(0.5, 0.5, 0.8);
                metaInput.Emission = half3(0, 0, 0);
                #ifdef EDITOR_VISUALIZATION
                metaInput.VizUV = input.VizUV;
                metaInput.LightCoord = input.LightCoord;
                #endif
                return UnityMetaFragment(metaInput);
            }

            ENDHLSL
        }

        // This pass it is only used for some editor preview effect.
        Pass
        {
            Name "EditorPreview"
            Tags
            {
                "LightMode" = "EditorPreview"
            }
            
            // -------------------------------------
            // Render State Commands
            Cull Off
            
            HLSLPROGRAM
            #pragma target 4.5

            // -------------------------------------
            // Shader Stages
            #pragma require geometry
            #pragma vertex UniversalVertexPreview
            #pragma fragment UniversalFragmentPreviewLit
            #pragma geometry UniversalGeometryPreview

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAPINDEX_PREVIEW
            
            #include "Packages/com.unity.render-pipelines.universal@14.0.11/ArtShaders/Scene/SubsurfaceScattering/Includes/SSSLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitPreviewPass.hlsl"

            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "SoulRender.SSSLitShaderGUI"
}