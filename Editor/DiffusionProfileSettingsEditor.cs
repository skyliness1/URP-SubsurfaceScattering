using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace SoulRender 
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(DiffusionProfileSettings))]
    public partial class DiffusionProfileSettingsEditor : UnityEditor.Editor
    {
        private Material m_ProfileMaterial;
        private Material m_TransmittanceMaterial;

        private List<DiffusionProfileSettings> m_DiffusionProfileSettingsTargets;
        private SerializedDiffusionProfileSettings m_SerializedDiffusionProfileSettings;
        private bool m_MultipleObjectSelected;

        void OnEnable()
        {
            // These shaders don't need to be reference by RenderPipelineResource as they are not use at runtime
            m_ProfileMaterial = CoreUtils.CreateEngineMaterial("Hidden/SubsurfaceScatteringEditor/DrawDiffusionProfile");
            m_TransmittanceMaterial = CoreUtils.CreateEngineMaterial("Hidden/SubsurfaceScatteringEditor/DrawTransmittanceGraph");

            m_DiffusionProfileSettingsTargets = targets.Cast<DiffusionProfileSettings>().ToList();
            m_SerializedDiffusionProfileSettings = new SerializedDiffusionProfileSettings((DiffusionProfileSettings)target, serializedObject);
            m_MultipleObjectSelected = targets.Length > 1;

            Undo.undoRedoPerformed += UpdateProfile;
        }

        void OnDisable()
        {
            CoreUtils.Destroy(m_ProfileMaterial);
            CoreUtils.Destroy(m_TransmittanceMaterial);

            m_SerializedDiffusionProfileSettings.Dispose();

            Undo.undoRedoPerformed -= UpdateProfile;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScattering(m_MultipleObjectSelected);
            DrawIORAndScale();
            DrawSSS();
            DrawTransmission();

            //NOTE: We manually apply changes and update all properties every time to fix a case when User click Reset on Component.
            //Unfortunately there is no way to receive callback from that Reset so only way to have correct Preview is to update target every time.
            foreach (var settings in m_DiffusionProfileSettingsTargets)
            {
                UpdateProfile(settings);
            }

            serializedObject.ApplyModifiedProperties();

            if (!m_MultipleObjectSelected)
            {
                RenderPreview();
            }
        }

        void DrawScattering(bool multipleObjectSelected)
        {
            EditorGUILayout.LabelField(Styles.scatteringLabel, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                bool previousShowMixedValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = m_SerializedDiffusionProfileSettings.scatteringDistance.hasMultipleDifferentValues;
                
                using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
                {
                    // For some reason the HDR picker is in gamma space, so convert to maintain same visual
                    var color = EditorGUILayout.ColorField(Styles.profileScatteringColor,
                        m_SerializedDiffusionProfileSettings.scatteringDistance.colorValue.gamma, true, false, false);
                    if (changeCheckScope.changed)
                    {
                        m_SerializedDiffusionProfileSettings.scatteringDistance.colorValue = color.linear;
                    }
                }
                
                EditorGUI.showMixedValue = previousShowMixedValue;

                using (new EditorGUI.IndentLevelScope())
                    EditorGUILayout.PropertyField(m_SerializedDiffusionProfileSettings.scatteringDistanceMultiplier,
                        Styles.profileScatteringDistanceMultiplier);

                if (!multipleObjectSelected)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.FloatField(Styles.profileMaxRadius, m_SerializedDiffusionProfileSettings.objReference.filterRadius);
                }
            }

            EditorGUILayout.Space();
        }

        void DrawIORAndScale()
        {
            EditorGUILayout.Slider(m_SerializedDiffusionProfileSettings.ior, 1.0f, 2.0f, Styles.profileIor);
            
            // Clamp worldScale to prevent reset to default when value is 0 or negative
            using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_SerializedDiffusionProfileSettings.worldScale, Styles.profileWorldScale);
                if (changeCheckScope.changed)
                {
                    // Ensure worldScale is at least 0.001 to avoid triggering reset in Validate()
                    if (m_SerializedDiffusionProfileSettings.worldScale.floatValue <= 0.0f)
                    {
                        m_SerializedDiffusionProfileSettings.worldScale.floatValue = 0.001f;
                    }
                }
            }
            
            EditorGUILayout.Space();
        }

        internal void DualSliderWithFields(GUIContent label, SerializedProperty values, float minLimit, float maxLimit)
        {
            const float fieldWidth = 65;
            const float padding = 4;
            var rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, label);

            float slider;
            Vector2 value = values.vector2Value;
            float midLevel = (minLimit + maxLimit) * 0.5f;

            EditorGUI.showMixedValue = values.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();

            if (rect.width >= 3 * fieldWidth + 2 * padding)
            {
                rect.xMin -= 15 * EditorGUI.indentLevel;
                var tmpRect = new Rect(rect);
                tmpRect.width = fieldWidth;

                EditorGUI.BeginChangeCheck();
                slider = EditorGUI.FloatField(tmpRect, value.x);
                if (EditorGUI.EndChangeCheck())
                {
                    value.x = Mathf.Clamp(slider, minLimit, midLevel);
                }

                tmpRect.x = rect.xMax - fieldWidth;
                    EditorGUI.BeginChangeCheck();
                slider = EditorGUI.FloatField(tmpRect, value.y);
                if (EditorGUI.EndChangeCheck())
                {
                    value.y = Mathf.Clamp(slider, midLevel, maxLimit);
                }

                tmpRect.xMin = rect.xMin + (fieldWidth + padding);
                tmpRect.xMax = rect.xMax - (EditorGUIUtility.fieldWidth + padding);
                rect = tmpRect;
            }

            rect.width = (rect.width - padding) * 0.5f;
            EditorGUI.BeginChangeCheck();
            slider = GUI.HorizontalSlider(rect, value.x, minLimit, midLevel);
            if (EditorGUI.EndChangeCheck())
            {
                value.x = slider;
            }

            rect.x += rect.width + padding - 1;
            EditorGUI.BeginChangeCheck();
            slider = GUI.HorizontalSlider(rect, value.y, midLevel, maxLimit);
            if (EditorGUI.EndChangeCheck())
            {
                value.y = slider;
            }

            if (EditorGUI.EndChangeCheck())
            {
                values.vector2Value = value;
            }
                
            EditorGUI.showMixedValue = false;
        }

        void DrawSSS()
        {
            EditorGUILayout.LabelField(Styles.subsurfaceScatteringLabel, EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(m_SerializedDiffusionProfileSettings.texturingMode);
                // DualSliderWithFields(Styles.smoothnessMultipliers, m_SerializedDiffusionProfileSettings.smoothnessMultipliers, 0.0f, 2.0f);
                // EditorGUILayout.PropertyField(m_SerializedDiffusionProfileSettings.lobeMix);
                EditorGUILayout.PropertyField(m_SerializedDiffusionProfileSettings.diffusePower);
                EditorGUILayout.PropertyField(m_SerializedDiffusionProfileSettings.borderAttenuationColor);
            }

            EditorGUILayout.Space();
        }

        void DrawTransmission()
        {
            EditorGUILayout.LabelField(Styles.transmissionLabel, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var transmissionModeMixedValues = m_SerializedDiffusionProfileSettings.transmissionMode.hasMultipleDifferentValues;
                
                bool previousShowMixedValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = transmissionModeMixedValues;
                
                using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
                {
                    var previousTransmissionMode = transmissionModeMixedValues ? int.MinValue : m_SerializedDiffusionProfileSettings.transmissionMode.intValue;
                    var newTransmissionMode = EditorGUILayout.EnumPopup(Styles.profileTransmissionMode, (DiffusionProfile.TransmissionMode)previousTransmissionMode);
                    if (changeCheckScope.changed)
                        m_SerializedDiffusionProfileSettings.transmissionMode.intValue = (int)(DiffusionProfile.TransmissionMode)newTransmissionMode;
                }
                
                EditorGUI.showMixedValue = previousShowMixedValue;

                EditorGUILayout.PropertyField(m_SerializedDiffusionProfileSettings.transmissionTint, Styles.profileTransmissionTint);
                EditorGUILayout.PropertyField(m_SerializedDiffusionProfileSettings.thicknessRemap, Styles.profileMinMaxThickness);

                if (!m_SerializedDiffusionProfileSettings.thicknessRemap.hasMultipleDifferentValues)
                {
                    using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
                    {
                        var thicknessRemap = m_SerializedDiffusionProfileSettings.thicknessRemap.vector2Value;
                        EditorGUILayout.MinMaxSlider(Styles.profileThicknessRemap, ref thicknessRemap.x, ref thicknessRemap.y, 0f, 50f);
                        if (changeCheckScope.changed)
                            m_SerializedDiffusionProfileSettings.thicknessRemap.vector2Value = thicknessRemap;
                    }
                }
            }

            EditorGUILayout.Space();
        }

        void RenderPreview()
        {
            EditorGUILayout.LabelField(Styles.profilePreview0, Styles.miniBoldButton);
            EditorGUILayout.LabelField(Styles.profilePreview1, EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();

            var obj = m_SerializedDiffusionProfileSettings.objReference;
            var radius = obj.filterRadius;
            var shape = obj.shapeParam;

            m_ProfileMaterial.SetFloat("_MaxRadius", radius);
            m_ProfileMaterial.SetVector("_ShapeParam", shape);

            // Draw the profile.
            EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetRect(256f, 256f), m_SerializedDiffusionProfileSettings.profileRT, m_ProfileMaterial, ScaleMode.ScaleToFit, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Styles.transmittancePreview0, Styles.miniBoldButton);
            EditorGUILayout.LabelField(Styles.transmittancePreview1, EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();

            m_TransmittanceMaterial.SetVector("_ShapeParam", shape);
            m_TransmittanceMaterial.SetVector("_TransmissionTint", obj.transmissionTint);
            m_TransmittanceMaterial.SetVector("_ThicknessRemap", obj.thicknessRemap);

            // Draw the transmittance graph.
            EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetRect(16f, 16f), m_SerializedDiffusionProfileSettings.transmittanceRT, m_TransmittanceMaterial, ScaleMode.ScaleToFit, 16f);
        }

        void UpdateProfile()
        {
            UpdateProfile(m_SerializedDiffusionProfileSettings.settings);
        }

        void UpdateProfile(DiffusionProfileSettings settings)
        {
            settings.profile.Validate();
            settings.UpdateCache();
        }
    }

    public sealed partial class DiffusionProfileSettingsEditor
    {
        private static class Styles
        {
            public static readonly GUIContent scatteringLabel = EditorGUIUtility.TrTextContent("Scattering");
            public static readonly GUIContent profileScatteringColor = EditorGUIUtility.TrTextContent("Scattering Color", "Controls the shape of the Diffusion Profile, and should be similar to the diffuse color of the Material.");
            public static readonly GUIContent profileScatteringDistanceMultiplier = EditorGUIUtility.TrTextContent("Multiplier", "Acts as a multiplier on the scattering color to control how far light travels below the surface, and controls the effective radius of the filter.");
            public static readonly GUIContent profileTransmissionTint = EditorGUIUtility.TrTextContent("Transmission Tint", "Specifies the tint of the translucent lighting transmitted through objects.");
            public static readonly GUIContent profileMaxRadius = EditorGUIUtility.TrTextContent("Max Radius", "The maximum radius of the effect you define in Scattering Color and Multiplier.\nWhen the world scale is 1, this value is in millimeters.");

            public static readonly GUIContent profileWorldScale = EditorGUIUtility.TrTextContent("World Scale", "Controls the scale of Unity's world units for this Diffusion Profile.");
            public static readonly GUIContent profileIor = EditorGUIUtility.TrTextContent("Index of Refraction", "Controls the refractive behavior of the Material, where larger values increase the intensity of specular reflection.");

            public static readonly GUIContent subsurfaceScatteringLabel = EditorGUIUtility.TrTextContent("Subsurface Scattering only");
            public static readonly GUIContent smoothnessMultipliers = EditorGUIUtility.TrTextContent("Dual Lobe Multipliers", "Mutlipliers for the smoothness of the two specular lobes");

            public static readonly GUIContent transmissionLabel = EditorGUIUtility.TrTextContent("Transmission only");
            public static readonly GUIContent profileTransmissionMode = EditorGUIUtility.TrTextContent("Transmission Mode", "Specifies how URP calculates light transmission.");
            public static readonly GUIContent profileMinMaxThickness = EditorGUIUtility.TrTextContent("Thickness Remap Values (Min-Max)", "Sets the range of thickness values (in millimeters) corresponding to the [0, 1] range of texel values stored in the Thickness Map.");
            public static readonly GUIContent profileThicknessRemap = EditorGUIUtility.TrTextContent("Thickness Remap (Min-Max)", profileMinMaxThickness.tooltip);


            public static readonly GUIContent profilePreview0 = EditorGUIUtility.TrTextContent("Diffusion Profile Preview");
            public static readonly GUIContent profilePreview1 = EditorGUIUtility.TrTextContent("Displays the fraction of lights scattered from the source located in the center.");
            public static readonly GUIContent transmittancePreview0 = EditorGUIUtility.TrTextContent("Transmittance Preview");
            public static readonly GUIContent transmittancePreview1 = EditorGUIUtility.TrTextContent("Displays the fraction of light passing through the GameObject depending on the values from the Thickness Remap (mm).");
            public static GUIStyle miniBoldButton => s_MiniBoldButton.Value;
            public static readonly System.Lazy<GUIStyle> s_MiniBoldButton = new System.Lazy<GUIStyle>(() => new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold
            });
        }
    }

    internal sealed class SerializedDiffusionProfileSettings : System.IDisposable
    {
        internal DiffusionProfileSettings settings;
        internal DiffusionProfile objReference;

        internal SerializedProperty scatteringDistance;
        internal SerializedProperty scatteringDistanceMultiplier;
        internal SerializedProperty transmissionTint;
        internal SerializedProperty texturingMode;
        internal SerializedProperty smoothnessMultipliers;
        internal SerializedProperty lobeMix;
        internal SerializedProperty diffusePower;
        internal SerializedProperty borderAttenuationColor;
        internal SerializedProperty transmissionMode;
        internal SerializedProperty thicknessRemap;
        internal SerializedProperty worldScale;
        internal SerializedProperty ior;

        // Render preview
        internal readonly RenderTexture profileRT;
        internal readonly RenderTexture transmittanceRT;

        internal SerializedDiffusionProfileSettings(DiffusionProfileSettings settings,
            SerializedObject serializedObject)
        {
            var serializedProfile =
                (new PropertyFetcher<DiffusionProfileSettings>(serializedObject).Find(x => x.profile));
            var rp = new RelativePropertyFetcher<DiffusionProfile>(serializedProfile);

            profileRT = new RenderTexture(256, 256, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat);
            transmittanceRT = new RenderTexture(16, 256, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat);

            this.settings = settings;
            objReference = settings.profile;

            scatteringDistance = rp.Find(x => x.scatteringDistance);
            scatteringDistanceMultiplier = rp.Find(x => x.scatteringDistanceMultiplier);
            transmissionTint = rp.Find(x => x.transmissionTint);
            texturingMode = rp.Find(x => x.texturingMode);
            smoothnessMultipliers = rp.Find(x => x.smoothnessMultipliers);
            lobeMix = rp.Find(x => x.lobeMix);
            diffusePower = rp.Find(x => x.diffuseShadingPower);
            borderAttenuationColor = rp.Find(x => x.borderAttenuationColor);
            transmissionMode = rp.Find(x => x.transmissionMode);
            thicknessRemap = rp.Find(x => x.thicknessRemap);
            worldScale = rp.Find(x => x.worldScale);
            ior = rp.Find(x => x.ior);
        }

        internal void Dispose() => ((System.IDisposable)this).Dispose();

        void System.IDisposable.Dispose()
        {
            CoreUtils.Destroy(profileRT);
            CoreUtils.Destroy(transmittanceRT);
        }
    }

    // Helper classes for property fetching
    internal class PropertyFetcher<T> where T : UnityEngine.Object
    {
        private  SerializedObject m_SerializedObject;
        private Dictionary<string, SerializedProperty> m_SerializedProperties = new Dictionary<string, SerializedProperty>();

        public PropertyFetcher(SerializedObject serializedObject)
        {
            m_SerializedObject = serializedObject;
        }

        public SerializedProperty Find<TValue>(System.Linq.Expressions.Expression<System.Func<T, TValue>> expr)
        {
            var body = expr.Body as System.Linq.Expressions.MemberExpression;
            if (body == null)
            {
                throw new System.ArgumentException("Expression must be a member access expression");
            }

            var propertyName = body.Member.Name;
            if (!m_SerializedProperties.TryGetValue(propertyName, out var property))
            {
                property = m_SerializedObject.FindProperty(propertyName);
                m_SerializedProperties[propertyName] = property;
            }
            return property;
        }
    }

    internal class RelativePropertyFetcher<T>
    {
        private SerializedProperty m_SerializedProperty;
        private Dictionary<string, SerializedProperty> m_SerializedProperties = new Dictionary<string, SerializedProperty>();

        public RelativePropertyFetcher(SerializedProperty serializedProperty)
        {
            m_SerializedProperty = serializedProperty;
        }

        public SerializedProperty Find<TValue>(System.Linq.Expressions.Expression<System.Func<T, TValue>> expr)
        {
            var body = expr.Body as System.Linq.Expressions.MemberExpression;
            if (body == null)
            {
                throw new System.ArgumentException("Expression must be a member access expression");
            }
                
            var propertyName = body.Member.Name;
            if (!m_SerializedProperties.TryGetValue(propertyName, out var property))
            {
                property = m_SerializedProperty.FindPropertyRelative(propertyName);
                m_SerializedProperties[propertyName] = property;
            }
            return property;
        }
    }
}
