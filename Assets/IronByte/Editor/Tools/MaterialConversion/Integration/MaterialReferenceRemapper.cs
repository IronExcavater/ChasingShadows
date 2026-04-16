using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronByte.Tools.MaterialConversion.Editor
{
    internal sealed class MaterialReferenceRemapChange
    {
        public string AssetPath { get; set; } = string.Empty;
        public string BeforeText { get; set; } = string.Empty;
        public string AfterText { get; set; } = string.Empty;
    }

    internal sealed class MaterialReferenceRemapResult
    {
        public int UpdatedAssetCount { get; set; }
        public string[] Notes { get; set; } = Array.Empty<string>();
        public MaterialReferenceRemapChange[] Changes { get; set; } = Array.Empty<MaterialReferenceRemapChange>();
    }

    internal static class MaterialReferenceRemapper
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".anim",
            ".asset",
            ".controller",
            ".guiskin",
            ".mask",
            ".mat",
            ".overrideController",
            ".playable",
            ".prefab",
            ".unity"
        };

        internal static bool CanRemapProjectReferences(out string reason)
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                reason = "Reference remapping only works when Asset Serialization is set to Force Text.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal static int RemapProjectReferences(IReadOnlyDictionary<Material, Material> materialMap, List<string> notes)
        {
            MaterialReferenceRemapResult result = RemapProjectReferences(materialMap);
            if (notes != null)
            {
                notes.AddRange(result.Notes);
            }

            return result.UpdatedAssetCount;
        }

        internal static MaterialReferenceRemapResult RemapProjectReferences(IReadOnlyDictionary<Material, Material> materialMap)
        {
            List<string> notes = new List<string>();
            if (materialMap == null || materialMap.Count == 0)
            {
                return new MaterialReferenceRemapResult();
            }

            if (!CanRemapProjectReferences(out string reason))
            {
                notes.Add(reason);
                return new MaterialReferenceRemapResult { Notes = notes.ToArray() };
            }

            Dictionary<string, string> guidMap = materialMap
                .Where(pair => pair.Key != null && pair.Value != null)
                .ToDictionary(
                    pair => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Key)),
                    pair => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Value)));

            if (guidMap.Count == 0)
            {
                return new MaterialReferenceRemapResult();
            }

            List<MaterialReferenceRemapChange> changes = new List<MaterialReferenceRemapChange>();
            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? throw new InvalidOperationException("Unable to resolve the Unity project root.");

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string assetPath in AssetDatabase.GetAllAssetPaths())
                {
                    if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) || !SupportedExtensions.Contains(Path.GetExtension(assetPath)))
                    {
                        continue;
                    }

                    string fileSystemPath = Path.Combine(projectRoot, assetPath).Replace('\\', '/');
                    if (!File.Exists(fileSystemPath))
                    {
                        continue;
                    }

                    string fileText = File.ReadAllText(fileSystemPath);
                    string updatedText = ReplaceGuids(fileText, guidMap);
                    if (ReferenceEquals(fileText, updatedText) || fileText == updatedText)
                    {
                        continue;
                    }

                    File.WriteAllText(fileSystemPath, updatedText, new UTF8Encoding(false));
                    changes.Add(new MaterialReferenceRemapChange
                    {
                        AssetPath = assetPath,
                        BeforeText = fileText,
                        AfterText = updatedText
                    });
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            if (changes.Count > 0)
            {
                notes.Add($"Remapped project references in {changes.Count} asset file(s).");
            }

            return new MaterialReferenceRemapResult
            {
                UpdatedAssetCount = changes.Count,
                Notes = notes.ToArray(),
                Changes = changes.ToArray()
            };
        }

        private static string ReplaceGuids(string input, IReadOnlyDictionary<string, string> guidMap)
        {
            string output = input;
            foreach (KeyValuePair<string, string> pair in guidMap)
            {
                if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value) || pair.Key == pair.Value)
                {
                    continue;
                }

                if (output.Contains(pair.Key, StringComparison.Ordinal))
                {
                    output = output.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
                }
            }

            return output;
        }
    }
}
