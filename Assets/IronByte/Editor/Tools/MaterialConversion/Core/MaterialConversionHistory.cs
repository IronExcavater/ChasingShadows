using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace IronByte.Tools.MaterialConversion.Editor
{
    internal sealed class MaterialConversionHistoryEntry
    {
        public string Label { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public MaterialConversionResult[] Results { get; set; } = Array.Empty<MaterialConversionResult>();
        public int RemappedAssetCount { get; set; }
        public AssetSnapshotChange[] AssetChanges { get; set; } = Array.Empty<AssetSnapshotChange>();

        public bool HasChanges => AssetChanges.Length > 0;

        public void Undo()
        {
            ApplySnapshots(useAfter: false);
        }

        public void Redo()
        {
            ApplySnapshots(useAfter: true);
        }

        private void ApplySnapshots(bool useAfter)
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                IEnumerable<AssetSnapshotChange> orderedChanges = useAfter
                    ? AssetChanges.OrderBy(change => change.AssetPath, StringComparer.Ordinal)
                    : AssetChanges.OrderByDescending(change => change.AssetPath, StringComparer.Ordinal);

                foreach (AssetSnapshotChange change in orderedChanges)
                {
                    AssetFileSnapshot snapshot = useAfter ? change.After : change.Before;
                    MaterialConversionHistoryUtility.ApplySnapshot(change.AssetPath, snapshot);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }
    }

    internal sealed class AssetSnapshotChange
    {
        public string AssetPath { get; set; } = string.Empty;
        public AssetFileSnapshot Before { get; set; } = AssetFileSnapshot.Missing();
        public AssetFileSnapshot After { get; set; } = AssetFileSnapshot.Missing();
    }

    internal sealed class AssetFileSnapshot
    {
        public bool Exists { get; private set; }
        public byte[] Data { get; private set; } = Array.Empty<byte>();
        public bool MetaExists { get; private set; }
        public byte[] MetaData { get; private set; } = Array.Empty<byte>();

        public static AssetFileSnapshot Missing()
        {
            return new AssetFileSnapshot();
        }

        public static AssetFileSnapshot Capture(string assetPath)
        {
            string fileSystemPath = MaterialConversionHistoryUtility.AssetPathToFileSystemPath(assetPath);
            string metaPath = fileSystemPath + ".meta";
            return new AssetFileSnapshot
            {
                Exists = File.Exists(fileSystemPath),
                Data = File.Exists(fileSystemPath) ? File.ReadAllBytes(fileSystemPath) : Array.Empty<byte>(),
                MetaExists = File.Exists(metaPath),
                MetaData = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : Array.Empty<byte>()
            };
        }

        public static AssetFileSnapshot FromText(string assetPath, string text)
        {
            AssetFileSnapshot current = Capture(assetPath);
            return new AssetFileSnapshot
            {
                Exists = true,
                Data = new UTF8Encoding(false).GetBytes(text ?? string.Empty),
                MetaExists = current.MetaExists,
                MetaData = current.MetaData ?? Array.Empty<byte>()
            };
        }

        public static bool AreEqual(AssetFileSnapshot left, AssetFileSnapshot right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.Exists == right.Exists &&
                   left.MetaExists == right.MetaExists &&
                   ByteArraysEqual(left.Data, right.Data) &&
                   ByteArraysEqual(left.MetaData, right.MetaData);
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal static class MaterialConversionHistoryUtility
    {
        internal static Dictionary<string, AssetFileSnapshot> CaptureSnapshots(IEnumerable<string> assetPaths)
        {
            Dictionary<string, AssetFileSnapshot> snapshots = new Dictionary<string, AssetFileSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (string assetPath in assetPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                snapshots[assetPath] = AssetFileSnapshot.Capture(assetPath);
            }

            return snapshots;
        }

        internal static MaterialConversionHistoryEntry CreateEntry(
            string label,
            IReadOnlyList<MaterialConversionResult> results,
            MaterialReferenceRemapResult remapResult,
            IReadOnlyDictionary<string, AssetFileSnapshot> beforeSnapshots)
        {
            List<AssetSnapshotChange> changes = new List<AssetSnapshotChange>();
            HashSet<string> trackedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (MaterialConversionResult result in results)
            {
                TrackAsset(result.SourcePath, result.ResultPath == result.SourcePath);
                TrackAsset(result.ResultPath, result.ResultPath != result.SourcePath);

                foreach (string generatedPath in result.GeneratedAssets)
                {
                    TrackAsset(generatedPath, true);
                }
            }

            if (remapResult != null)
            {
                foreach (MaterialReferenceRemapChange change in remapResult.Changes)
                {
                    AssetFileSnapshot before = AssetFileSnapshot.FromText(change.AssetPath, change.BeforeText);
                    AssetFileSnapshot after = AssetFileSnapshot.FromText(change.AssetPath, change.AfterText);
                    AddOrReplace(change.AssetPath, before, after);
                }
            }

            return new MaterialConversionHistoryEntry
            {
                Label = label,
                Timestamp = DateTime.Now,
                Results = results.ToArray(),
                RemappedAssetCount = remapResult?.UpdatedAssetCount ?? 0,
                AssetChanges = changes.ToArray()
            };

            void TrackAsset(string assetPath, bool include)
            {
                if (!include || string.IsNullOrWhiteSpace(assetPath))
                {
                    return;
                }

                if (!trackedPaths.Add(assetPath))
                {
                    return;
                }

                beforeSnapshots.TryGetValue(assetPath, out AssetFileSnapshot before);
                before ??= AssetFileSnapshot.Missing();
                AssetFileSnapshot after = AssetFileSnapshot.Capture(assetPath);
                if (!AssetFileSnapshot.AreEqual(before, after))
                {
                    changes.Add(new AssetSnapshotChange
                    {
                        AssetPath = assetPath,
                        Before = before,
                        After = after
                    });
                }
            }

            void AddOrReplace(string assetPath, AssetFileSnapshot before, AssetFileSnapshot after)
            {
                int existingIndex = changes.FindIndex(change => string.Equals(change.AssetPath, assetPath, StringComparison.OrdinalIgnoreCase));
                AssetSnapshotChange snapshotChange = new AssetSnapshotChange
                {
                    AssetPath = assetPath,
                    Before = before,
                    After = after
                };

                if (existingIndex >= 0)
                {
                    changes[existingIndex] = snapshotChange;
                }
                else
                {
                    changes.Add(snapshotChange);
                }
            }
        }

        internal static string BuildEntryLabel(MaterialConversionMode mode, MaterialConversionTarget target, int materialCount)
        {
            string modeLabel = mode == MaterialConversionMode.Copy ? "Copy" : "Replace";
            return $"{modeLabel} {materialCount} to {MaterialConversionPresentation.GetTargetDisplayName(target)}";
        }

        internal static string AssetPathToFileSystemPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? throw new InvalidOperationException("Unable to resolve the Unity project root.");
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }

        internal static void ApplySnapshot(string assetPath, AssetFileSnapshot snapshot)
        {
            string fileSystemPath = AssetPathToFileSystemPath(assetPath);
            string metaPath = fileSystemPath + ".meta";

            if (snapshot.Exists)
            {
                string directory = Path.GetDirectoryName(fileSystemPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(fileSystemPath, snapshot.Data ?? Array.Empty<byte>());
            }
            else if (File.Exists(fileSystemPath))
            {
                File.Delete(fileSystemPath);
            }

            if (snapshot.MetaExists)
            {
                string metaDirectory = Path.GetDirectoryName(metaPath);
                if (!string.IsNullOrEmpty(metaDirectory))
                {
                    Directory.CreateDirectory(metaDirectory);
                }

                File.WriteAllBytes(metaPath, snapshot.MetaData ?? Array.Empty<byte>());
            }
            else if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
    }
}
