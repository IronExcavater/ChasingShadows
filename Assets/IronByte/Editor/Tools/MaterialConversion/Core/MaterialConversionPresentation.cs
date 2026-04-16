using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IronByte.Tools.MaterialConversion.Editor
{
    internal static class MaterialConversionPresentation
    {
        internal static (int score, string label, string summary) CalculateStrength(
            MaterialConversionConfidence confidence,
            IReadOnlyCollection<string> losses,
            IReadOnlyCollection<string> notes,
            int helperCount,
            bool success)
        {
            if (!success || confidence == MaterialConversionConfidence.Unsupported)
            {
                return (0, "Unsupported", "This material cannot be converted with the current settings.");
            }

            int baseScore = confidence switch
            {
                MaterialConversionConfidence.Official => 94,
                MaterialConversionConfidence.Mapped => 84,
                MaterialConversionConfidence.Heuristic => 68,
                _ => 0
            };

            int severeLossCount = 0;
            int mediumLossCount = 0;
            int lowLossCount = 0;
            int lossPenalty = 0;
            foreach (string loss in losses.Where(loss => !string.IsNullOrWhiteSpace(loss)).Distinct())
            {
                lossPenalty += GetLossPenalty(loss, out int severityBand);
                switch (severityBand)
                {
                    case 2:
                        severeLossCount++;
                        break;
                    case 1:
                        mediumLossCount++;
                        break;
                    default:
                        lowLossCount++;
                        break;
                }
            }

            int heuristicNoteCount = 0;
            int sourceLimitNoteCount = 0;
            int notePenalty = 0;
            foreach (string note in notes.Where(note => !string.IsNullOrWhiteSpace(note)).Distinct())
            {
                notePenalty += GetNotePenalty(note, out bool isHeuristicNote, out bool isSourceLimitNote);
                if (isHeuristicNote)
                {
                    heuristicNoteCount++;
                }

                if (isSourceLimitNote)
                {
                    sourceLimitNoteCount++;
                }
            }

            int helperPenalty = Mathf.Min(helperCount, 2);
            int score = Mathf.Clamp(baseScore - lossPenalty - notePenalty - helperPenalty, 0, 100);
            string label = GetStrengthLabel(score);
            string summary = BuildStrengthSummary(
                confidence,
                losses.Count,
                severeLossCount,
                mediumLossCount,
                lowLossCount,
                notes.Count,
                heuristicNoteCount,
                sourceLimitNoteCount,
                helperCount);
            return (score, label, summary);
        }

        internal static string GetStrengthLabel(int score)
        {
            if (score >= 90) return "Excellent";
            if (score >= 78) return "Strong";
            if (score >= 60) return "Moderate";
            if (score >= 40) return "Fragile";
            if (score > 0) return "Weak";
            return "Unsupported";
        }

        internal static Color GetStrengthColor(int score)
        {
            if (score >= 90) return new Color(0.19f, 0.58f, 0.27f);
            if (score >= 78) return new Color(0.25f, 0.65f, 0.35f);
            if (score >= 60) return new Color(0.73f, 0.65f, 0.18f);
            if (score >= 40) return new Color(0.82f, 0.52f, 0.17f);
            if (score > 0) return new Color(0.76f, 0.27f, 0.18f);
            return new Color(0.45f, 0.18f, 0.18f);
        }

        internal static string GetTargetDisplayName(MaterialConversionTarget target)
        {
            return target switch
            {
                MaterialConversionTarget.BuiltInStandard => "Built-in Standard",
                MaterialConversionTarget.BuiltInStandardSpecular => "Built-in Standard (Specular)",
                MaterialConversionTarget.BuiltInUnlitTexture => "Built-in Unlit/Texture",
                MaterialConversionTarget.URPLit => "URP Lit",
                MaterialConversionTarget.URPSimpleLit => "URP Simple Lit",
                MaterialConversionTarget.URPUnlit => "URP Unlit",
                MaterialConversionTarget.URPParticlesLit => "URP Particles Lit",
                MaterialConversionTarget.URPParticlesUnlit => "URP Particles Unlit",
                MaterialConversionTarget.URPTerrainLit => "URP Terrain Lit",
                MaterialConversionTarget.HDRPLit => "HDRP Lit",
                MaterialConversionTarget.HDRPUnlit => "HDRP Unlit",
                MaterialConversionTarget.HDRPTerrainLit => "HDRP Terrain Lit",
                _ => target.ToString()
            };
        }

        internal static string GetSourceDisplayName(MaterialSourceFamily sourceFamily)
        {
            return sourceFamily switch
            {
                MaterialSourceFamily.BuiltInStandard => "Built-in Standard",
                MaterialSourceFamily.BuiltInStandardSpecular => "Built-in Standard (Specular)",
                MaterialSourceFamily.BuiltInLegacyLit => "Built-in Legacy Lit",
                MaterialSourceFamily.BuiltInLegacyUnlit => "Built-in Legacy Unlit",
                MaterialSourceFamily.BuiltInTerrain => "Built-in Terrain",
                MaterialSourceFamily.BuiltInParticle => "Built-in Particle",
                MaterialSourceFamily.URPLit => "URP Lit",
                MaterialSourceFamily.URPSimpleLit => "URP Simple Lit",
                MaterialSourceFamily.URPUnlit => "URP Unlit",
                MaterialSourceFamily.URPParticlesLit => "URP Particles Lit",
                MaterialSourceFamily.URPParticlesUnlit => "URP Particles Unlit",
                MaterialSourceFamily.URPTerrainLit => "URP Terrain Lit",
                MaterialSourceFamily.HDRPLit => "HDRP Lit",
                MaterialSourceFamily.HDRPUnlit => "HDRP Unlit",
                MaterialSourceFamily.HDRPTerrainLit => "HDRP Terrain Lit",
                MaterialSourceFamily.Custom => "Custom / Shader Graph",
                _ => "Unknown"
            };
        }

        internal static string GetConfidenceDisplayName(MaterialConversionConfidence confidence)
        {
            return confidence switch
            {
                MaterialConversionConfidence.Official => "Official",
                MaterialConversionConfidence.Mapped => "Mapped",
                MaterialConversionConfidence.Heuristic => "Heuristic",
                MaterialConversionConfidence.Unsupported => "Unsupported",
                _ => confidence.ToString()
            };
        }

        internal static string BuildBatchSummary(IReadOnlyList<MaterialConversionResult> results, int remappedAssetCount)
        {
            int successCount = results.Count(result => result.Success);
            int skippedCount = results.Count - successCount;
            List<MaterialConversionResult> riskyResults = results
                .Where(result => result.Success && (result.Losses.Length > 0 || result.Notes.Length > 0 || result.GeneratedAssets.Length > 0))
                .ToList();

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"{successCount} material(s) converted");
            builder.AppendLine($"{skippedCount} material(s) skipped");

            if (remappedAssetCount > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"Updated {remappedAssetCount} asset file(s) to point at the converted copy.");
            }

            if (riskyResults.Count == 0)
            {
                return builder.ToString();
            }

            builder.AppendLine();
            builder.AppendLine("Review:");
            foreach (MaterialConversionResult result in riskyResults.Take(6))
            {
                builder.AppendLine(result.SourceMaterial != null ? result.SourceMaterial.name : "(Missing Material)");

                foreach (string loss in result.Losses.Take(3))
                {
                    builder.AppendLine("  Loss: " + loss);
                }

                foreach (string helper in result.GeneratedAssets.Take(2))
                {
                    builder.AppendLine("  Helper: " + System.IO.Path.GetFileName(helper));
                }

                if (result.GeneratedAssets.Length == 0)
                {
                    foreach (string helper in result.ExpectedGeneratedAssets.Take(2))
                    {
                        builder.AppendLine("  Helper: " + helper);
                    }
                }

                foreach (string note in result.Notes.Take(2))
                {
                    builder.AppendLine("  Note: " + note);
                }
            }

            if (riskyResults.Count > 6)
            {
                builder.AppendLine($"  ...see the {MaterialConversionToolInfo.WindowTitle} window for the full report.");
            }

            return builder.ToString().TrimEnd();
        }

        internal static string BuildMultilineList(IEnumerable<string> items)
        {
            string[] entries = items.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToArray();
            if (entries.Length == 0)
            {
                return string.Empty;
            }

            return string.Join("\n", entries.Select(entry => "- " + entry));
        }

        private static int GetLossPenalty(string loss, out int severityBand)
        {
            string normalized = loss.ToLowerInvariant();
            if (normalized.Contains("removed", StringComparison.Ordinal) ||
                normalized.Contains("cannot preserve", StringComparison.Ordinal) ||
                normalized.Contains("flattened", StringComparison.Ordinal) ||
                normalized.Contains("only keeps", StringComparison.Ordinal))
            {
                severityBand = 2;
                return 18;
            }

            if (normalized.Contains("approximate", StringComparison.Ordinal) ||
                normalized.Contains("fall back", StringComparison.Ordinal) ||
                normalized.Contains("not exactly preserve", StringComparison.Ordinal) ||
                normalized.Contains("downgrade", StringComparison.Ordinal))
            {
                severityBand = 1;
                return 11;
            }

            severityBand = 0;
            return 7;
        }

        private static int GetNotePenalty(string note, out bool isHeuristicNote, out bool isSourceLimitNote)
        {
            string normalized = note.ToLowerInvariant();
            isHeuristicNote = normalized.Contains("heuristic", StringComparison.Ordinal);
            isSourceLimitNote = normalized.Contains("do not carry", StringComparison.Ordinal) ||
                                normalized.Contains("already lacks", StringComparison.Ordinal);

            if (isHeuristicNote)
            {
                return 5;
            }

            if (isSourceLimitNote)
            {
                return 2;
            }

            return 1;
        }

        private static string BuildStrengthSummary(
            MaterialConversionConfidence confidence,
            int lossCount,
            int severeLossCount,
            int mediumLossCount,
            int lowLossCount,
            int noteCount,
            int heuristicNoteCount,
            int sourceLimitNoteCount,
            int helperCount)
        {
            List<string> parts = new List<string>();

            if (lossCount == 0 && helperCount == 0 && noteCount == 0)
            {
                return "No predicted data loss.";
            }

            if (severeLossCount > 0)
            {
                parts.Add($"{severeLossCount} major feature area{(severeLossCount == 1 ? string.Empty : "s")} will not carry across.");
            }

            if (mediumLossCount > 0)
            {
                parts.Add($"{mediumLossCount} area{(mediumLossCount == 1 ? string.Empty : "s")} will be approximated.");
            }

            if (lowLossCount > 0 && severeLossCount == 0 && mediumLossCount == 0)
            {
                parts.Add($"{lowLossCount} minor compatibility warning{(lowLossCount == 1 ? string.Empty : "s")} detected.");
            }

            if (helperCount > 0)
            {
                parts.Add($"{helperCount} helper texture{(helperCount == 1 ? string.Empty : "s")} needed to keep packed channels.");
            }

            if (heuristicNoteCount > 0 && confidence != MaterialConversionConfidence.Heuristic)
            {
                parts.Add("Some properties relied on alias matching.");
            }

            if (sourceLimitNoteCount > 0)
            {
                parts.Add("The source shader already omits some surface data.");
            }

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
