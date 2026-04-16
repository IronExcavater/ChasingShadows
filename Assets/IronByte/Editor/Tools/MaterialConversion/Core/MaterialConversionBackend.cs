using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronByte.Tools.MaterialConversion.Editor
{
    internal static class MaterialConversionBackend
    {
        private static readonly string[] BaseMapAliases = { "_BaseMap", "_BaseColorMap", "_MainTex", "_UnlitColorMap" };
        private static readonly string[] BaseColorAliases = { "_BaseColor", "_Color", "_UnlitColor" };
        private static readonly string[] NormalAliases = { "_BumpMap", "_NormalMap" };
        private static readonly string[] MetallicGlossAliases = { "_MetallicGlossMap" };
        private static readonly string[] SpecGlossAliases = { "_SpecGlossMap", "_SpecularColorMap" };
        private static readonly string[] OcclusionAliases = { "_OcclusionMap" };
        private static readonly string[] EmissionMapAliases = { "_EmissionMap", "_EmissiveColorMap" };
        private static readonly string[] EmissionColorAliases = { "_EmissionColor", "_EmissiveColor" };
        private static readonly string[] HeightAliases = { "_ParallaxMap", "_HeightMap" };

        private static readonly string[] HdrpFeatureProperties =
        {
            "_CoatMask",
            "_CoatMaskMap",
            "_Anisotropy",
            "_AnisotropyMap",
            "_TransmissionMask",
            "_TransmissionMaskMap",
            "_IridescenceMask",
            "_IridescenceMaskMap",
            "_RefractionModel",
            "_HeightPoMAmplitude"
        };

        internal static bool TryExtractSemanticData(
            Material material,
            MaterialSourceFamily sourceFamily,
            List<string> warnings,
            out MaterialSemanticData data,
            out bool heuristic,
            out string reason)
        {
            heuristic = false;
            reason = string.Empty;
            data = new MaterialSemanticData
            {
                SourceMaterial = material,
                SourceFamily = sourceFamily,
                BaseMap = FindTexture(material, BaseMapAliases),
                BaseColor = FindColor(material, BaseColorAliases, Color.white),
                BaseMapScale = GetTextureScale(material, BaseMapAliases),
                BaseMapOffset = GetTextureOffset(material, BaseMapAliases),
                NormalMap = FindTexture(material, NormalAliases),
                NormalScale = FindFloat(material, new[] { "_BumpScale", "_NormalScale" }, 1f),
                Metallic = FindFloat(material, new[] { "_Metallic", "_MetallicRemapMax" }, 0f),
                MetallicGlossMap = FindTexture(material, MetallicGlossAliases),
                Smoothness = FindFloat(material, new[] { "_Smoothness", "_SmoothnessRemapMax", "_Glossiness" }, 0.5f),
                SmoothnessTextureChannel = Mathf.RoundToInt(FindFloat(material, new[] { "_SmoothnessTextureChannel" }, 0f)),
                SpecularColor = FindColor(material, new[] { "_SpecColor", "_SpecularColor" }, new Color(0.2f, 0.2f, 0.2f, 1f)),
                SpecularGlossMap = FindTexture(material, SpecGlossAliases),
                OcclusionMap = FindTexture(material, OcclusionAliases),
                OcclusionStrength = FindFloat(material, new[] { "_OcclusionStrength", "_AORemapMax" }, 1f),
                EmissionMap = FindTexture(material, EmissionMapAliases),
                EmissionColor = FindColor(material, EmissionColorAliases, Color.black),
                HeightMap = FindTexture(material, HeightAliases),
                HeightScale = FindFloat(material, new[] { "_Parallax", "_HeightPoMAmplitude" }, 0f),
                DetailMaskMap = material.HasProperty("_DetailMask") ? material.GetTexture("_DetailMask") : null,
                DetailAlbedoMap = material.HasProperty("_DetailAlbedoMap") ? material.GetTexture("_DetailAlbedoMap") : (material.HasProperty("_DetailMap") ? material.GetTexture("_DetailMap") : null),
                DetailNormalMap = material.HasProperty("_DetailNormalMap") ? material.GetTexture("_DetailNormalMap") : null,
                DetailNormalScale = FindFloat(material, new[] { "_DetailNormalMapScale", "_DetailNormalScale" }, 1f),
                MaskMap = material.HasProperty("_MaskMap") ? material.GetTexture("_MaskMap") : null,
                Cutoff = FindFloat(material, new[] { "_Cutoff", "_AlphaCutoff" }, 0.5f),
                AlphaClip = FindFloat(material, new[] { "_AlphaClip", "_AlphaCutoffEnable" }, 0f) > 0.5f,
                CullMode = Mathf.RoundToInt(FindFloat(material, new[] { "_Cull", "_CullMode" }, 2f)),
                DoubleSided = FindFloat(material, new[] { "_DoubleSidedEnable" }, 0f) > 0.5f
            };

            data.EmissionEnabled = material.IsKeywordEnabled("_EMISSION") || data.EmissionMap != null || data.EmissionColor.maxColorComponent > 0f;
            data.DoubleSided |= data.CullMode == 0;
            data.AlphaMode = DetermineAlphaMode(material, sourceFamily, data.AlphaClip);

            switch (sourceFamily)
            {
                case MaterialSourceFamily.BuiltInStandard:
                    data.Workflow = MaterialWorkflow.Metallic;
                    break;
                case MaterialSourceFamily.BuiltInStandardSpecular:
                case MaterialSourceFamily.URPSimpleLit:
                    data.Workflow = MaterialWorkflow.Specular;
                    break;
                case MaterialSourceFamily.BuiltInLegacyUnlit:
                case MaterialSourceFamily.URPUnlit:
                case MaterialSourceFamily.HDRPUnlit:
                    data.Workflow = MaterialWorkflow.Unlit;
                    warnings.Add("Unlit sources do not carry normal, metallic, or occlusion data.");
                    break;
                case MaterialSourceFamily.BuiltInParticle:
                case MaterialSourceFamily.URPParticlesLit:
                    data.Workflow = MaterialWorkflow.ParticleLit;
                    data.IsParticle = true;
                    break;
                case MaterialSourceFamily.URPParticlesUnlit:
                    data.Workflow = MaterialWorkflow.ParticleUnlit;
                    data.IsParticle = true;
                    break;
                case MaterialSourceFamily.BuiltInTerrain:
                case MaterialSourceFamily.URPTerrainLit:
                case MaterialSourceFamily.HDRPTerrainLit:
                    ExtractTerrain(material, data);
                    break;
                case MaterialSourceFamily.HDRPLit:
                    data.Workflow = DetermineHdrpWorkflow(material);
                    break;
                case MaterialSourceFamily.URPLit:
                    data.Workflow = Mathf.RoundToInt(FindFloat(material, new[] { "_WorkflowMode" }, 1f)) == 0 ? MaterialWorkflow.Specular : MaterialWorkflow.Metallic;
                    break;
                case MaterialSourceFamily.BuiltInLegacyLit:
                    data.Workflow = MaterialWorkflow.Metallic;
                    if (material.shader.name.Contains("Self-Illumin", StringComparison.OrdinalIgnoreCase))
                    {
                        data.EmissionMap = material.GetTexture("_Illum");
                        data.EmissionEnabled = data.EmissionMap != null;
                        data.EmissionColor = data.EmissionEnabled ? Color.white : Color.black;
                    }
                    break;
                case MaterialSourceFamily.Custom:
                    heuristic = true;
                    data.Workflow = GuessWorkflow(data);
                    if (data.BaseMap == null && data.EmissionMap == null && data.NormalMap == null && data.MaskMap == null && data.MetallicGlossMap == null && data.SpecularGlossMap == null)
                    {
                        reason = "Could not infer enough semantic properties from the custom shader.";
                        return false;
                    }
                    break;
                default:
                    reason = $"Shader '{material.shader.name}' is not in a supported source family.";
                    return false;
            }

            if (data.Workflow == MaterialWorkflow.Terrain && data.TerrainControlMap == null && data.BaseMap == null)
            {
                reason = "Terrain materials need either terrain layer data or a terrain basemap to convert.";
                return false;
            }

            return true;
        }

        internal static IEnumerable<string> CollectTargetNotes(MaterialSemanticData data, MaterialConversionTarget target)
        {
            List<string> notes = new List<string>();
            notes.AddRange(PredictGeneratedAssetNotes(data, target));
            return notes;
        }

        internal static IEnumerable<string> CollectTargetLosses(MaterialSemanticData data, MaterialConversionTarget target)
        {
            List<string> losses = new List<string>();

            bool targetIsUnlit = target == MaterialConversionTarget.BuiltInUnlitTexture ||
                                 target == MaterialConversionTarget.URPUnlit ||
                                 target == MaterialConversionTarget.HDRPUnlit;
            if (targetIsUnlit && (data.NormalMap != null || data.MetallicGlossMap != null || data.MaskMap != null || data.OcclusionMap != null || data.SpecularGlossMap != null))
            {
                losses.Add("Lighting response, normal detail, metallic/specular data, and occlusion will be removed because the target shader is unlit.");
            }

            if (!MaterialConversionService.IsHdrpTarget(target) && (data.SourceFamily == MaterialSourceFamily.HDRPLit || data.SourceFamily == MaterialSourceFamily.HDRPUnlit))
            {
                losses.AddRange(CollectHdrpDowngradeLosses(data.SourceMaterial));
            }

            if (target == MaterialConversionTarget.BuiltInStandard && data.Workflow == MaterialWorkflow.Specular)
            {
                losses.Add("Specular workflow will be approximated as metallic workflow in Built-in Standard.");
            }

            if (target == MaterialConversionTarget.BuiltInUnlitTexture && data.AlphaMode != MaterialAlphaMode.Opaque && data.AlphaMode != MaterialAlphaMode.Cutout)
            {
                losses.Add("Built-in Unlit/Texture cannot preserve transparent blending. The result will be flattened to opaque.");
            }

            if ((target == MaterialConversionTarget.BuiltInStandard || target == MaterialConversionTarget.BuiltInStandardSpecular) &&
                (data.AlphaMode == MaterialAlphaMode.Additive || data.AlphaMode == MaterialAlphaMode.Multiply))
            {
                losses.Add("Built-in Standard cannot preserve additive or multiply transparency. It will fall back to regular transparent blending.");
            }

            if ((target == MaterialConversionTarget.URPLit || target == MaterialConversionTarget.URPSimpleLit || target == MaterialConversionTarget.URPParticlesLit) &&
                data.AlphaMode == MaterialAlphaMode.Additive)
            {
                losses.Add("URP stock shaders approximate additive transparency and will not exactly preserve the original blend mode.");
            }

            if (MaterialConversionService.IsTerrainTarget(target))
            {
                losses.Add("Terrain conversion only keeps terrain layer assignments, masks, and the basemap. Review advanced terrain shading manually.");
            }

            return losses;
        }

        internal static IEnumerable<string> PredictGeneratedAssetNotes(MaterialSemanticData data, MaterialConversionTarget target)
        {
            List<string> notes = new List<string>();
            if (NeedsHdrpMaskMap(data, target))
            {
                notes.Add("A packed HDRP mask map will be generated to preserve metallic, smoothness, and occlusion channels.");
            }

            if (NeedsMetallicGlossTexture(data, target))
            {
                notes.Add("A packed metallic/smoothness helper texture will be generated.");
            }

            if (NeedsSpecGlossTexture(data, target))
            {
                notes.Add("A packed specular/smoothness helper texture will be generated.");
            }

            return notes;
        }

        internal static IEnumerable<string> PredictGeneratedAssetPaths(MaterialSemanticData data, MaterialConversionTarget target)
        {
            List<string> paths = new List<string>();
            if (NeedsHdrpMaskMap(data, target))
            {
                paths.Add(MaterialTextureUtility.GetPackedTextureAssetPath(data.SourceMaterial, target, "HdrpMaskMap"));
            }

            if (NeedsMetallicGlossTexture(data, target))
            {
                string role = target == MaterialConversionTarget.URPLit ? "UrpMetallicGloss" : "MetallicGloss";
                paths.Add(MaterialTextureUtility.GetPackedTextureAssetPath(data.SourceMaterial, target, role));
            }

            if (NeedsSpecGlossTexture(data, target))
            {
                string role = target switch
                {
                    MaterialConversionTarget.URPSimpleLit => "SimpleSpecGloss",
                    MaterialConversionTarget.URPLit => "UrpSpecGloss",
                    _ => "SpecGloss"
                };
                paths.Add(MaterialTextureUtility.GetPackedTextureAssetPath(data.SourceMaterial, target, role));
            }

            return paths;
        }

        internal static void WriteSemanticData(Material destinationMaterial, MaterialConversionTarget target, MaterialSemanticData data, MaterialConversionRequest request, List<string> warnings)
        {
            if (!MaterialConversionService.TryResolveTargetShader(target, out Shader targetShader, out string shaderName) || targetShader == null)
            {
                throw new InvalidOperationException($"Target shader '{shaderName}' is not available.");
            }

            destinationMaterial.shader = targetShader;
            switch (target)
            {
                case MaterialConversionTarget.BuiltInStandard:
                    WriteBuiltInStandard(destinationMaterial, data, request, warnings, false);
                    break;
                case MaterialConversionTarget.BuiltInStandardSpecular:
                    WriteBuiltInStandard(destinationMaterial, data, request, warnings, true);
                    break;
                case MaterialConversionTarget.BuiltInUnlitTexture:
                    WriteBuiltInUnlit(destinationMaterial, data, warnings);
                    break;
                case MaterialConversionTarget.URPLit:
                    WriteUrpLit(destinationMaterial, data, request, warnings, false);
                    break;
                case MaterialConversionTarget.URPSimpleLit:
                    WriteUrpLit(destinationMaterial, data, request, warnings, true);
                    break;
                case MaterialConversionTarget.URPUnlit:
                    WriteUrpUnlit(destinationMaterial, data, warnings);
                    break;
                case MaterialConversionTarget.URPParticlesLit:
                    WriteUrpParticle(destinationMaterial, data, warnings, true);
                    break;
                case MaterialConversionTarget.URPParticlesUnlit:
                    WriteUrpParticle(destinationMaterial, data, warnings, false);
                    break;
                case MaterialConversionTarget.URPTerrainLit:
                case MaterialConversionTarget.HDRPTerrainLit:
                    WriteTerrain(destinationMaterial, data);
                    break;
                case MaterialConversionTarget.HDRPLit:
                    WriteHdrpLit(destinationMaterial, data, request, warnings);
                    break;
                case MaterialConversionTarget.HDRPUnlit:
                    WriteHdrpUnlit(destinationMaterial, data, warnings);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }

            PostApplyKeywords(destinationMaterial, target);
        }

        private static void ExtractTerrain(Material material, MaterialSemanticData data)
        {
            data.Workflow = MaterialWorkflow.Terrain;
            data.IsTerrain = true;
            data.TerrainControlMap = material.GetTexture("_Control");
            data.TerrainHolesTexture = material.GetTexture("_TerrainHolesTexture");
            data.BaseMap = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : data.BaseMap;
            data.BaseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : (material.HasProperty("_Color") ? material.GetColor("_Color") : data.BaseColor);
            data.TerrainHeightBlend = FindFloat(material, new[] { "_EnableHeightBlend" }, 0f);
            data.TerrainHeightTransition = FindFloat(material, new[] { "_HeightTransition" }, 0f);

            for (int i = 0; i < 4; i++)
            {
                string index = i.ToString(CultureInfo.InvariantCulture);
                data.TerrainDiffuseMaps[i] = material.GetTexture("_Splat" + index);
                data.TerrainNormalMaps[i] = material.GetTexture("_Normal" + index);
                data.TerrainMaskMaps[i] = material.GetTexture("_Mask" + index);
                data.TerrainMetallicValues[i] = FindFloat(material, new[] { "_Metallic" + index }, 0f);
                data.TerrainSmoothnessValues[i] = FindFloat(material, new[] { "_Smoothness" + index }, 0.5f);
            }
        }

        private static MaterialWorkflow GuessWorkflow(MaterialSemanticData data)
        {
            if (data.IsTerrain)
            {
                return MaterialWorkflow.Terrain;
            }

            if (data.SpecularGlossMap != null)
            {
                return MaterialWorkflow.Specular;
            }

            if (data.NormalMap != null || data.MetallicGlossMap != null || data.MaskMap != null)
            {
                return MaterialWorkflow.Metallic;
            }

            return MaterialWorkflow.Unlit;
        }

        private static MaterialWorkflow DetermineHdrpWorkflow(Material material)
        {
            if (material.HasProperty("_MaterialID") && Mathf.RoundToInt(material.GetFloat("_MaterialID")) == 4)
            {
                return MaterialWorkflow.Specular;
            }

            return material.GetTexture("_SpecularColorMap") != null ? MaterialWorkflow.Specular : MaterialWorkflow.Metallic;
        }

        private static MaterialAlphaMode DetermineAlphaMode(Material material, MaterialSourceFamily family, bool alphaClip)
        {
            if (family == MaterialSourceFamily.BuiltInStandard || family == MaterialSourceFamily.BuiltInStandardSpecular)
            {
                return material.HasProperty("_Mode")
                    ? Mathf.RoundToInt(material.GetFloat("_Mode")) switch
                    {
                        1 => MaterialAlphaMode.Cutout,
                        2 => MaterialAlphaMode.Fade,
                        3 => MaterialAlphaMode.Transparent,
                        _ => MaterialAlphaMode.Opaque
                    }
                    : (material.renderQueue >= (int)RenderQueue.Transparent ? MaterialAlphaMode.Transparent : MaterialAlphaMode.Opaque);
            }

            if (family == MaterialSourceFamily.URPLit || family == MaterialSourceFamily.URPSimpleLit ||
                family == MaterialSourceFamily.URPUnlit || family == MaterialSourceFamily.URPParticlesLit || family == MaterialSourceFamily.URPParticlesUnlit)
            {
                bool transparent = material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f;
                if (!transparent)
                {
                    return alphaClip ? MaterialAlphaMode.Cutout : MaterialAlphaMode.Opaque;
                }

                if (material.IsKeywordEnabled("_ALPHAMODULATE_ON")) return MaterialAlphaMode.Multiply;
                if (material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")) return MaterialAlphaMode.Premultiply;
                return MaterialAlphaMode.Transparent;
            }

            if (family == MaterialSourceFamily.HDRPLit || family == MaterialSourceFamily.HDRPUnlit)
            {
                bool transparent = material.HasProperty("_SurfaceType") && material.GetFloat("_SurfaceType") > 0.5f;
                if (!transparent)
                {
                    return alphaClip ? MaterialAlphaMode.Cutout : MaterialAlphaMode.Opaque;
                }

                return material.HasProperty("_BlendMode")
                    ? Mathf.RoundToInt(material.GetFloat("_BlendMode")) switch
                    {
                        1 => MaterialAlphaMode.Additive,
                        2 => MaterialAlphaMode.Multiply,
                        4 => MaterialAlphaMode.Premultiply,
                        _ => MaterialAlphaMode.Transparent
                    }
                    : MaterialAlphaMode.Transparent;
            }

            return alphaClip ? MaterialAlphaMode.Cutout : (material.renderQueue >= (int)RenderQueue.Transparent ? MaterialAlphaMode.Transparent : MaterialAlphaMode.Opaque);
        }

        private static void WriteBuiltInStandard(Material material, MaterialSemanticData data, MaterialConversionRequest request, List<string> warnings, bool specularWorkflow)
        {
            material.SetTexture("_MainTex", data.BaseMap);
            material.SetColor("_Color", data.BaseColor);
            material.SetTextureScale("_MainTex", data.BaseMapScale);
            material.SetTextureOffset("_MainTex", data.BaseMapOffset);
            material.SetTexture("_BumpMap", data.NormalMap);
            material.SetFloat("_BumpScale", data.NormalScale);
            material.SetFloat("_Glossiness", data.Smoothness);
            material.SetFloat("_GlossMapScale", data.Smoothness);
            material.SetFloat("_Cutoff", data.Cutoff);
            material.SetTexture("_OcclusionMap", data.OcclusionMap);
            material.SetFloat("_OcclusionStrength", data.OcclusionStrength);
            material.SetTexture("_EmissionMap", data.EmissionMap);
            material.SetColor("_EmissionColor", data.EmissionEnabled ? data.EmissionColor : Color.black);

            if (specularWorkflow)
            {
                material.SetColor("_SpecColor", data.SpecularColor);
                material.SetTexture("_SpecGlossMap", ResolveSpecGlossTexture(data, request, MaterialConversionTarget.BuiltInStandardSpecular, "SpecGloss", false));
            }
            else
            {
                material.SetFloat("_Metallic", data.Metallic);
                material.SetTexture("_MetallicGlossMap", ResolveMetallicGlossTexture(data, request, MaterialConversionTarget.BuiltInStandard, "MetallicGloss", false));
            }

            if (data.EmissionEnabled) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");

            SetupBuiltInBlendMode(material, data, warnings);
            MaterialEditor.FixupEmissiveFlag(material);
        }

        private static void WriteBuiltInUnlit(Material material, MaterialSemanticData data, List<string> warnings)
        {
            material.SetTexture("_MainTex", data.BaseMap);
            material.SetColor("_Color", data.BaseColor);
            material.SetTextureScale("_MainTex", data.BaseMapScale);
            material.SetTextureOffset("_MainTex", data.BaseMapOffset);
            if (data.AlphaMode != MaterialAlphaMode.Opaque && data.AlphaMode != MaterialAlphaMode.Cutout)
            {
                warnings.Add("Built-in Unlit/Texture does not preserve transparent blend modes. The result was flattened to opaque.");
            }
        }

        private static void WriteUrpLit(Material material, MaterialSemanticData data, MaterialConversionRequest request, List<string> warnings, bool simpleLit)
        {
            material.SetTexture("_BaseMap", data.BaseMap);
            material.SetColor("_BaseColor", data.BaseColor);
            material.SetTextureScale("_BaseMap", data.BaseMapScale);
            material.SetTextureOffset("_BaseMap", data.BaseMapOffset);
            material.SetTexture("_BumpMap", data.NormalMap);
            material.SetFloat("_BumpScale", data.NormalScale);
            material.SetTexture("_EmissionMap", data.EmissionMap);
            material.SetColor("_EmissionColor", data.EmissionEnabled ? data.EmissionColor : Color.black);
            material.SetTexture("_OcclusionMap", data.OcclusionMap);
            material.SetFloat("_OcclusionStrength", data.OcclusionStrength);

            if (simpleLit)
            {
                material.SetFloat("_Smoothness", data.Smoothness);
                material.SetColor("_SpecColor", data.SpecularColor);
                material.SetTexture("_SpecGlossMap", ResolveSpecGlossTexture(data, request, MaterialConversionTarget.URPSimpleLit, "SimpleSpecGloss", false));
            }
            else if (data.Workflow == MaterialWorkflow.Specular)
            {
                material.SetFloat("_WorkflowMode", 0f);
                material.SetFloat("_Smoothness", data.Smoothness);
                material.SetFloat("_SmoothnessTextureChannel", data.SmoothnessTextureChannel);
                material.SetColor("_SpecColor", data.SpecularColor);
                material.SetTexture("_SpecGlossMap", ResolveSpecGlossTexture(data, request, MaterialConversionTarget.URPLit, "UrpSpecGloss", false));
            }
            else
            {
                material.SetFloat("_WorkflowMode", 1f);
                material.SetFloat("_Smoothness", data.Smoothness);
                material.SetFloat("_SmoothnessTextureChannel", data.SmoothnessTextureChannel);
                material.SetFloat("_Metallic", data.Metallic);
                material.SetTexture("_MetallicGlossMap", ResolveMetallicGlossTexture(data, request, MaterialConversionTarget.URPLit, "UrpMetallicGloss", false));
            }

            SetupUrpSurface(material, data, warnings);
        }

        private static void WriteUrpUnlit(Material material, MaterialSemanticData data, List<string> warnings)
        {
            material.SetTexture("_BaseMap", data.BaseMap);
            material.SetColor("_BaseColor", data.BaseColor);
            material.SetTextureScale("_BaseMap", data.BaseMapScale);
            material.SetTextureOffset("_BaseMap", data.BaseMapOffset);
            SetupUrpSurface(material, data, warnings);
        }

        private static void WriteUrpParticle(Material material, MaterialSemanticData data, List<string> warnings, bool lit)
        {
            material.SetTexture("_BaseMap", data.BaseMap);
            material.SetColor("_BaseColor", data.BaseColor);
            material.SetTextureScale("_BaseMap", data.BaseMapScale);
            material.SetTextureOffset("_BaseMap", data.BaseMapOffset);
            material.SetTexture("_EmissionMap", data.EmissionMap);
            material.SetColor("_EmissionColor", data.EmissionEnabled ? data.EmissionColor : Color.black);
            if (lit)
            {
                material.SetTexture("_BumpMap", data.NormalMap);
                material.SetFloat("_BumpScale", data.NormalScale);
                material.SetFloat("_Smoothness", data.Smoothness);
            }

            SetupUrpSurface(material, data, warnings);
        }

        private static void WriteHdrpLit(Material material, MaterialSemanticData data, MaterialConversionRequest request, List<string> warnings)
        {
            material.SetColor("_BaseColor", data.BaseColor);
            material.SetTexture("_BaseColorMap", data.BaseMap);
            material.SetTextureScale("_BaseColorMap", data.BaseMapScale);
            material.SetTextureOffset("_BaseColorMap", data.BaseMapOffset);
            material.SetTexture("_NormalMap", data.NormalMap);
            material.SetFloat("_NormalScale", data.NormalScale);
            material.SetTexture("_EmissiveColorMap", data.EmissionMap);
            material.SetColor("_EmissiveColor", data.EmissionEnabled ? data.EmissionColor : Color.black);
            material.SetFloat("_Metallic", data.Metallic);
            material.SetFloat("_Smoothness", data.Smoothness);
            material.SetTexture("_MaskMap", ResolveHdrpMaskMap(data, request, MaterialConversionTarget.HDRPLit, "HdrpMaskMap"));
            material.SetFloat("_AORemapMin", 0f);
            material.SetFloat("_AORemapMax", data.OcclusionStrength);
            material.SetTexture("_HeightMap", data.HeightMap);
            material.SetFloat("_HeightPoMAmplitude", data.HeightScale);
            material.SetFloat("_DoubleSidedEnable", data.DoubleSided ? 1f : 0f);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", data.DoubleSided ? 0f : data.CullMode);

            if (data.Workflow == MaterialWorkflow.Specular)
            {
                material.SetFloat("_MaterialID", 4f);
                material.SetColor("_SpecularColor", data.SpecularColor);
                material.SetTexture("_SpecularColorMap", data.SpecularGlossMap);
            }
            else
            {
                material.SetFloat("_MaterialID", 1f);
            }

            SetupHdrpSurface(material, data, warnings);
        }

        private static void WriteHdrpUnlit(Material material, MaterialSemanticData data, List<string> warnings)
        {
            material.SetTexture("_UnlitColorMap", data.BaseMap);
            material.SetColor("_UnlitColor", data.BaseColor);
            material.SetTextureScale("_UnlitColorMap", data.BaseMapScale);
            material.SetTextureOffset("_UnlitColorMap", data.BaseMapOffset);
            material.SetTexture("_EmissiveColorMap", data.EmissionMap);
            material.SetColor("_EmissiveColor", data.EmissionEnabled ? data.EmissionColor : Color.black);
            material.SetFloat("_DoubleSidedEnable", data.DoubleSided ? 1f : 0f);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", data.DoubleSided ? 0f : data.CullMode);
            SetupHdrpSurface(material, data, warnings);
        }

        private static void WriteTerrain(Material material, MaterialSemanticData data)
        {
            material.SetTexture("_Control", data.TerrainControlMap);
            material.SetTexture("_TerrainHolesTexture", data.TerrainHolesTexture);
            material.SetFloat("_EnableHeightBlend", data.TerrainHeightBlend);
            material.SetFloat("_HeightTransition", data.TerrainHeightTransition);
            material.SetTexture("_MainTex", data.BaseMap);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", data.BaseColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", data.BaseColor);

            for (int i = 0; i < 4; i++)
            {
                string index = i.ToString(CultureInfo.InvariantCulture);
                material.SetTexture("_Splat" + index, data.TerrainDiffuseMaps[i]);
                material.SetTexture("_Normal" + index, data.TerrainNormalMaps[i]);
                material.SetTexture("_Mask" + index, data.TerrainMaskMaps[i]);
                if (material.HasProperty("_Metallic" + index)) material.SetFloat("_Metallic" + index, data.TerrainMetallicValues[i]);
                if (material.HasProperty("_Smoothness" + index)) material.SetFloat("_Smoothness" + index, data.TerrainSmoothnessValues[i]);
            }
        }

        internal static void PostApplyKeywords(Material material, MaterialConversionTarget target)
        {
            if (MaterialConversionService.IsHdrpTarget(target))
            {
                Type hdShaderUtilsType = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor");
                MethodInfo resetMethod = hdShaderUtilsType?.GetMethod("ResetMaterialKeywords", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
                resetMethod?.Invoke(null, new object[] { material });
            }
            else
            {
                MaterialEditor.FixupEmissiveFlag(material);
            }
        }

        private static void SetupBuiltInBlendMode(Material material, MaterialSemanticData data, List<string> warnings)
        {
            int mode = data.AlphaMode switch
            {
                MaterialAlphaMode.Cutout => 1,
                MaterialAlphaMode.Fade => 2,
                MaterialAlphaMode.Transparent or MaterialAlphaMode.Premultiply or MaterialAlphaMode.Additive or MaterialAlphaMode.Multiply => 3,
                _ => 0
            };

            if (data.AlphaMode == MaterialAlphaMode.Additive || data.AlphaMode == MaterialAlphaMode.Multiply)
            {
                warnings.Add("Built-in Standard does not preserve additive or multiply transparency. Converted using transparent alpha blending.");
            }

            material.SetFloat("_Mode", mode);
            material.SetFloat("_SrcBlend", mode <= 1 ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", mode <= 1 ? (float)BlendMode.Zero : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", mode <= 1 ? 1f : 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if (mode == 1) material.EnableKeyword("_ALPHATEST_ON");
            if (mode == 2) material.EnableKeyword("_ALPHABLEND_ON");
            if (mode == 3) material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = mode == 0 ? -1 : (mode == 1 ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Transparent);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", data.DoubleSided ? 0f : data.CullMode);
        }

        private static void SetupUrpSurface(Material material, MaterialSemanticData data, List<string> warnings)
        {
            bool transparent = data.AlphaMode != MaterialAlphaMode.Opaque && data.AlphaMode != MaterialAlphaMode.Cutout;
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_AlphaClip", data.AlphaClip || data.AlphaMode == MaterialAlphaMode.Cutout ? 1f : 0f);
            material.SetFloat("_Cutoff", data.Cutoff);
            material.SetFloat("_Cull", data.DoubleSided ? 0f : data.CullMode);
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            material.SetFloat("_SrcBlendAlpha", transparent ? (float)BlendMode.One : (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.DisableKeyword("_ALPHATEST_ON");

            if (transparent)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (data.AlphaMode == MaterialAlphaMode.Premultiply) material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                if (data.AlphaMode == MaterialAlphaMode.Multiply) material.EnableKeyword("_ALPHAMODULATE_ON");
                material.SetFloat("_Blend", data.AlphaMode == MaterialAlphaMode.Additive ? 2f : (data.AlphaMode == MaterialAlphaMode.Multiply ? 3f : 0f));
                if (data.AlphaMode == MaterialAlphaMode.Additive)
                {
                    warnings.Add("URP stock shaders approximate additive transparency without source-specific particle settings.");
                }
            }
            else
            {
                material.SetFloat("_Blend", 0f);
            }

            if (data.AlphaClip || data.AlphaMode == MaterialAlphaMode.Cutout) material.EnableKeyword("_ALPHATEST_ON");
            if (data.NormalMap != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
        }

        private static void SetupHdrpSurface(Material material, MaterialSemanticData data, List<string> warnings)
        {
            bool transparent = data.AlphaMode != MaterialAlphaMode.Opaque && data.AlphaMode != MaterialAlphaMode.Cutout;
            material.SetFloat("_SurfaceType", transparent ? 1f : 0f);
            material.SetFloat("_AlphaCutoffEnable", data.AlphaClip || data.AlphaMode == MaterialAlphaMode.Cutout ? 1f : 0f);
            material.SetFloat("_AlphaCutoff", data.Cutoff);
            material.SetFloat("_DoubleSidedEnable", data.DoubleSided ? 1f : 0f);

            float blendMode = data.AlphaMode switch
            {
                MaterialAlphaMode.Additive => 1f,
                MaterialAlphaMode.Multiply => 2f,
                MaterialAlphaMode.Premultiply => 4f,
                _ => 0f
            };

            if (data.AlphaMode == MaterialAlphaMode.Multiply)
            {
                warnings.Add("HDRP stock shaders approximate multiply transparency using the closest stock blend mode.");
            }

            material.SetFloat("_BlendMode", transparent ? blendMode : 0f);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", data.DoubleSided ? 0f : data.CullMode);
        }

        private static Texture ResolveHdrpMaskMap(MaterialSemanticData data, MaterialConversionRequest request, MaterialConversionTarget target, string role)
        {
            if (data.MaskMap != null)
            {
                return data.MaskMap;
            }

            if (!NeedsHdrpMaskMap(data, target))
            {
                return null;
            }

            EnsureHelperTexturesAllowed(request, "a packed HDRP mask map");
            return BuildHdrpMaskMap(data, request, target, role);
        }

        private static Texture ResolveMetallicGlossTexture(MaterialSemanticData data, MaterialConversionRequest request, MaterialConversionTarget target, string role, bool useBaseAlpha)
        {
            if (data.MetallicGlossMap != null)
            {
                return data.MetallicGlossMap;
            }

            if (!NeedsMetallicGlossTexture(data, target))
            {
                return null;
            }

            EnsureHelperTexturesAllowed(request, "a packed metallic/smoothness helper texture");
            return BuildMetallicGlossTexture(data, request, target, role, useBaseAlpha);
        }

        private static Texture ResolveSpecGlossTexture(MaterialSemanticData data, MaterialConversionRequest request, MaterialConversionTarget target, string role, bool useBaseAlpha)
        {
            if (data.SpecularGlossMap != null)
            {
                return data.SpecularGlossMap;
            }

            if (!NeedsSpecGlossTexture(data, target))
            {
                return null;
            }

            EnsureHelperTexturesAllowed(request, "a packed specular/smoothness helper texture");
            return BuildSpecGlossTexture(data, request, target, role, useBaseAlpha);
        }

        private static Texture BuildHdrpMaskMap(MaterialSemanticData data, MaterialConversionRequest request, MaterialConversionTarget target, string role)
        {
            ChannelDescriptor r = data.MetallicGlossMap != null ? new ChannelDescriptor(data.MetallicGlossMap, TextureChannel.Red, data.Metallic) : ChannelDescriptor.Constant(data.Metallic);
            ChannelDescriptor g = data.OcclusionMap != null ? new ChannelDescriptor(data.OcclusionMap, TextureChannel.Green, data.OcclusionStrength) : ChannelDescriptor.Constant(data.OcclusionStrength);
            ChannelDescriptor b = data.DetailMaskMap != null ? new ChannelDescriptor(data.DetailMaskMap, TextureChannel.Alpha, 1f) : ChannelDescriptor.Constant(1f);
            ChannelDescriptor a = data.MaskMap != null ? new ChannelDescriptor(data.MaskMap, TextureChannel.Alpha, data.Smoothness)
                : (data.MetallicGlossMap != null ? new ChannelDescriptor(data.MetallicGlossMap, TextureChannel.Alpha, data.Smoothness)
                : (data.SpecularGlossMap != null ? new ChannelDescriptor(data.SpecularGlossMap, TextureChannel.Alpha, data.Smoothness) : ChannelDescriptor.Constant(data.Smoothness)));
            return MaterialTextureUtility.CreatePackedTexture(data.SourceMaterial, target, role, request, r, g, b, a);
        }

        private static Texture BuildMetallicGlossTexture(MaterialSemanticData data, MaterialConversionRequest request, MaterialConversionTarget target, string role, bool useBaseAlpha)
        {
            ChannelDescriptor r = data.MetallicGlossMap != null ? new ChannelDescriptor(data.MetallicGlossMap, TextureChannel.Red, data.Metallic)
                : (data.MaskMap != null ? new ChannelDescriptor(data.MaskMap, TextureChannel.Red, data.Metallic) : ChannelDescriptor.Constant(data.Metallic));
            ChannelDescriptor a = useBaseAlpha && data.BaseMap != null ? new ChannelDescriptor(data.BaseMap, TextureChannel.Alpha, data.Smoothness)
                : (data.MetallicGlossMap != null ? new ChannelDescriptor(data.MetallicGlossMap, TextureChannel.Alpha, data.Smoothness)
                : (data.MaskMap != null ? new ChannelDescriptor(data.MaskMap, TextureChannel.Alpha, data.Smoothness)
                : (data.SpecularGlossMap != null ? new ChannelDescriptor(data.SpecularGlossMap, TextureChannel.Alpha, data.Smoothness) : ChannelDescriptor.Constant(data.Smoothness))));
            return MaterialTextureUtility.CreatePackedTexture(data.SourceMaterial, target, role, request, r, ChannelDescriptor.Constant(0f), ChannelDescriptor.Constant(0f), a);
        }

        private static Texture BuildSpecGlossTexture(MaterialSemanticData data, MaterialConversionRequest request, MaterialConversionTarget target, string role, bool useBaseAlpha)
        {
            ChannelDescriptor r = data.SpecularGlossMap != null ? new ChannelDescriptor(data.SpecularGlossMap, TextureChannel.Red, data.SpecularColor.r) : ChannelDescriptor.Constant(data.SpecularColor.r);
            ChannelDescriptor g = data.SpecularGlossMap != null ? new ChannelDescriptor(data.SpecularGlossMap, TextureChannel.Green, data.SpecularColor.g) : ChannelDescriptor.Constant(data.SpecularColor.g);
            ChannelDescriptor b = data.SpecularGlossMap != null ? new ChannelDescriptor(data.SpecularGlossMap, TextureChannel.Blue, data.SpecularColor.b) : ChannelDescriptor.Constant(data.SpecularColor.b);
            ChannelDescriptor a = useBaseAlpha && data.BaseMap != null ? new ChannelDescriptor(data.BaseMap, TextureChannel.Alpha, data.Smoothness)
                : (data.SpecularGlossMap != null ? new ChannelDescriptor(data.SpecularGlossMap, TextureChannel.Alpha, data.Smoothness)
                : (data.MaskMap != null ? new ChannelDescriptor(data.MaskMap, TextureChannel.Alpha, data.Smoothness)
                : (data.MetallicGlossMap != null ? new ChannelDescriptor(data.MetallicGlossMap, TextureChannel.Alpha, data.Smoothness) : ChannelDescriptor.Constant(data.Smoothness))));
            return MaterialTextureUtility.CreatePackedTexture(data.SourceMaterial, target, role, request, r, g, b, a);
        }

        private static IEnumerable<string> CollectHdrpDowngradeLosses(Material material)
        {
            List<string> losses = new List<string>();
            if (material == null) return losses;

            foreach (string propertyName in HdrpFeatureProperties)
            {
                if (!material.HasProperty(propertyName)) continue;
                if (propertyName.EndsWith("Map", StringComparison.Ordinal))
                {
                    if (material.GetTexture(propertyName) != null)
                    {
                        losses.Add(GetHdrpFeatureLossDescription(propertyName));
                    }
                }
                else if (Mathf.Abs(material.GetFloat(propertyName)) > 0.0001f)
                {
                    losses.Add(GetHdrpFeatureLossDescription(propertyName));
                }
            }

            return losses.Distinct();
        }

        private static string GetHdrpFeatureLossDescription(string propertyName)
        {
            return propertyName switch
            {
                "_CoatMask" or "_CoatMaskMap" => "Clear-coat layering is HDRP-only and will be removed.",
                "_Anisotropy" or "_AnisotropyMap" => "Anisotropy is HDRP-only and will be flattened.",
                "_TransmissionMask" or "_TransmissionMaskMap" => "Transmission is HDRP-only and will be flattened.",
                "_IridescenceMask" or "_IridescenceMaskMap" => "Iridescence is HDRP-only and will be removed.",
                "_RefractionModel" => "HDRP refraction settings are not preserved outside HDRP.",
                "_HeightPoMAmplitude" => "Parallax depth is HDRP-only and will be flattened.",
                _ => $"HDRP-only feature '{propertyName}' is not preserved outside HDRP."
            };
        }

        private static bool NeedsHdrpMaskMap(MaterialSemanticData data, MaterialConversionTarget target)
        {
            return target == MaterialConversionTarget.HDRPLit &&
                   data.MaskMap == null &&
                   (data.MetallicGlossMap != null || data.OcclusionMap != null || data.DetailMaskMap != null || data.SpecularGlossMap != null);
        }

        private static bool NeedsMetallicGlossTexture(MaterialSemanticData data, MaterialConversionTarget target)
        {
            if (data.MetallicGlossMap != null)
            {
                return false;
            }

            return target == MaterialConversionTarget.BuiltInStandard ||
                   (target == MaterialConversionTarget.URPLit && data.Workflow != MaterialWorkflow.Specular)
                ? data.MaskMap != null || data.SpecularGlossMap != null
                : false;
        }

        private static bool NeedsSpecGlossTexture(MaterialSemanticData data, MaterialConversionTarget target)
        {
            if (data.SpecularGlossMap != null)
            {
                return false;
            }

            return target == MaterialConversionTarget.BuiltInStandardSpecular ||
                   target == MaterialConversionTarget.URPSimpleLit ||
                   (target == MaterialConversionTarget.URPLit && data.Workflow == MaterialWorkflow.Specular)
                ? data.MaskMap != null || data.MetallicGlossMap != null
                : false;
        }

        private static void EnsureHelperTexturesAllowed(MaterialConversionRequest request, string helperDescription)
        {
            if (!request.AllowGeneratedHelperTextures)
            {
                throw new InvalidOperationException($"Conversion needs {helperDescription}, but helper texture generation is disabled.");
            }
        }

        private static Texture FindTexture(Material material, IEnumerable<string> propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName))
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture != null) return texture;
                }
            }

            return null;
        }

        private static Color FindColor(Material material, IEnumerable<string> propertyNames, Color fallback)
        {
            foreach (string propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName)) return material.GetColor(propertyName);
            }

            return fallback;
        }

        private static float FindFloat(Material material, IEnumerable<string> propertyNames, float fallback)
        {
            foreach (string propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName)) return material.GetFloat(propertyName);
            }

            return fallback;
        }

        private static Vector2 GetTextureScale(Material material, IEnumerable<string> propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName)) return material.GetTextureScale(propertyName);
            }

            return Vector2.one;
        }

        private static Vector2 GetTextureOffset(Material material, IEnumerable<string> propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName)) return material.GetTextureOffset(propertyName);
            }

            return Vector2.zero;
        }
    }

    internal enum TextureChannel
    {
        Red,
        Green,
        Blue,
        Alpha
    }

    internal readonly struct ChannelDescriptor
    {
        public ChannelDescriptor(Texture texture, TextureChannel channel, float fallback)
        {
            Texture = texture;
            Channel = channel;
            Fallback = fallback;
        }

        public Texture Texture { get; }
        public TextureChannel Channel { get; }
        public float Fallback { get; }

        public static ChannelDescriptor Constant(float value) => new ChannelDescriptor(null, TextureChannel.Red, value);
    }

    internal static class MaterialTextureUtility
    {
        public static string GetPackedTextureAssetPath(Material sourceMaterial, MaterialConversionTarget target, string role)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            string directory = Path.GetDirectoryName(sourcePath) ?? "Assets";
            string generatedDirectory = Path.Combine(directory, "MaterialConversionGenerated").Replace('\\', '/');
            string readableName = SanitizeFileName(sourceMaterial.name);
            string shortGuid = sourceGuid.Length >= 8 ? sourceGuid.Substring(0, 8) : sourceGuid;
            return Path.Combine(generatedDirectory, $"{readableName}_{target}_{role}_{shortGuid}.png").Replace('\\', '/');
        }

        public static Texture CreatePackedTexture(Material sourceMaterial, MaterialConversionTarget target, string role, MaterialConversionRequest request, ChannelDescriptor red, ChannelDescriptor green, ChannelDescriptor blue, ChannelDescriptor alpha)
        {
            string assetPath = GetPackedTextureAssetPath(sourceMaterial, target, role);
            string generatedDirectory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";
            if (!AssetDatabase.IsValidFolder(generatedDirectory))
            {
                string parent = Path.GetDirectoryName(generatedDirectory)?.Replace('\\', '/') ?? "Assets";
                AssetDatabase.CreateFolder(parent, Path.GetFileName(generatedDirectory));
            }

            if (!request.OverwriteGeneratedAssets && File.Exists(assetPath))
            {
                request.GeneratedAssetPaths.Add(assetPath);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }

            (int width, int height) = DetermineTextureSize(red, green, blue, alpha);
            Texture2D rr = GetReadableCopy(red.Texture, width, height);
            Texture2D rg = GetReadableCopy(green.Texture, width, height);
            Texture2D rb = GetReadableCopy(blue.Texture, width, height);
            Texture2D ra = GetReadableCopy(alpha.Texture, width, height);
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = height > 1 ? y / (float)(height - 1) : 0f;
                for (int x = 0; x < width; x++)
                {
                    float u = width > 1 ? x / (float)(width - 1) : 0f;
                    int index = y * width + x;
                    pixels[index] = new Color(Sample(rr, red, u, v), Sample(rg, green, u, v), Sample(rb, blue, u, v), Sample(ra, alpha, u, v));
                }
            }

            output.SetPixels(pixels);
            output.Apply();
            File.WriteAllBytes(assetPath, output.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }

            UnityEngine.Object.DestroyImmediate(output);
            DestroyImmediateIfSet(rr);
            DestroyImmediateIfSet(rg);
            DestroyImmediateIfSet(rb);
            DestroyImmediateIfSet(ra);
            request.GeneratedAssetPaths.Add(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(name.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "Material" : sanitized;
        }

        private static (int width, int height) DetermineTextureSize(params ChannelDescriptor[] descriptors)
        {
            int width = 4;
            int height = 4;
            foreach (ChannelDescriptor descriptor in descriptors)
            {
                if (descriptor.Texture is Texture2D texture2D)
                {
                    width = Mathf.Max(width, texture2D.width);
                    height = Mathf.Max(height, texture2D.height);
                }
            }

            return (width, height);
        }

        private static Texture2D GetReadableCopy(Texture texture, int width, int height)
        {
            if (texture == null) return null;
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, renderTexture);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
            return readable;
        }

        private static float Sample(Texture2D texture, ChannelDescriptor descriptor, float u, float v)
        {
            if (texture == null) return Mathf.Clamp01(descriptor.Fallback);
            Color color = texture.GetPixelBilinear(u, v);
            return descriptor.Channel switch
            {
                TextureChannel.Red => color.r,
                TextureChannel.Green => color.g,
                TextureChannel.Blue => color.b,
                TextureChannel.Alpha => color.a,
                _ => descriptor.Fallback
            };
        }

        private static void DestroyImmediateIfSet(Texture2D texture)
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
