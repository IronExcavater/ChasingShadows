using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IronByte.Tools.MaterialConversion.Editor
{
    [InitializeOnLoad]
    internal static class MaterialConversionInspectorHook
    {
        static MaterialConversionInspectorHook()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
        }

        private static void OnFinishedDefaultHeaderGUI(UnityEditor.Editor editor)
        {
            if (editor.targets == null || editor.targets.Length == 0 || !editor.targets.All(target => target is Material))
            {
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Convert Shader...", EditorStyles.miniButton))
            {
                MaterialConversionWindow.OpenWithMaterials(editor.targets.Cast<Material>());
            }

            GUILayout.EndHorizontal();
        }
    }
}
