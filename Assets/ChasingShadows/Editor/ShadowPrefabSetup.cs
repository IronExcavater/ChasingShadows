using System.IO;
using ChasingShadows.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChasingShadows.Editor
{
    public static class ShadowPrefabSetup
    {
        private const string ShadowPrefabPath = "Assets/ChasingShadows/Prefabs/Shadow.prefab";

        [MenuItem("ChasingShadows/Create Shadow Prefab from Joe")]
        public static void CreateShadowPrefab()
        {
            var joePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChasingShadows/Prefabs/Joe.prefab");
            if (joePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Joe.prefab not found at Assets/ChasingShadows/Prefabs/Joe.prefab", "OK");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(joePrefab);
            instance.name = "Shadow";

            // Remove components that shadow doesn't need
            RemoveComponentIfPresent<JoeCinematicController>(instance);

            // Disable all renderers visually — keep only shadow casting
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                r.receiveShadows = false;
            }

            // Make the shadow visually distinct — disable any lights/effects
            foreach (var light in instance.GetComponentsInChildren<Light>(true))
                light.enabled = false;

            // Save as new prefab
            Directory.CreateDirectory(Path.GetDirectoryName(ShadowPrefabPath)!);
            var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(instance, ShadowPrefabPath, InteractionMode.UserAction);
            Object.DestroyImmediate(instance);

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Done",
                $"Shadow prefab saved to {ShadowPrefabPath}\n\nAll renderers set to ShadowsOnly.",
                "OK");
            Selection.activeObject = saved;
        }

        [MenuItem("ChasingShadows/Configure Joe Renderers (No Shadow Cast)")]
        public static void ConfigureJoeRenderers()
        {
            var joe = Selection.activeGameObject;
            if (joe == null)
            {
                EditorUtility.DisplayDialog("Error", "Select Joe in the scene first.", "OK");
                return;
            }

            int count = 0;
            foreach (var r in joe.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                count++;
            }

            EditorUtility.DisplayDialog("Done", $"Set {count} renderers on '{joe.name}' to ShadowCastingMode.Off.", "OK");
        }

        private static void RemoveComponentIfPresent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) Object.DestroyImmediate(c);
        }
    }
}
