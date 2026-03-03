using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Linq;

namespace SoulRender
{
    /// <summary>
    /// Monitors DiffusionProfileSettings asset changes and auto-updates RenderFeatures
    /// </summary>
    internal class DiffusionProfileAssetProcessor : AssetPostprocessor
    {
        // Track if we're already processing to avoid recursion
        private static bool s_IsProcessing = false;
         
        /// <summary>
        /// Called after assets are imported, deleted, or moved
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            
            /*
            if (s_IsProcessing)
            {
                return;
            }

            bool profileChanged = false;

            // Check if any DiffusionProfileSettings were affected
            foreach (string assetPath in importedAssets)
            {
                if (assetPath.EndsWith(".asset"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(assetPath);
                    if (asset != null)
                    {
                        profileChanged = true;
                        Debug.Log($"[SSS] Profile created/imported: {asset.name}");
                        break;
                    }
                }
            }

            // Check deleted assets
            // Note: We can't load deleted assets, so we check all .asset deletions
            // The update logic will determine if it was actually a profile
            if (!profileChanged)
            {
                foreach (string assetPath in deletedAssets)
                {
                    if (assetPath.EndsWith(".asset"))
                    {
                        profileChanged = true;
                        Debug.Log($"[SSS] Asset deleted, checking if profile: {assetPath}");
                        break;
                    }
                }
            }

            // Check moved assets (both new and old paths)
            if (!profileChanged)
            {
                // Check new paths
                foreach (string assetPath in movedAssets)
                {
                    if (assetPath.EndsWith(".asset"))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(assetPath);
                        if (asset != null)
                        {
                            profileChanged = true;
                            Debug.Log($"[SSS] Profile moved to: {asset.name}");
                            break;
                        }
                    }
                }
            }

            if (!profileChanged)
            {
                // Check old paths (in case profile was moved out of project or renamed)
                foreach (string assetPath in movedFromAssetPaths)
                {
                    if (assetPath.EndsWith(".asset"))
                    {
                        // Can't load old path, but if any .asset moved, worth checking
                        profileChanged = true;
                        Debug.Log($"[SSS] Asset moved from: {assetPath}");
                        break;
                    }
                }
            }

            // If profiles changed, auto-update all RenderFeatures
            if (profileChanged)
            {
                EditorApplication.delayCall += () =>
                {
                    if (!s_IsProcessing)
                    {
                        AutoUpdateAllRenderFeatures();
                    }
                };
            }
            
            */
        }
        

        /// <summary>
        /// Find all SubsurfaceScattering RenderFeatures and update their profile lists
        /// </summary>
        private static void AutoUpdateAllRenderFeatures()
        {
            s_IsProcessing = true;

            try
            {
                // Find all UniversalRendererData assets
                string[] guids = AssetDatabase.FindAssets("t:ScriptableRendererData");
                int updatedCount = 0;

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                    
                    if (rendererData == null)
                    {
                        continue;
                    }

                    // Use SerializedObject to access the features list
                    SerializedObject serializedRenderer = new SerializedObject(rendererData);
                    SerializedProperty featuresProperty = serializedRenderer.FindProperty("m_RendererFeatures");

                    if (featuresProperty == null || !featuresProperty.isArray)
                    {
                        continue;
                    }

                    bool rendererUpdated = false;

                    // Check each feature
                    for (int i = 0; i < featuresProperty.arraySize; i++)
                    {
                        SerializedProperty featureProperty = featuresProperty.GetArrayElementAtIndex(i);
                        var feature = featureProperty.objectReferenceValue as SubsurfaceScattering;

                        if (feature != null)
                        {
                            if (AutoUpdateRenderFeature(feature))
                            {
                                rendererUpdated = true;
                                updatedCount++;
                            }
                        }
                    }

                    if (rendererUpdated)
                    {
                        serializedRenderer.ApplyModifiedProperties();
                        EditorUtility.SetDirty(rendererData);
                    }
                }

                if (updatedCount > 0)
                {
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[SSS] Auto-updated {updatedCount} RenderFeature(s) with latest profiles");
                }
            }
            finally
            {
                s_IsProcessing = false;
            }
        }

        /// <summary>
        /// Update a single RenderFeature's profile list
        /// </summary>
        private static bool AutoUpdateRenderFeature(SubsurfaceScattering renderFeature)
        {
            SerializedObject serializedFeature = new SerializedObject(renderFeature);
            SerializedProperty settingsProperty = serializedFeature.FindProperty("settings");
            SerializedProperty profilesProperty = settingsProperty.FindPropertyRelative("diffusionProfiles");

            if (profilesProperty == null)
            {
                return false;
            }

            // Find all profiles in project
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

            // Store total count before modifying the set
            int totalFoundCount = foundProfiles.Count;

            // Build new list: keep existing profiles in order, add new ones at the end
            List<DiffusionProfileSettings> newList = new List<DiffusionProfileSettings>();

            // Step 1: Keep existing profiles (if they still exist)
            int existingCount = profilesProperty.arraySize;
            for (int i = 0; i < existingCount; i++)
            {
                var existingProfile = profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue as DiffusionProfileSettings;
                if (existingProfile != null && foundProfiles.Contains(existingProfile))
                {
                    newList.Add(existingProfile);
                    foundProfiles.Remove(existingProfile); // Remove to avoid duplicates
                }
            }

            // Step 2: Add new profiles (sorted by name)
            var newProfiles = foundProfiles.OrderBy(p => p.name).ToList();
            newList.AddRange(newProfiles);

            // Check if list actually changed
            bool changed = newList.Count != existingCount;
            if (!changed)
            {
                for (int i = 0; i < newList.Count; i++)
                {
                    var current = profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue as DiffusionProfileSettings;
                    if (current != newList[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
            {
                return false;
            }

            // Limit check
            int maxCount = DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT;
            if (newList.Count > maxCount)
            {
                Debug.LogWarning($"[SSS] Too many profiles ({totalFoundCount}), limiting to {maxCount}");
                newList = newList.Take(maxCount).ToList();
            }

            // Update array
            profilesProperty.arraySize = newList.Count;
            for (int i = 0; i < newList.Count; i++)
            {
                profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue = newList[i];
            }

            serializedFeature.ApplyModifiedProperties();
            return true;
        }
    }
}

