using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{
    internal static class Property
    {
        public static readonly string SpecularWorkflowMode = "_WorkflowMode";
        public static readonly string SurfaceType = "_Surface";
        public static readonly string BlendMode = "_Blend";
        public static readonly string CustomDepth = "_CustomDepthFade";
        public static readonly string AlphaClip = "_AlphaClip";
        public static readonly string AlphaToMask = "_AlphaToMask";
        public static readonly string SrcBlend = "_SrcBlend";
        public static readonly string DstBlend = "_DstBlend";
        public static readonly string SrcBlendAlpha = "_SrcBlendAlpha";
        public static readonly string DstBlendAlpha = "_DstBlendAlpha";
        public static readonly string BlendModePreserveSpecular = "_BlendModePreserveSpecular";
        public static readonly string ZWrite = "_ZWrite";
        public static readonly string CullMode = "_Cull";
        public static readonly string CastShadows = "_CastShadows";
        public static readonly string ReceiveShadows = "_ReceiveShadows";
        public static readonly string QueueOffset = "_QueueOffset";
        public static readonly string ColorMask = "_ColorMask";

        // for ShaderGraph shaders only
        public static readonly string ZTest = "_ZTest";
        public static readonly string ZWriteControl = "_ZWriteControl";
        public static readonly string QueueControl = "_QueueControl";

        // Global Illumination requires some properties to be named specifically: 
        public static readonly string EmissionMap = "_EmissionMap";
        public static readonly string EmissionColor = "_EmissionColor";
    }
    
    public static class SSSLitGUI
    {
        /// <summary>
        /// Workflow modes for the shader.
        /// </summary>
        public enum WorkflowMode
        {
            /// <summary>
            /// Use this for specular workflow.
            /// </summary>
            Specular = 0,

            /// <summary>
            /// Use this for metallic workflow. 
            /// </summary>
            Metallic
        }

        /// <summary>
        /// Options to select the texture channel where the smoothness value is stored.
        /// </summary>
        public enum SmoothnessMapChannel
        {
            /// <summary>
            /// Use this when smoothness is stored in the alpha channel of the Specular/Metallic Map.
            /// </summary>
            SpecularMetallicAlpha,

            /// <summary>
            /// Use this when smoothness is stored in the alpha channel of the Albedo Map.
            /// </summary>
            AlbedoAlpha,
        }

        /// <summary>
        /// Container for the text and tooltips used to display the shader. 
        /// </summary>
        public static class Styles
        {
            /// <summary>
            /// The text and tooltip for the workflow Mode GUI.
            /// </summary>
            public static GUIContent workflowModeText = EditorGUIUtility.TrTextContent("Workflow Mode",
                "Select a workflow that fits your textures.  Choose between Metallic or Specular.");

            /// <summary>
            /// The text and tooltip for the specular Map GUI.
            /// </summary>
            public static GUIContent specularMapText =
                EditorGUIUtility.TrTextContent("Specular Map", "Designates a Specular Map and specular color determining the apperance of reflections on this Material's surface.");

            /// <summary>
            /// The text and tooltip for the metallic Map GUI.
            /// </summary>
            public static GUIContent metallicMapText =
                EditorGUIUtility.TrTextContent("Metallic Map", "Sets and configures the map for the Metallic workflow.");

            /// <summary>
            /// The text and tooltip for the MAHS Map GUI.
            /// </summary>
            public static GUIContent mahsMapText =
                EditorGUIUtility.TrTextContent("MAHS Map", "Combined texture map:  R=Metallic, G=AO, A=Smoothness.");

            /// <summary>
            /// The text and tooltip for the use MAHS Map toggle GUI.
            /// </summary>
            public static GUIContent useMAHSMapText =
                EditorGUIUtility.TrTextContent("Use MAHS Map", "When enabled, uses a combined MAHS map (R=Metallic, G=AO, A=Smoothness) instead of separate maps.");

            /// <summary>
            /// The text and tooltip for the metallic GUI.
            /// </summary>
            public static GUIContent metallicText = EditorGUIUtility.TrTextContent("Metallic",
                "Controls the metallic value of the surface.");

            /// <summary>
            /// The text and tooltip for the smoothness GUI.
            /// </summary>
            public static GUIContent smoothnessText = EditorGUIUtility.TrTextContent("Smoothness",
                "Controls the spread of highlights and reflections on the surface.");

            /// <summary>
            /// The text and tooltip for the occlusion strength GUI.
            /// </summary>
            public static GUIContent occlusionStrengthText = EditorGUIUtility.TrTextContent("Occlusion Strength",
                "Controls the strength of the ambient occlusion effect.");

            /// <summary>
            /// The text and tooltip for the smoothness source GUI.
            /// </summary>
            public static GUIContent smoothnessMapChannelText =
                EditorGUIUtility.TrTextContent("Source",
                    "Specifies where to sample a smoothness map from.  By default, uses the alpha channel for your map.");

            /// <summary>
            /// The text and tooltip for the specular Highlights GUI.
            /// </summary>
            public static GUIContent highlightsText = EditorGUIUtility.TrTextContent("Specular Highlights",
                "When enabled, the Material reflects the shine from direct lighting.");

            /// <summary>
            /// The text and tooltip for the environment Reflections GUI.
            /// </summary>
            public static GUIContent reflectionsText =
                EditorGUIUtility.TrTextContent("Environment Reflections",
                    "When enabled, the Material samples reflections from the nearest Reflection Probes or Lighting Probe.");

            /// <summary>
            /// The text and tooltip for the height map GUI.
            /// </summary>
            public static GUIContent heightMapText = EditorGUIUtility.TrTextContent("Height Map",
                "Defines a Height Map that will drive a parallax effect in the shader making the surface seem displaced.");

            /// <summary>
            /// The text and tooltip for the occlusion map GUI.
            /// </summary>
            public static GUIContent occlusionText = EditorGUIUtility.TrTextContent("Occlusion Map",
                "Sets an occlusion map to simulate shadowing from ambient lighting.");

            /// <summary>
            /// The names for smoothness alpha options available for metallic workflow.
            /// </summary>
            public static readonly string[] metallicSmoothnessChannelNames = { "Metallic Alpha", "Albedo Alpha" };

            /// <summary>
            /// The names for smoothness alpha options available for specular workflow.
            /// </summary>
            public static readonly string[] specularSmoothnessChannelNames = { "Specular Alpha", "Albedo Alpha" };

            /// <summary>
            /// The text and tooltip for the enabling/disabling clear coat GUI.
            /// </summary>
            public static GUIContent clearCoatText = EditorGUIUtility.TrTextContent("Clear Coat",
                "A multi-layer material feature which simulates a thin layer of coating on top of the surface material." +
                "\nPerformance cost is considerable as the specular component is evaluated twice, once per layer.");

            /// <summary>
            /// The text and tooltip for the clear coat Mask GUI.
            /// </summary>
            public static GUIContent clearCoatMaskText = EditorGUIUtility.TrTextContent("Mask",
                "Specifies the amount of the coat blending." +
                "\nActs as a multiplier of the clear coat map mask value or as a direct mask value if no map is specified." +
                "\nThe map specifies clear coat mask in the red channel and clear coat smoothness in the green channel.");

            /// <summary>
            /// The text and tooltip for the clear coat smoothness GUI.
            /// </summary>
            public static GUIContent clearCoatSmoothnessText = EditorGUIUtility.TrTextContent("Smoothness",
                "Specifies the smoothness of the coating." +
                "\nActs as a multiplier of the clear coat map smoothness value or as a direct smoothness value if no map is specified.");
        }

        /// <summary>
        /// Container for the properties used in the <c>LitGUI</c> editor script.
        /// </summary>
        public struct LitProperties
        {
            // Surface Option Props

            /// <summary>
            /// The MaterialProperty for workflow mode.
            /// </summary>
            public MaterialProperty workflowMode;

            // Surface Input Props

            /// <summary>
            /// The MaterialProperty for metallic value.
            /// </summary>
            public MaterialProperty metallic;

            /// <summary>
            /// The MaterialProperty for specular color.
            /// </summary>
            public MaterialProperty specColor;

            /// <summary>
            /// The MaterialProperty for MAHS map.
            /// </summary>
            public MaterialProperty mahsMap;

            /// <summary>
            /// The MaterialProperty for use MAHS map toggle.
            /// </summary>
            public MaterialProperty useMAHSMap;

            /// <summary>
            /// The MaterialProperty for specular smoothness map.
            /// </summary>
            public MaterialProperty specGlossMap;

            /// <summary>
            /// The MaterialProperty for smoothness value.
            /// </summary>
            public MaterialProperty smoothness;

            /// <summary>
            /// The MaterialProperty for smoothness alpha channel.
            /// </summary>
            public MaterialProperty smoothnessMapChannel;

            /// <summary>
            /// The MaterialProperty for normal map.
            /// </summary>
            public MaterialProperty bumpMapProp;

            /// <summary>
            /// The MaterialProperty for normal map scale.
            /// </summary>
            public MaterialProperty bumpScaleProp;

            /// <summary>
            /// The MaterialProperty for height map.
            /// </summary>
            public MaterialProperty parallaxMapProp;

            /// <summary>
            /// The MaterialProperty for height map scale.
            /// </summary>
            public MaterialProperty parallaxScaleProp;

            /// <summary>
            /// The MaterialProperty for occlusion strength.
            /// </summary>
            public MaterialProperty occlusionStrength;

            /// <summary>
            /// The MaterialProperty for occlusion map.
            /// </summary>
            public MaterialProperty occlusionMap;

            // Advanced Props

            /// <summary>
            /// The MaterialProperty for specular highlights.
            /// </summary>
            public MaterialProperty highlights;

            /// <summary>
            /// The MaterialProperty for environment reflections.
            /// </summary>
            public MaterialProperty reflections;

            /// <summary>
            /// The MaterialProperty for enabling/disabling clear coat.
            /// </summary>
            public MaterialProperty clearCoat;

            /// <summary>
            /// The MaterialProperty for clear coat map.
            /// </summary>
            public MaterialProperty clearCoatMap;

            /// <summary>
            /// The MaterialProperty for clear coat mask.
            /// </summary>
            public MaterialProperty clearCoatMask;

            /// <summary>
            /// The MaterialProperty for clear coat smoothness.
            /// </summary>
            public MaterialProperty clearCoatSmoothness;

            /// <summary>
            /// Constructor for the <c>LitProperties</c> container struct.
            /// </summary>
            /// <param name="properties"></param>
            public LitProperties(MaterialProperty[] properties)
            {
                // Surface Option Props
                workflowMode = BaseShaderGUI.FindProperty("_WorkflowMode", properties, false);
                
                // Surface Input Props - MAHS workflow
                metallic = BaseShaderGUI.FindProperty("_Metallic", properties, false);
                specColor = BaseShaderGUI.FindProperty("_SpecColor", properties, false);
                mahsMap = BaseShaderGUI.FindProperty("_MAHSMap", properties, false);
                useMAHSMap = BaseShaderGUI.FindProperty("_UseMAHSMap", properties, false);
                specGlossMap = BaseShaderGUI.FindProperty("_SpecGlossMap", properties, false);
                smoothness = BaseShaderGUI.FindProperty("_Smoothness", properties, false);
                smoothnessMapChannel = BaseShaderGUI.FindProperty("_SmoothnessTextureChannel", properties, false);
                bumpMapProp = BaseShaderGUI.FindProperty("_BumpMap", properties, false);
                bumpScaleProp = BaseShaderGUI.FindProperty("_BumpScale", properties, false);
                parallaxMapProp = BaseShaderGUI.FindProperty("_ParallaxMap", properties, false);
                parallaxScaleProp = BaseShaderGUI.FindProperty("_Parallax", properties, false);
                occlusionStrength = BaseShaderGUI.FindProperty("_OcclusionStrength", properties, false);
                occlusionMap = BaseShaderGUI.FindProperty("_OcclusionMap", properties, false);
                
                // Advanced Props
                highlights = BaseShaderGUI.FindProperty("_SpecularHighlights", properties, false);
                reflections = BaseShaderGUI.FindProperty("_EnvironmentReflections", properties, false);

                clearCoat = BaseShaderGUI.FindProperty("_ClearCoat", properties, false);
                clearCoatMap = BaseShaderGUI.FindProperty("_ClearCoatMap", properties, false);
                clearCoatMask = BaseShaderGUI.FindProperty("_ClearCoatMask", properties, false);
                clearCoatSmoothness = BaseShaderGUI.FindProperty("_ClearCoatSmoothness", properties, false);
            }
        }

        /// <summary>
        /// Draws the surface inputs GUI.
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="materialEditor"></param>
        /// <param name="material"></param>
        public static void Inputs(LitProperties properties, MaterialEditor materialEditor, Material material)
        {
            DoMAHSArea(properties, materialEditor, material);
            BaseShaderGUI.DrawNormalArea(materialEditor, properties.bumpMapProp, properties.bumpScaleProp);

            if (HeightmapAvailable(material))
            {
                DoHeightmapArea(properties, materialEditor);
            }
                

            // Check that we have all the required properties for clear coat,
            // otherwise we will get null ref exception from MaterialEditor GUI helpers. 
            if (ClearCoatAvailable(material))
            {
                DoClearCoat(properties, materialEditor, material);
            }
                
        }

        /// <summary>
        /// Draws the MAHS (Metallic, AO, Smoothness) area GUI.
        /// </summary>
        public static void DoMAHSArea(LitProperties properties, MaterialEditor materialEditor, Material material)
        {
            // Use MAHS Map toggle
            if (properties.useMAHSMap != null)
            {
                EditorGUI.BeginChangeCheck();
                materialEditor.ShaderProperty(properties.useMAHSMap, Styles.useMAHSMapText);
                if (EditorGUI.EndChangeCheck())
                {
                    SetMAHSKeyword(material);
                }
            }

            bool useMAHS = properties.useMAHSMap != null && properties.useMAHSMap.floatValue > 0.5f;

            // MAHS Map texture
            if (useMAHS && properties.mahsMap != null)
            {
                materialEditor.TexturePropertySingleLine(Styles.mahsMapText, properties.mahsMap);
            }

            // Metallic slider
            if (properties.metallic != null)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(properties.metallic, Styles.metallicText);
                EditorGUI.indentLevel--;
            }

            // Smoothness slider
            if (properties.smoothness != null)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(properties.smoothness, Styles.smoothnessText);
                EditorGUI.indentLevel--;
            }

            // Occlusion Strength slider
            if (properties.occlusionStrength != null)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(properties.occlusionStrength, Styles.occlusionStrengthText);
                EditorGUI.indentLevel--;
            }
        }

        private static void SetMAHSKeyword(Material material)
        {
            if (material.HasProperty("_UseMAHSMap"))
            {
                bool useMAHS = material.GetFloat("_UseMAHSMap") > 0.5f;
                CoreUtils.SetKeyword(material, "_MAHSMAP", useMAHS);
            }
        }

        private static bool ClearCoatAvailable(Material material)
        {
            return material.HasProperty("_ClearCoat")
                && material.HasProperty("_ClearCoatMap")
                && material.HasProperty("_ClearCoatMask")
                && material.HasProperty("_ClearCoatSmoothness");
        }

        private static bool HeightmapAvailable(Material material)
        {
            return material.HasProperty("_Parallax")
                && material.HasProperty("_ParallaxMap");
        }

        private static void DoHeightmapArea(LitProperties properties, MaterialEditor materialEditor)
        {
            if (properties.parallaxMapProp != null && properties.parallaxScaleProp != null)
            {
                materialEditor.TexturePropertySingleLine(Styles.heightMapText, properties.parallaxMapProp,
                    properties.parallaxMapProp.textureValue != null ? properties.parallaxScaleProp :  null);
            }
        }

        private static bool ClearCoatEnabled(Material material)
        {
            return material.HasProperty("_ClearCoat") && material.GetFloat("_ClearCoat") > 0.0;
        }

        /// <summary>
        /// Draws the clear coat GUI.
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="materialEditor"></param>
        /// <param name="material"></param>
        public static void DoClearCoat(LitProperties properties, MaterialEditor materialEditor, Material material)
        {
            if (properties.clearCoat == null)
            {
                return;
            }
                

            materialEditor.ShaderProperty(properties.clearCoat, Styles.clearCoatText);
            var coatEnabled = material.GetFloat("_ClearCoat") > 0.0;

            EditorGUI.BeginDisabledGroup(!coatEnabled);
            {
                EditorGUI.indentLevel += 2;
                
                if (properties.clearCoatMap != null && properties.clearCoatMask != null)
                {
                    materialEditor.TexturePropertySingleLine(Styles.clearCoatMaskText, properties.clearCoatMap, properties.clearCoatMask);
                }

                if (properties.clearCoatSmoothness != null)
                {
                    materialEditor.ShaderProperty(properties.clearCoatSmoothness, Styles.clearCoatSmoothnessText);
                }

                EditorGUI.indentLevel -= 2;
            }
            EditorGUI.EndDisabledGroup();
        }

        internal static bool IsOpaque(Material material)
        {
            bool opaque = true;
            if (material.HasProperty(Property.SurfaceType))
            {
                opaque = ((BaseShaderGUI.SurfaceType)material.GetFloat(Property.SurfaceType) == BaseShaderGUI.SurfaceType.Opaque);
            }
                
            return opaque;
        }

        /// <summary>
        /// Retrieves the alpha channel used for smoothness.
        /// </summary>
        /// <param name="material"></param>
        /// <returns>The Alpha channel used for Smoothness.</returns>
        public static SmoothnessMapChannel GetSmoothnessMapChannel(Material material)
        {
            if (!material.HasProperty("_SmoothnessTextureChannel"))
            {
                return SmoothnessMapChannel.SpecularMetallicAlpha;
            }
                
                
            int ch = (int)material.GetFloat("_SmoothnessTextureChannel");
            if (ch == (int) SmoothnessMapChannel.AlbedoAlpha)
            {
                return SmoothnessMapChannel.AlbedoAlpha;
            }

            return SmoothnessMapChannel.SpecularMetallicAlpha;
        }

        // (shared by all lit shaders, including shadergraph Lit Target and Lit.shader)
        internal static void SetupSpecularWorkflowKeyword(Material material, out bool isSpecularWorkflow)
        {
            isSpecularWorkflow = false;     // default is metallic workflow
            if (material.HasProperty(Property.SpecularWorkflowMode))
            {
                isSpecularWorkflow = ((WorkflowMode)material.GetFloat(Property.SpecularWorkflowMode)) == WorkflowMode.Specular;
            }
                
            CoreUtils.SetKeyword(material, "_SPECULAR_SETUP", isSpecularWorkflow);
        }

        /// <summary>
        /// Sets up the keywords for the Lit shader and material.
        /// </summary>
        /// <param name="material"></param>
        public static void SetMaterialKeywords(Material material)
        {
            SetupSpecularWorkflowKeyword(material, out bool isSpecularWorkFlow);

            // MAHS Map keyword
            if (material.HasProperty("_UseMAHSMap"))
            {
                bool useMAHS = material.GetFloat("_UseMAHSMap") > 0.5f;
                CoreUtils.SetKeyword(material, "_MAHSMAP", useMAHS);
            }

            // Specular workflow (for compatibility)
            if (isSpecularWorkFlow && material.HasProperty("_SpecGlossMap"))
            {
                var hasSpecGlossMap = material.GetTexture("_SpecGlossMap") != null;
                CoreUtils.SetKeyword(material, "_METALLICSPECGLOSSMAP", hasSpecGlossMap);
            }

            if (material.HasProperty("_SpecularHighlights"))
            {
                CoreUtils.SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF",
                    material.GetFloat("_SpecularHighlights") == 0.0f);
            }

            if (material.HasProperty("_EnvironmentReflections"))
            {
                CoreUtils.SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF",
                    material.GetFloat("_EnvironmentReflections") == 0.0f);
            }

            if (material.HasProperty("_ParallaxMap"))
            {
                CoreUtils.SetKeyword(material, "_PARALLAXMAP", material.GetTexture("_ParallaxMap"));
            }

            if (material.HasProperty("_SmoothnessTextureChannel"))
            {
                var opaque = IsOpaque(material);
                CoreUtils.SetKeyword(material, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
                    GetSmoothnessMapChannel(material) == SmoothnessMapChannel.AlbedoAlpha && opaque);
            }

            // Clear coat keywords are independent to remove possiblity of invalid combinations. 
            if (ClearCoatEnabled(material))
            {
                var hasMap = material.HasProperty("_ClearCoatMap") && material.GetTexture("_ClearCoatMap") != null;
                if (hasMap)
                {
                    CoreUtils.SetKeyword(material, "_CLEARCOAT", false);
                    CoreUtils.SetKeyword(material, "_CLEARCOATMAP", true);
                }
                else
                {
                    CoreUtils.SetKeyword(material, "_CLEARCOAT", true);
                    CoreUtils.SetKeyword(material, "_CLEARCOATMAP", false);
                }
            }
            else
            {
                CoreUtils.SetKeyword(material, "_CLEARCOAT", false);
                CoreUtils.SetKeyword(material, "_CLEARCOATMAP", false);
            }
        }
    }
}
