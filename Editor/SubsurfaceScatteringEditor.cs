using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace SoulRender
{
    /// <summary>
    /// Custom editor for SubsurfaceScattering Render Feature
    /// Provides Auto Collect button to gather all profiles
    /// </summary>
    [CustomEditor(typeof(SubsurfaceScattering))]
    public class SubsurfaceScatteringEditor : UnityEditor.Editor
    {
        private SerializedProperty m_SettingsProperty;
        private SerializedProperty m_DiffusionProfilesProperty;
        private SerializedProperty m_RenderPassEventProperty;
        private SerializedProperty m_LayerMaskProperty;
        
        // Quality settings
        private SerializedProperty m_SampleBudgetProperty;
        private SerializedProperty m_DownsampleStepsProperty;
        private SerializedProperty m_SubsurfaceScatteringAttenuationProperty;
        
        private SerializedProperty m_globalDetailPreservationProperty;
        
        // Compute shaders
        private SerializedProperty m_SubsurfaceScatteringCSProperty;
        private SerializedProperty m_SubsurfaceScatteringDownsampleCSProperty;
        private SerializedProperty m_ResolveStencilCSProperty;
        
        // Shaders
        private SerializedProperty m_CombineLightingShaderProperty;

        // Foldout states
        private bool m_ProfilesFoldout = true;
        private bool m_QualityFoldout = true;
        private bool m_ShadersFoldout = true;
        
        private bool m_IsInitialized = false;

        private void OnEnable()
        {
            Init();
        }
        
        private void Init()
        {
            m_SettingsProperty = serializedObject.FindProperty("settings");
            
            // Basic settings
            m_DiffusionProfilesProperty = m_SettingsProperty.FindPropertyRelative("diffusionProfiles");
            m_RenderPassEventProperty = m_SettingsProperty.FindPropertyRelative("renderPassEvent");
            m_LayerMaskProperty = m_SettingsProperty.FindPropertyRelative("layerMask");
            
            // Quality settings
            m_SampleBudgetProperty = m_SettingsProperty.FindPropertyRelative("sampleBudget");
            m_DownsampleStepsProperty = m_SettingsProperty.FindPropertyRelative("downsampleSteps");
            m_SubsurfaceScatteringAttenuationProperty = m_SettingsProperty.FindPropertyRelative("subsurfaceScatteringAttenuation");
            
            m_globalDetailPreservationProperty = m_SettingsProperty.FindPropertyRelative("globalDetailPreservation");
            
            // Compute shaders
            m_SubsurfaceScatteringCSProperty = m_SettingsProperty.FindPropertyRelative("subsurfaceScatteringCS");
            m_SubsurfaceScatteringDownsampleCSProperty = m_SettingsProperty.FindPropertyRelative("subsurfaceScatteringDownsampleCS");
            m_ResolveStencilCSProperty = m_SettingsProperty.FindPropertyRelative("resolveStencilCS");
            
            // Shaders
            m_CombineLightingShaderProperty = m_SettingsProperty.FindPropertyRelative("combineLightingShader");
            
            m_IsInitialized = true;
        }

        public override void OnInspectorGUI()
        {
            if (!m_IsInitialized)
            {
                Init();
            }

            serializedObject.Update();

            EditorGUILayout.Space(5);
            
            // Diffusion Profiles foldout
            m_ProfilesFoldout = EditorGUILayout.Foldout(m_ProfilesFoldout, "Diffusion Profiles", true, EditorStyles.foldoutHeader);
            
            if (m_ProfilesFoldout)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.HelpBox(
                    "Add Diffusion Profiles to this list.\n" +
                    "Use 'Auto Collect' button to find all profiles in project automatically.",
                    MessageType.Info
                );

                EditorGUILayout.Space(5);

                // Auto collect button
                if (GUILayout.Button("🔍 Auto Collect All Profiles in Project", GUILayout.Height(35)))
                {
                    AutoCollectProfiles();
                }

                EditorGUILayout.Space(5);

                // Profile array
                EditorGUILayout.PropertyField(m_DiffusionProfilesProperty, new GUIContent("Profiles"), true);

                // Show count
                if (m_DiffusionProfilesProperty.arraySize > 0)
                {
                    EditorGUILayout.Space(3);
                    int maxCount = DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT;
                    string countText = $"Count: {m_DiffusionProfilesProperty.arraySize} / {maxCount}";
                    
                    if (m_DiffusionProfilesProperty.arraySize > maxCount)
                    {
                        EditorGUILayout.HelpBox(
                            $"⚠️ Too many profiles! Maximum: {maxCount}\nExtra profiles will be ignored.",
                            MessageType.Warning
                        );
                    }
                    else
                    {
                        EditorGUILayout.LabelField(countText, EditorStyles.miniLabel);
                    }
                }
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);
            
            // Rendering Settings
            EditorGUILayout.LabelField("Rendering Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            EditorGUILayout.PropertyField(m_RenderPassEventProperty, new GUIContent("Render Pass Event"));
            EditorGUILayout.PropertyField(m_LayerMaskProperty, new GUIContent("Layer Mask"));

            EditorGUILayout.Space(10);
            
            // Quality Settings Foldout
            m_QualityFoldout = EditorGUILayout.Foldout(m_QualityFoldout, "Quality Settings", true, EditorStyles.foldoutHeader);
            if (m_QualityFoldout)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(m_SampleBudgetProperty, new GUIContent("Sample Budget", "Sample budget for SSS filtering (higher = better quality, slower)"));
                EditorGUILayout.PropertyField(m_DownsampleStepsProperty, new GUIContent("Downsample Steps", "Downsample steps (0 = full resolution, 1 = half, 2 = quarter)"));
                EditorGUILayout.PropertyField(m_SubsurfaceScatteringAttenuationProperty, new GUIContent("SSS Attenuation", "Enable SubSurface-Scattering occlusion computation. Enabling this makes the SSS slightly more expensive but add great details to occluded zones with SSS materials."));
                EditorGUILayout.PropertyField(m_globalDetailPreservationProperty, new GUIContent("Global Detail Preservation", "Global detail preservation for SSS."));
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);
            
            // Auto find missing shaders (silently, like GTAO)
            if (HasMissingShaders())
            {
                AutoFindShaders();
            }
            
            // Shaders Foldout (unified: compute shaders + regular shaders)
            m_ShadersFoldout = EditorGUILayout.Foldout(m_ShadersFoldout, "Shaders", true, EditorStyles.foldoutHeader);
            if (m_ShadersFoldout)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(m_SubsurfaceScatteringCSProperty, new GUIContent("Subsurface Scattering", "Main SSS compute shader"));
                EditorGUILayout.PropertyField(m_SubsurfaceScatteringDownsampleCSProperty, new GUIContent("SSS Downsample", "Downsample compute shader"));
                EditorGUILayout.PropertyField(m_ResolveStencilCSProperty, new GUIContent("Resolve Stencil", "Resolve stencil compute shader (for coarse stencil optimization)"));
                EditorGUILayout.PropertyField(m_CombineLightingShaderProperty, new GUIContent("Combine Lighting", "Combine lighting shader (additive blend of SSS filtered diffuse with color buffer)"));
                
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Find and assign shaders from the project (silently, like GTAO)
        /// </summary>
        private void AutoFindShaders()
        {
            const string packageShadersPath = "Packages/com.baddog.rendering.subsurfacescattering/Shaders/";
            
            TryLoadComputeShader(m_SubsurfaceScatteringCSProperty, packageShadersPath + "SubsurfaceScattering.compute", "Subsurface Scattering");
            TryLoadComputeShader(m_SubsurfaceScatteringDownsampleCSProperty, packageShadersPath + "SubsurfaceScatteringDownsample.compute", "SSS Downsample");
            TryLoadComputeShader(m_ResolveStencilCSProperty, packageShadersPath + "ResolveStencilBuffer.compute", "Resolve Stencil");
            TryLoadShader(m_CombineLightingShaderProperty, packageShadersPath + "CombineLighting.shader", "Combine Lighting");
        }
        
        /// <summary>
        /// Helper method to try loading a compute shader if it's null
        /// </summary>
        private void TryLoadComputeShader(SerializedProperty shaderRef, string path, string displayName)
        {
            if (shaderRef.objectReferenceValue == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                if (shader != null)
                {
                    shaderRef.objectReferenceValue = shader;
                }
            }
        }
        
        /// <summary>
        /// Helper method to try loading a shader if it's null
        /// </summary>
        private void TryLoadShader(SerializedProperty shaderRef, string path, string displayName)
        {
            if (shaderRef.objectReferenceValue == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader != null)
                {
                    shaderRef.objectReferenceValue = shader;
                }
            }
        }
        
        /// <summary>
        /// Check if any required shaders are missing
        /// </summary>
        private bool HasMissingShaders()
        {
            return m_SubsurfaceScatteringCSProperty.objectReferenceValue == null || 
                   m_SubsurfaceScatteringDownsampleCSProperty.objectReferenceValue == null || 
                   m_ResolveStencilCSProperty.objectReferenceValue == null ||
                   m_CombineLightingShaderProperty.objectReferenceValue == null;
        }

        /// <summary>
        /// Auto-collect all DiffusionProfileSettings in the project
        /// Maintains existing profile order for index stability
        /// </summary>
        private void AutoCollectProfiles()
        {
            // Find all DiffusionProfileSettings assets
            string[] guids = AssetDatabase.FindAssets("t:DiffusionProfileSettings");
            HashSet<DiffusionProfileSettings> foundProfiles = new HashSet<DiffusionProfileSettings>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(path);
                if (profile != null)
                {
                    foundProfiles.Add(profile);
                }
            }

            if (foundProfiles.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Profiles Found",
                    "No Diffusion Profile Settings found in the project.\n\n" +
                    "Create one via: Create > BadDog > Rendering > Diffusion Profile Settings",
                    "OK"
                );
                return;
            }

            // Build new list: keep existing profiles in order, add new ones at the end
            List<DiffusionProfileSettings> newList = new List<DiffusionProfileSettings>();
            
            // Step 1: Keep existing profiles (if they still exist)
            int existingCount = m_DiffusionProfilesProperty.arraySize;
            for (int i = 0; i < existingCount; i++)
            {
                var existingProfile = m_DiffusionProfilesProperty.GetArrayElementAtIndex(i).objectReferenceValue as DiffusionProfileSettings;
                if (existingProfile != null && foundProfiles.Contains(existingProfile))
                {
                    newList.Add(existingProfile);
                    foundProfiles.Remove(existingProfile); // Mark as processed
                }
            }

            // Step 2: Add new profiles (sorted by name for consistency)
            var newProfiles = foundProfiles.OrderBy(p => p.name).ToList();
            newList.AddRange(newProfiles);

            // Check limit
            int maxCount = DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT;
            if (newList.Count > maxCount)
            {
                if (!EditorUtility.DisplayDialog(
                    "Too Many Profiles",
                    $"Found {newList.Count} profiles, but only {maxCount} can be used.\n\n" +
                    $"Do you want to keep the first {maxCount} profiles?",
                    "Keep", "Cancel"))
                {
                    return;
                }

                newList = newList.Take(maxCount).ToList();
            }

            // Set array
            m_DiffusionProfilesProperty.arraySize = newList.Count;
            for (int i = 0; i < newList.Count; i++)
            {
                m_DiffusionProfilesProperty.GetArrayElementAtIndex(i).objectReferenceValue = newList[i];
            }

            serializedObject.ApplyModifiedProperties();

            string message = $"Successfully collected {newList.Count} Diffusion Profiles.\n\n" +
                           $"Existing: {existingCount - newProfiles.Count}, New: {newProfiles.Count}";

            EditorUtility.DisplayDialog("Profiles Collected", message, "OK");

            Debug.Log($"[SSS] Auto-collected {newList.Count} profiles (kept {existingCount - newProfiles.Count} existing, added {newProfiles.Count} new)");
        }
    }
}

