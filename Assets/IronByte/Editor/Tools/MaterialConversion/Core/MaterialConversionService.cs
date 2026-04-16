using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IronByte.Tools.MaterialConversion.Editor
{
    public static class MaterialConversionService
    {
        internal const string StandardShaderName = "Standard";
        internal const string StandardSpecularShaderName = "Standard (Specular setup)";
        internal const string BuiltInUnlitTextureShaderName = "Unlit/Texture";
        internal const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        internal const string UrpSimpleLitShaderName = "Universal Render Pipeline/Simple Lit";
        internal const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";
        internal const string UrpParticlesLitShaderName = "Universal Render Pipeline/Particles/Lit";
        internal const string UrpParticlesUnlitShaderName = "Universal Render Pipeline/Particles/Unlit";
        internal const string UrpTerrainShaderName = "Universal Render Pipeline/Terrain/Lit";
        internal const string HdrpLitShaderName = "HDRP/Lit";
        internal const string HdrpUnlitShaderName = "HDRP/Unlit";
        internal const string HdrpTerrainShaderName = "HDRP/TerrainLit";
        private const string ConvertedMaterialLabel = "MC_Converted";
        private const string SourceLabelPrefix = "MC_Source_";
        private const string TargetLabelPrefix = "MC_Target_";

        public static MaterialConversionResult Analyze(Material material, MaterialConversionTarget target)
        {
            return Analyze(new MaterialConversionRequest(material, target, MaterialConversionMode.Copy));
        }

        public static MaterialConversionResult Analyze(MaterialConversionRequest request)
        {
            if (request.SourceMaterial == null)
            {
                return UnsupportedResult(null, request.Target, MaterialSourceFamily.Unknown, "No material selected.");
            }

            string sourcePath = AssetDatabase.GetAssetPath(request.SourceMaterial);
            MaterialSourceFamily sourceFamily = DetectSourceFamily(request.SourceMaterial);
            List<string> notes = new List<string>();
            List<string> losses = new List<string>();
            string[] expectedGeneratedAssets = Array.Empty<string>();
            string[] expectedGeneratedAssetPaths = Array.Empty<string>();

            if (!TryResolveTargetShader(request.Target, out Shader targetShader, out string shaderName))
            {
                return UnsupportedResult(request.SourceMaterial, request.Target, sourceFamily, $"Target shader '{shaderName}' is not available in the project.", sourcePath);
            }

            if (MaterialUsesTargetShader(request.SourceMaterial, targetShader))
            {
                return SkippedResult(
                    request.SourceMaterial,
                    request.Target,
                    sourceFamily,
                    "Material already uses the selected target shader.",
                    sourcePath);
            }

            if (request.Mode == MaterialConversionMode.Copy &&
                TryFindExistingConvertedCopy(request.SourceMaterial, request.Target, request.CopySuffix, targetShader, out Material existingCopy, out string existingCopyPath))
            {
                return SkippedResult(
                    request.SourceMaterial,
                    request.Target,
                    sourceFamily,
                    $"A converted copy already exists at '{Path.GetFileNameWithoutExtension(existingCopyPath)}'.",
                    sourcePath,
                    existingCopy,
                    existingCopyPath);
            }

            bool isOfficial = IsOfficialConversion(sourceFamily, request.Target) &&
                              TryGetOfficialPipelineType(request.Target, out Type pipelineType) &&
                              MaterialUpgrader.FetchAllUpgradersForPipeline(pipelineType) is { Count: > 0 };

            bool hasSemanticData = MaterialConversionBackend.TryExtractSemanticData(
                request.SourceMaterial,
                sourceFamily,
                notes,
                out MaterialSemanticData semanticData,
                out bool heuristic,
                out string reason);

            if (!hasSemanticData && !isOfficial)
            {
                return UnsupportedResult(request.SourceMaterial, request.Target, sourceFamily, reason, sourcePath, notes);
            }

            if (hasSemanticData)
            {
                losses.AddRange(MaterialConversionBackend.CollectTargetLosses(semanticData, request.Target));
                expectedGeneratedAssets = MaterialConversionBackend.PredictGeneratedAssetNotes(semanticData, request.Target).Distinct().ToArray();
                expectedGeneratedAssetPaths = MaterialConversionBackend.PredictGeneratedAssetPaths(semanticData, request.Target).Distinct().ToArray();

                if (!request.AllowGeneratedHelperTextures && expectedGeneratedAssets.Length > 0)
                {
                    return UnsupportedResult(
                        request.SourceMaterial,
                        request.Target,
                        sourceFamily,
                        "Conversion needs helper textures, but helper texture generation is disabled.",
                        sourcePath,
                        notes,
                        losses,
                        expectedGeneratedAssets,
                        expectedGeneratedAssetPaths);
                }
            }

            MaterialConversionConfidence confidence = isOfficial
                ? MaterialConversionConfidence.Official
                : heuristic
                    ? MaterialConversionConfidence.Heuristic
                    : MaterialConversionConfidence.Mapped;

            string summary = confidence switch
            {
                MaterialConversionConfidence.Official => "Uses Unity's official material upgrader.",
                MaterialConversionConfidence.Heuristic => "Uses heuristic property matching.",
                _ => "Uses semantic property mapping."
            };

            (int strengthScore, string strengthLabel, string strengthSummary) = MaterialConversionPresentation.CalculateStrength(
                confidence,
                losses,
                notes,
                expectedGeneratedAssets.Length,
                true);

            return SuccessResult(
                request.SourceMaterial,
                request.Target,
                sourceFamily,
                confidence,
                summary,
                notes,
                losses,
                expectedGeneratedAssets,
                expectedGeneratedAssetPaths,
                sourcePath,
                strengthScore,
                strengthLabel,
                strengthSummary);
        }

        public static MaterialConversionResult Convert(MaterialConversionRequest request)
        {
            MaterialConversionResult analysis = Analyze(request);
            if (!analysis.Success || request.SourceMaterial == null)
            {
                return analysis;
            }

            request.GeneratedAssetPaths.Clear();

            List<string> notes = new List<string>(analysis.Notes);
            List<string> losses = new List<string>(analysis.Losses);
            string sourcePath = analysis.SourcePath;
            string resultPath = sourcePath;
            Material destinationMaterial = request.SourceMaterial;

            try
            {
                if (request.Mode == MaterialConversionMode.Copy)
                {
                    resultPath = GenerateCopyPath(sourcePath, request.CopySuffix);
                    if (analysis.Confidence == MaterialConversionConfidence.Official)
                    {
                        AssetDatabase.CopyAsset(sourcePath, resultPath);
                        destinationMaterial = AssetDatabase.LoadAssetAtPath<Material>(resultPath);
                    }
                    else
                    {
                        TryResolveTargetShader(request.Target, out Shader targetShader, out string _);
                        destinationMaterial = new Material(targetShader)
                        {
                            name = Path.GetFileNameWithoutExtension(resultPath)
                        };
                        AssetDatabase.CreateAsset(destinationMaterial, resultPath);
                    }
                }
                else
                {
                    Undo.RecordObject(destinationMaterial, "Convert Material Shader");
                }

                if (analysis.Confidence == MaterialConversionConfidence.Official)
                {
                    if (!TryRunOfficialUpgrade(destinationMaterial, request.Target, out string message))
                    {
                        notes.Add(message);
                        return UnsupportedResult(request.SourceMaterial, request.Target, analysis.SourceFamily, "Official conversion failed.", sourcePath, notes, losses, analysis.ExpectedGeneratedAssets, analysis.ExpectedGeneratedAssetPaths);
                    }

                    MaterialConversionBackend.PostApplyKeywords(destinationMaterial, request.Target);
                }
                else
                {
                    if (!MaterialConversionBackend.TryExtractSemanticData(request.SourceMaterial, analysis.SourceFamily, notes, out MaterialSemanticData semanticData, out _, out string reason))
                    {
                        return UnsupportedResult(request.SourceMaterial, request.Target, analysis.SourceFamily, reason, sourcePath, notes, losses, analysis.ExpectedGeneratedAssets, analysis.ExpectedGeneratedAssetPaths);
                    }

                    MaterialConversionBackend.WriteSemanticData(destinationMaterial, request.Target, semanticData, request, notes);
                }

                StampConversionMetadata(destinationMaterial, request.SourceMaterial, request.Target);
                EditorUtility.SetDirty(destinationMaterial);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(resultPath, ImportAssetOptions.ForceUpdate);

                return new MaterialConversionResult
                {
                    Success = true,
                    SourceMaterial = request.SourceMaterial,
                    ResultMaterial = destinationMaterial,
                    SourcePath = sourcePath,
                    ResultPath = resultPath,
                    SourceFamily = analysis.SourceFamily,
                    Target = request.Target,
                    Confidence = analysis.Confidence,
                    Summary = request.Mode == MaterialConversionMode.Copy ? "Created converted material copy." : "Converted material in place.",
                    Notes = notes.Distinct().ToArray(),
                    Losses = losses.Distinct().ToArray(),
                    ExpectedGeneratedAssets = analysis.ExpectedGeneratedAssets,
                    ExpectedGeneratedAssetPaths = analysis.ExpectedGeneratedAssetPaths,
                    GeneratedAssets = request.GeneratedAssetPaths.Distinct().ToArray(),
                    StrengthScore = analysis.StrengthScore,
                    StrengthLabel = analysis.StrengthLabel,
                    StrengthSummary = analysis.StrengthSummary,
                    Skipped = false
                };
            }
            catch (Exception ex)
            {
                notes.Add(ex.Message);
                return UnsupportedResult(request.SourceMaterial, request.Target, analysis.SourceFamily, "Material conversion threw an exception.", sourcePath, notes, losses, analysis.ExpectedGeneratedAssets, analysis.ExpectedGeneratedAssetPaths);
            }
        }

        public static IEnumerable<MaterialConversionResult> ConvertInPlace(IEnumerable<Material> materials, MaterialConversionTarget target)
        {
            foreach (Material material in materials.Where(material => material != null))
            {
                yield return Convert(new MaterialConversionRequest(material, target, MaterialConversionMode.Replace));
            }
        }

        internal static MaterialSourceFamily DetectSourceFamily(Material material)
        {
            string shaderName = material.shader != null ? material.shader.name : string.Empty;
            return shaderName switch
            {
                StandardShaderName => MaterialSourceFamily.BuiltInStandard,
                StandardSpecularShaderName => MaterialSourceFamily.BuiltInStandardSpecular,
                BuiltInUnlitTextureShaderName => MaterialSourceFamily.BuiltInLegacyUnlit,
                UrpLitShaderName => MaterialSourceFamily.URPLit,
                UrpSimpleLitShaderName => MaterialSourceFamily.URPSimpleLit,
                UrpUnlitShaderName => MaterialSourceFamily.URPUnlit,
                UrpParticlesLitShaderName => MaterialSourceFamily.URPParticlesLit,
                UrpParticlesUnlitShaderName => MaterialSourceFamily.URPParticlesUnlit,
                UrpTerrainShaderName => MaterialSourceFamily.URPTerrainLit,
                HdrpLitShaderName => MaterialSourceFamily.HDRPLit,
                HdrpUnlitShaderName => MaterialSourceFamily.HDRPUnlit,
                HdrpTerrainShaderName => MaterialSourceFamily.HDRPTerrainLit,
                _ => DetectFallbackSourceFamily(shaderName)
            };
        }

        internal static bool TryResolveTargetShader(MaterialConversionTarget target, out Shader shader, out string shaderName)
        {
            shaderName = GetTargetShaderName(target);
            shader = Shader.Find(shaderName);
            return shader != null;
        }

        internal static string GetTargetShaderName(MaterialConversionTarget target)
        {
            return target switch
            {
                MaterialConversionTarget.BuiltInStandard => StandardShaderName,
                MaterialConversionTarget.BuiltInStandardSpecular => StandardSpecularShaderName,
                MaterialConversionTarget.BuiltInUnlitTexture => BuiltInUnlitTextureShaderName,
                MaterialConversionTarget.URPLit => UrpLitShaderName,
                MaterialConversionTarget.URPSimpleLit => UrpSimpleLitShaderName,
                MaterialConversionTarget.URPUnlit => UrpUnlitShaderName,
                MaterialConversionTarget.URPParticlesLit => UrpParticlesLitShaderName,
                MaterialConversionTarget.URPParticlesUnlit => UrpParticlesUnlitShaderName,
                MaterialConversionTarget.URPTerrainLit => UrpTerrainShaderName,
                MaterialConversionTarget.HDRPLit => HdrpLitShaderName,
                MaterialConversionTarget.HDRPUnlit => HdrpUnlitShaderName,
                MaterialConversionTarget.HDRPTerrainLit => HdrpTerrainShaderName,
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
            };
        }

        internal static bool IsTerrainSource(MaterialSourceFamily family)
        {
            return family == MaterialSourceFamily.BuiltInTerrain ||
                   family == MaterialSourceFamily.URPTerrainLit ||
                   family == MaterialSourceFamily.HDRPTerrainLit;
        }

        internal static bool IsTerrainTarget(MaterialConversionTarget target)
        {
            return target == MaterialConversionTarget.URPTerrainLit || target == MaterialConversionTarget.HDRPTerrainLit;
        }

        internal static bool IsUrpTarget(MaterialConversionTarget target)
        {
            return target == MaterialConversionTarget.URPLit ||
                   target == MaterialConversionTarget.URPSimpleLit ||
                   target == MaterialConversionTarget.URPUnlit ||
                   target == MaterialConversionTarget.URPParticlesLit ||
                   target == MaterialConversionTarget.URPParticlesUnlit ||
                   target == MaterialConversionTarget.URPTerrainLit;
        }

        internal static bool IsHdrpTarget(MaterialConversionTarget target)
        {
            return target == MaterialConversionTarget.HDRPLit ||
                   target == MaterialConversionTarget.HDRPUnlit ||
                   target == MaterialConversionTarget.HDRPTerrainLit;
        }

        private static MaterialConversionResult SuccessResult(
            Material material,
            MaterialConversionTarget target,
            MaterialSourceFamily sourceFamily,
            MaterialConversionConfidence confidence,
            string summary,
            IEnumerable<string> notes,
            IEnumerable<string> losses,
            IEnumerable<string> expectedGeneratedAssets,
            IEnumerable<string> expectedGeneratedAssetPaths,
            string sourcePath,
            int strengthScore,
            string strengthLabel,
            string strengthSummary)
        {
            return new MaterialConversionResult
            {
                Success = true,
                SourceMaterial = material,
                ResultMaterial = material,
                SourcePath = sourcePath,
                ResultPath = sourcePath,
                SourceFamily = sourceFamily,
                Target = target,
                Confidence = confidence,
                Summary = summary,
                Notes = notes.Distinct().ToArray(),
                Losses = losses.Distinct().ToArray(),
                ExpectedGeneratedAssets = expectedGeneratedAssets.Distinct().ToArray(),
                ExpectedGeneratedAssetPaths = expectedGeneratedAssetPaths.Distinct().ToArray(),
                StrengthScore = strengthScore,
                StrengthLabel = strengthLabel,
                StrengthSummary = strengthSummary,
                Skipped = false
            };
        }

        private static MaterialConversionResult SkippedResult(
            Material material,
            MaterialConversionTarget target,
            MaterialSourceFamily sourceFamily,
            string summary,
            string sourcePath = "",
            Material resultMaterial = null,
            string resultPath = "")
        {
            return new MaterialConversionResult
            {
                Success = false,
                Skipped = true,
                SourceMaterial = material,
                ResultMaterial = resultMaterial != null ? resultMaterial : material,
                SourcePath = sourcePath,
                ResultPath = string.IsNullOrWhiteSpace(resultPath) ? sourcePath : resultPath,
                SourceFamily = sourceFamily,
                Target = target,
                Confidence = MaterialConversionConfidence.Mapped,
                Summary = summary,
                StrengthScore = 0,
                StrengthLabel = "Skipped",
                StrengthSummary = summary
            };
        }

        private static MaterialConversionResult UnsupportedResult(
            Material material,
            MaterialConversionTarget target,
            MaterialSourceFamily sourceFamily,
            string summary,
            string sourcePath = "",
            IEnumerable<string> notes = null,
            IEnumerable<string> losses = null,
            IEnumerable<string> expectedGeneratedAssets = null,
            IEnumerable<string> expectedGeneratedAssetPaths = null)
        {
            return new MaterialConversionResult
            {
                Success = false,
                SourceMaterial = material,
                ResultMaterial = material,
                SourcePath = sourcePath,
                ResultPath = sourcePath,
                SourceFamily = sourceFamily,
                Target = target,
                Confidence = MaterialConversionConfidence.Unsupported,
                Summary = summary,
                Notes = notes == null ? Array.Empty<string>() : notes.Distinct().ToArray(),
                Losses = losses == null ? Array.Empty<string>() : losses.Distinct().ToArray(),
                ExpectedGeneratedAssets = expectedGeneratedAssets == null ? Array.Empty<string>() : expectedGeneratedAssets.Distinct().ToArray(),
                ExpectedGeneratedAssetPaths = expectedGeneratedAssetPaths == null ? Array.Empty<string>() : expectedGeneratedAssetPaths.Distinct().ToArray(),
                StrengthScore = 0,
                StrengthLabel = "Unsupported",
                StrengthSummary = summary,
                Skipped = false
            };
        }

        private static MaterialSourceFamily DetectFallbackSourceFamily(string shaderName)
        {
            if (shaderName.StartsWith("Legacy Shaders/", StringComparison.Ordinal))
            {
                if (shaderName.Contains("Particle", StringComparison.OrdinalIgnoreCase))
                {
                    return MaterialSourceFamily.BuiltInParticle;
                }

                if (shaderName.Contains("Terrain", StringComparison.OrdinalIgnoreCase))
                {
                    return MaterialSourceFamily.BuiltInTerrain;
                }

                if (shaderName.Contains("Self-Illumin", StringComparison.OrdinalIgnoreCase) ||
                    shaderName.Contains("Diffuse", StringComparison.OrdinalIgnoreCase) ||
                    shaderName.Contains("Specular", StringComparison.OrdinalIgnoreCase) ||
                    shaderName.Contains("VertexLit", StringComparison.OrdinalIgnoreCase) ||
                    shaderName.Contains("Bumped", StringComparison.OrdinalIgnoreCase))
                {
                    return MaterialSourceFamily.BuiltInLegacyLit;
                }

                return MaterialSourceFamily.BuiltInLegacyUnlit;
            }

            if (shaderName.StartsWith("Unlit/", StringComparison.Ordinal) ||
                shaderName.StartsWith("Mobile/Unlit", StringComparison.Ordinal))
            {
                return MaterialSourceFamily.BuiltInLegacyUnlit;
            }

            if (shaderName.StartsWith("Particles/", StringComparison.Ordinal))
            {
                return MaterialSourceFamily.BuiltInParticle;
            }

            if (shaderName.StartsWith("Nature/Terrain/", StringComparison.Ordinal))
            {
                return MaterialSourceFamily.BuiltInTerrain;
            }

            return MaterialSourceFamily.Custom;
        }

        private static string GenerateCopyPath(string sourcePath, string suffix)
        {
            string candidate = GetPreferredCopyPath(sourcePath, suffix);
            return AssetDatabase.GenerateUniqueAssetPath(candidate);
        }

        private static string GetPreferredCopyPath(string sourcePath, string suffix)
        {
            string directory = Path.GetDirectoryName(sourcePath) ?? "Assets";
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            string extension = Path.GetExtension(sourcePath);
            return Path.Combine(directory, name + suffix + extension).Replace('\\', '/');
        }

        private static bool IsOfficialConversion(MaterialSourceFamily family, MaterialConversionTarget target)
        {
            return IsBuiltInSource(family) && (IsUrpTarget(target) || IsHdrpTarget(target));
        }

        private static bool IsBuiltInSource(MaterialSourceFamily family)
        {
            return family == MaterialSourceFamily.BuiltInStandard ||
                   family == MaterialSourceFamily.BuiltInStandardSpecular ||
                   family == MaterialSourceFamily.BuiltInLegacyLit ||
                   family == MaterialSourceFamily.BuiltInLegacyUnlit ||
                   family == MaterialSourceFamily.BuiltInTerrain ||
                   family == MaterialSourceFamily.BuiltInParticle;
        }

        private static bool TryRunOfficialUpgrade(Material material, MaterialConversionTarget target, out string message)
        {
            message = string.Empty;
            if (!TryGetOfficialPipelineType(target, out Type pipelineType))
            {
                message = "Official target pipeline type is unavailable.";
                return false;
            }

            List<MaterialUpgrader> upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(pipelineType);
            if (upgraders == null || upgraders.Count == 0)
            {
                message = "No Unity material upgraders were registered for the selected pipeline.";
                return false;
            }

            return MaterialUpgrader.Upgrade(material, upgraders, MaterialUpgrader.UpgradeFlags.LogMessageWhenNoUpgraderFound, ref message);
        }

        private static bool TryGetOfficialPipelineType(MaterialConversionTarget target, out Type pipelineType)
        {
            pipelineType = null;
            if (IsUrpTarget(target))
            {
                pipelineType = typeof(UniversalRenderPipelineAsset);
                return true;
            }

            if (IsHdrpTarget(target))
            {
                pipelineType = Type.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, Unity.RenderPipelines.HighDefinition.Runtime");
                return pipelineType != null;
            }

            return false;
        }

        private static void StampConversionMetadata(Material material, Material sourceMaterial, MaterialConversionTarget target)
        {
            if (material == null)
            {
                return;
            }

            string sourcePath = sourceMaterial != null ? AssetDatabase.GetAssetPath(sourceMaterial) : string.Empty;
            string sourceGuid = string.IsNullOrWhiteSpace(sourcePath) ? string.Empty : AssetDatabase.AssetPathToGUID(sourcePath);

            List<string> labels = AssetDatabase.GetLabels(material)
                .Where(label => !string.Equals(label, ConvertedMaterialLabel, StringComparison.Ordinal) &&
                                !label.StartsWith(SourceLabelPrefix, StringComparison.Ordinal) &&
                                !label.StartsWith(TargetLabelPrefix, StringComparison.Ordinal))
                .ToList();

            labels.Add(ConvertedMaterialLabel);
            labels.Add(BuildTargetLabel(target));
            if (!string.IsNullOrWhiteSpace(sourceGuid))
            {
                labels.Add(BuildSourceLabel(sourceGuid));
            }

            AssetDatabase.SetLabels(material, labels.Distinct(StringComparer.Ordinal).OrderBy(label => label, StringComparer.Ordinal).ToArray());
        }

        private static bool TryFindExistingConvertedCopy(
            Material sourceMaterial,
            MaterialConversionTarget target,
            string suffix,
            Shader targetShader,
            out Material existingMaterial,
            out string existingPath)
        {
            existingMaterial = null;
            existingPath = string.Empty;

            string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            string preferredPath = GetPreferredCopyPath(sourcePath, suffix);
            if (TryLoadMatchingConvertedMaterial(preferredPath, target, targetShader, sourceGuid, true, out existingMaterial))
            {
                existingPath = preferredPath;
                return true;
            }

            string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(sourceGuid))
            {
                return false;
            }

            string[] candidates = AssetDatabase.FindAssets(
                $"l:{ConvertedMaterialLabel} l:{BuildTargetLabel(target)} l:{BuildSourceLabel(sourceGuid)}",
                new[] { directory });

            foreach (string guid in candidates)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(candidatePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryLoadMatchingConvertedMaterial(candidatePath, target, targetShader, sourceGuid, false, out existingMaterial))
                {
                    existingPath = candidatePath;
                    return true;
                }
            }

            return false;
        }

        private static bool TryLoadMatchingConvertedMaterial(
            string assetPath,
            MaterialConversionTarget target,
            Shader targetShader,
            string sourceGuid,
            bool allowLegacyPreferredPathFallback,
            out Material material)
        {
            material = string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null || !MaterialUsesTargetShader(material, targetShader))
            {
                return false;
            }

            string[] labels = AssetDatabase.GetLabels(material);
            bool metadataMatch = labels.Contains(ConvertedMaterialLabel) &&
                                 labels.Contains(BuildTargetLabel(target)) &&
                                 (string.IsNullOrWhiteSpace(sourceGuid) || labels.Contains(BuildSourceLabel(sourceGuid)));
            if (metadataMatch)
            {
                return true;
            }

            return allowLegacyPreferredPathFallback;
        }

        private static bool MaterialUsesTargetShader(Material material, Shader targetShader)
        {
            return material != null && targetShader != null && material.shader == targetShader;
        }

        private static string BuildSourceLabel(string sourceGuid)
        {
            return string.IsNullOrWhiteSpace(sourceGuid) ? string.Empty : SourceLabelPrefix + sourceGuid;
        }

        private static string BuildTargetLabel(MaterialConversionTarget target)
        {
            return TargetLabelPrefix + target;
        }
    }
}
