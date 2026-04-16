using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronByte.Tools.MaterialConversion.Editor.Tests
{
    public sealed class MaterialConversionServiceTests
    {
        private const string FixtureRoot = "Assets/IronByte/Editor/Tools/MaterialConversion/Tests/Fixtures";
        private const string HeuristicShaderPath = "Assets/IronByte/Editor/Tools/MaterialConversion/Tests/Fixtures/HeuristicAlias.shader";
        private const string UnsupportedShaderPath = "Assets/IronByte/Editor/Tools/MaterialConversion/Tests/Fixtures/UnsupportedMinimal.shader";

        private const string StandardShaderName = "Standard";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string HdrpLitShaderName = "HDRP/Lit";

        private string testFolderPath;
        private string[] rawAssetPathsBefore;
        private SerializationMode originalSerializationMode;

        [SetUp]
        public void SetUp()
        {
            originalSerializationMode = EditorSettings.serializationMode;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            rawAssetPathsBefore = Directory.GetFiles(Application.dataPath, "raw_*", SearchOption.TopDirectoryOnly);
            testFolderPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(FixtureRoot, Guid.NewGuid().ToString("N")));
        }

        [TearDown]
        public void TearDown()
        {
            EditorSettings.serializationMode = originalSerializationMode;
            if (!string.IsNullOrEmpty(testFolderPath))
            {
                AssetDatabase.DeleteAsset(testFolderPath);
            }

            DeleteNewRawAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void BuiltInStandardToUrpLitCopy_UsesOfficialUpgraderAndPreservesCoreInputs()
        {
            Material source = CreateBuiltInStandardSource("BuiltInToUrp");
            string sourcePath = AssetDatabase.GetAssetPath(source);

            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.URPLit, MaterialConversionMode.Copy, "_URP"));

            Assert.That(result.Success, Is.True, result.Summary);
            Assert.That(result.Confidence, Is.EqualTo(MaterialConversionConfidence.Official));
            Assert.That(result.ResultPath, Is.Not.EqualTo(sourcePath));
            Assert.That(source.shader.name, Is.EqualTo(StandardShaderName));
            Assert.That(result.ResultMaterial.shader.name, Is.EqualTo(UrpLitShaderName));
            Assert.That(result.ResultMaterial.GetTexture("_BaseMap"), Is.EqualTo(source.GetTexture("_MainTex")));
            Assert.That(result.ResultMaterial.GetTexture("_BumpMap"), Is.EqualTo(source.GetTexture("_BumpMap")));
            Assert.That(result.ResultMaterial.GetTexture("_OcclusionMap"), Is.EqualTo(source.GetTexture("_OcclusionMap")));
            Assert.That(result.ResultMaterial.GetTexture("_EmissionMap"), Is.EqualTo(source.GetTexture("_EmissionMap")));
            AssertColorsClose(source.GetColor("_Color"), result.ResultMaterial.GetColor("_BaseColor"));
        }

        [Test]
        public void BuiltInStandardToHdrpLitCopy_UsesOfficialUpgrader()
        {
            Material source = CreateBuiltInStandardSource("BuiltInToHdrp");

            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.HDRPLit, MaterialConversionMode.Copy, "_HDRP"));

            Assert.That(result.Success, Is.True, result.Summary);
            Assert.That(result.Confidence, Is.EqualTo(MaterialConversionConfidence.Official));
            Assert.That(result.ResultMaterial.shader.name, Is.EqualTo(HdrpLitShaderName));
            Assert.That(result.ResultMaterial.GetTexture("_BaseColorMap"), Is.EqualTo(source.GetTexture("_MainTex")));
            Assert.That(result.ResultMaterial.GetTexture("_NormalMap"), Is.EqualTo(source.GetTexture("_BumpMap")));
            Assert.That(result.ResultMaterial.GetTexture("_MaskMap"), Is.Not.Null);
        }

        [Test]
        public void UrpLitToBuiltInStandardCopy_PreservesMappedProperties()
        {
            Material source = CreateUrpLitSource("UrpToBuiltIn", transparent: true);

            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.BuiltInStandard, MaterialConversionMode.Copy, "_Standard"));

            Assert.That(result.Success, Is.True, result.Summary);
            Assert.That(result.Confidence, Is.EqualTo(MaterialConversionConfidence.Mapped));
            Assert.That(result.ResultMaterial.shader.name, Is.EqualTo(StandardShaderName));
            Assert.That(result.ResultMaterial.GetTexture("_MainTex"), Is.EqualTo(source.GetTexture("_BaseMap")));
            Assert.That(result.ResultMaterial.GetTexture("_BumpMap"), Is.EqualTo(source.GetTexture("_BumpMap")));
            Assert.That(result.ResultMaterial.GetTexture("_EmissionMap"), Is.EqualTo(source.GetTexture("_EmissionMap")));
            Assert.That(result.ResultMaterial.GetTexture("_OcclusionMap"), Is.EqualTo(source.GetTexture("_OcclusionMap")));
            Assert.That(result.ResultMaterial.GetFloat("_Mode"), Is.EqualTo(3f));
            AssertColorsClose(source.GetColor("_BaseColor"), result.ResultMaterial.GetColor("_Color"));
        }

        [Test]
        public void UrpLitToHdrpLitCopy_CreatesGeneratedMaskMap()
        {
            Material source = CreateUrpLitSource("UrpToHdrp", transparent: false);
            source.SetTexture("_MetallicGlossMap", CreateTextureAsset("UrpToHdrp_Metallic", new Color(0.8f, 0f, 0f, 0.4f)));
            source.SetTexture("_OcclusionMap", CreateTextureAsset("UrpToHdrp_Occlusion", new Color(0f, 0.5f, 0f, 1f)));
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();

            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.HDRPLit, MaterialConversionMode.Copy, "_HDRP"));

            Texture maskMap = result.ResultMaterial.GetTexture("_MaskMap");
            Assert.That(result.Success, Is.True, result.Summary);
            Assert.That(result.Confidence, Is.EqualTo(MaterialConversionConfidence.Mapped));
            Assert.That(result.ResultMaterial.shader.name, Is.EqualTo(HdrpLitShaderName));
            Assert.That(maskMap, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(maskMap), Does.Contain("MaterialConversionGenerated"));
            Assert.That(result.ResultMaterial.GetTexture("_BaseColorMap"), Is.EqualTo(source.GetTexture("_BaseMap")));
            Assert.That(result.ResultMaterial.GetTexture("_NormalMap"), Is.EqualTo(source.GetTexture("_BumpMap")));
        }

        [Test]
        public void HdrpLitToUrpLitCopy_CreatesGeneratedMetallicGlossMap()
        {
            Material source = CreateHdrpLitSource("HdrpToUrp");

            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.URPLit, MaterialConversionMode.Copy, "_URP"));

            Texture metallicGlossMap = result.ResultMaterial.GetTexture("_MetallicGlossMap");
            Assert.That(result.Success, Is.True, result.Summary);
            Assert.That(result.Confidence, Is.EqualTo(MaterialConversionConfidence.Mapped));
            Assert.That(result.ResultMaterial.shader.name, Is.EqualTo(UrpLitShaderName));
            Assert.That(metallicGlossMap, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(metallicGlossMap), Does.Contain("MaterialConversionGenerated"));
            Assert.That(result.ResultMaterial.GetTexture("_BaseMap"), Is.EqualTo(source.GetTexture("_BaseColorMap")));
            Assert.That(result.ResultMaterial.GetTexture("_BumpMap"), Is.EqualTo(source.GetTexture("_NormalMap")));
            Assert.That(result.ResultMaterial.GetTexture("_EmissionMap"), Is.EqualTo(source.GetTexture("_EmissiveColorMap")));
        }

        [Test]
        public void ScalarOnlyConversion_DoesNotCreateHelperTextures()
        {
            Material source = CreateUrpLitSource("ScalarOnly", transparent: false);
            source.SetTexture("_MetallicGlossMap", null);
            source.SetTexture("_OcclusionMap", null);
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();

            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.HDRPLit, MaterialConversionMode.Copy, "_HDRP"));

            Assert.That(result.Success, Is.True, result.Summary);
            Assert.That(result.GeneratedAssets, Is.Empty);
            Assert.That(result.ExpectedGeneratedAssets, Is.Empty);
        }

        [Test]
        public void ReplaceMode_KeepsOriginalAssetPathAndConvertsInPlace()
        {
            Material source = CreateUrpLitSource("ReplaceMode", transparent: false);
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);

            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.BuiltInStandard, MaterialConversionMode.Replace));

            Assert.That(result.Success, Is.True, result.Summary);
            Assert.That(result.ResultPath, Is.EqualTo(sourcePath));
            Assert.That(AssetDatabase.AssetPathToGUID(result.ResultPath), Is.EqualTo(sourceGuid));
            Assert.That(source.shader.name, Is.EqualTo(StandardShaderName));
            Assert.That(result.ResultMaterial, Is.EqualTo(source));
        }

        [Test]
        public void CopyMode_SkipsWhenMaterialAlreadyUsesTargetShader()
        {
            Material source = CreateUrpLitSource("AlreadyTarget", transparent: false);
            int materialCountBefore = AssetDatabase.FindAssets("t:Material", new[] { testFolderPath }).Length;

            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.URPLit, MaterialConversionMode.Copy, "_URP"));

            int materialCountAfter = AssetDatabase.FindAssets("t:Material", new[] { testFolderPath }).Length;
            Assert.That(result.Success, Is.False);
            Assert.That(result.Skipped, Is.True);
            Assert.That(result.Summary, Does.Contain("already uses"));
            Assert.That(materialCountAfter, Is.EqualTo(materialCountBefore));
        }

        [Test]
        public void CopyMode_SkipsWhenConvertedCopyAlreadyExists()
        {
            Material source = CreateUrpLitSource("ExistingCopy", transparent: false);
            MaterialConversionRequest request = new MaterialConversionRequest(source, MaterialConversionTarget.BuiltInStandard, MaterialConversionMode.Copy, "_Standard");

            MaterialConversionResult firstResult = MaterialConversionService.Convert(request);
            MaterialConversionResult secondResult = MaterialConversionService.Convert(request);

            Assert.That(firstResult.Success, Is.True, firstResult.Summary);
            Assert.That(secondResult.Success, Is.False);
            Assert.That(secondResult.Skipped, Is.True);
            Assert.That(secondResult.ResultPath, Is.EqualTo(firstResult.ResultPath));
            Assert.That(secondResult.Summary, Does.Contain("already exists"));
            Assert.That(AssetDatabase.FindAssets("t:Material", new[] { testFolderPath }), Has.Length.EqualTo(2));
        }

        [Test]
        public void HistoryEntry_UndoRedoReplaceRestoresMaterialState()
        {
            Material source = CreateUrpLitSource("HistoryReplace", transparent: false);
            Dictionary<string, AssetFileSnapshot> beforeSnapshots = MaterialConversionHistoryUtility.CaptureSnapshots(new[] { AssetDatabase.GetAssetPath(source) });

            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.BuiltInStandard, MaterialConversionMode.Replace));

            MaterialConversionHistoryEntry entry = MaterialConversionHistoryUtility.CreateEntry(
                "Replace history test",
                new[] { result },
                new MaterialReferenceRemapResult(),
                beforeSnapshots);

            Assert.That(source.shader.name, Is.EqualTo(StandardShaderName));
            entry.Undo();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(source), ImportAssetOptions.ForceUpdate);
            Material afterUndo = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GetAssetPath(source));
            Assert.That(afterUndo.shader.name, Is.EqualTo(UrpLitShaderName));

            entry.Redo();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(source), ImportAssetOptions.ForceUpdate);
            Material afterRedo = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GetAssetPath(source));
            Assert.That(afterRedo.shader.name, Is.EqualTo(StandardShaderName));
        }

        [Test]
        public void Analyze_ReturnsHeuristicForAliasShader_AndUnsupportedForUnknownShader()
        {
            Shader heuristicShader = AssetDatabase.LoadAssetAtPath<Shader>(HeuristicShaderPath);
            Shader unsupportedShader = AssetDatabase.LoadAssetAtPath<Shader>(UnsupportedShaderPath);
            Assert.That(heuristicShader, Is.Not.Null);
            Assert.That(unsupportedShader, Is.Not.Null);

            Material heuristicMaterial = CreateMaterialAsset("Heuristic", heuristicShader);
            heuristicMaterial.SetTexture("_MainTex", CreateTextureAsset("Heuristic_Base", new Color(0.4f, 0.7f, 0.2f, 1f)));
            heuristicMaterial.SetColor("_Color", new Color(0.7f, 0.8f, 0.9f, 1f));
            heuristicMaterial.SetTexture("_BumpMap", CreateTextureAsset("Heuristic_Normal", new Color(0.5f, 0.5f, 1f, 1f), linear: true));
            heuristicMaterial.SetTexture("_MetallicGlossMap", CreateTextureAsset("Heuristic_Metallic", new Color(0.9f, 0f, 0f, 0.6f), linear: true));
            EditorUtility.SetDirty(heuristicMaterial);

            Material unsupportedMaterial = CreateMaterialAsset("Unsupported", unsupportedShader);
            unsupportedMaterial.SetFloat("_Noise", 0.75f);
            EditorUtility.SetDirty(unsupportedMaterial);
            AssetDatabase.SaveAssets();

            MaterialConversionResult heuristicResult = MaterialConversionService.Analyze(heuristicMaterial, MaterialConversionTarget.URPLit);
            MaterialConversionResult unsupportedResult = MaterialConversionService.Analyze(unsupportedMaterial, MaterialConversionTarget.URPLit);

            Assert.That(heuristicResult.Success, Is.True, heuristicResult.Summary);
            Assert.That(heuristicResult.Confidence, Is.EqualTo(MaterialConversionConfidence.Heuristic));
            Assert.That(heuristicResult.SourceFamily, Is.EqualTo(MaterialSourceFamily.Custom));
            Assert.That(unsupportedResult.Success, Is.False);
            Assert.That(unsupportedResult.Confidence, Is.EqualTo(MaterialConversionConfidence.Unsupported));
        }

        [Test]
        public void CopyMode_RemapperCanSwitchYamlReferencesToConvertedMaterial()
        {
            Material source = CreateUrpLitSource("ReferenceRemap", transparent: false);
            MaterialConversionResult result = MaterialConversionService.Convert(
                new MaterialConversionRequest(source, MaterialConversionTarget.BuiltInStandard, MaterialConversionMode.Copy, "_Standard"));

            MaterialReferenceHolder holder = ScriptableObject.CreateInstance<MaterialReferenceHolder>();
            holder.material = source;
            holder.materials = new[] { source };
            string holderPath = Path.Combine(testFolderPath, "ReferenceHolder.asset").Replace('\\', '/');
            AssetDatabase.CreateAsset(holder, holderPath);
            EditorUtility.SetDirty(holder);
            AssetDatabase.SaveAssets();

            List<string> notes = new List<string>();
            int updatedAssets = MaterialReferenceRemapper.RemapProjectReferences(
                new Dictionary<Material, Material> { { source, result.ResultMaterial } },
                notes);

            AssetDatabase.ImportAsset(holderPath, ImportAssetOptions.ForceUpdate);
            MaterialReferenceHolder reloaded = AssetDatabase.LoadAssetAtPath<MaterialReferenceHolder>(holderPath);
            Assert.That(updatedAssets, Is.GreaterThan(0));
            Assert.That(reloaded.material, Is.EqualTo(result.ResultMaterial));
            Assert.That(reloaded.materials, Has.Length.EqualTo(1));
            Assert.That(reloaded.materials[0], Is.EqualTo(result.ResultMaterial));
        }

        [Test]
        public void HistoryEntry_UndoRedoCopyAndRemapRestoresReferencesAndCopy()
        {
            Material source = CreateUrpLitSource("HistoryCopy", transparent: false);
            MaterialReferenceHolder holder = ScriptableObject.CreateInstance<MaterialReferenceHolder>();
            holder.material = source;
            holder.materials = new[] { source };
            string holderPath = Path.Combine(testFolderPath, "HistoryReferenceHolder.asset").Replace('\\', '/');
            AssetDatabase.CreateAsset(holder, holderPath);
            EditorUtility.SetDirty(holder);
            AssetDatabase.SaveAssets();

            MaterialConversionRequest request = new MaterialConversionRequest(source, MaterialConversionTarget.BuiltInStandard, MaterialConversionMode.Copy, "_Standard");
            MaterialConversionResult result = MaterialConversionService.Convert(request);
            Dictionary<string, AssetFileSnapshot> beforeSnapshots = MaterialConversionHistoryUtility.CaptureSnapshots(result.ExpectedGeneratedAssetPaths);

            MaterialReferenceRemapResult remapResult = MaterialReferenceRemapper.RemapProjectReferences(
                new Dictionary<Material, Material> { { source, result.ResultMaterial } });

            MaterialConversionHistoryEntry entry = MaterialConversionHistoryUtility.CreateEntry(
                "Copy history test",
                new[] { result },
                remapResult,
                beforeSnapshots);

            Assert.That(AssetDatabase.LoadAssetAtPath<MaterialReferenceHolder>(holderPath).material, Is.EqualTo(result.ResultMaterial));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(result.ResultPath), Is.Not.Null);

            entry.Undo();
            AssetDatabase.Refresh();
            MaterialReferenceHolder afterUndo = AssetDatabase.LoadAssetAtPath<MaterialReferenceHolder>(holderPath);
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(result.ResultPath), Is.Null);
            Assert.That(afterUndo.material, Is.EqualTo(source));

            entry.Redo();
            AssetDatabase.Refresh();
            MaterialReferenceHolder afterRedo = AssetDatabase.LoadAssetAtPath<MaterialReferenceHolder>(holderPath);
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(result.ResultPath), Is.Not.Null);
            Assert.That(afterRedo.material, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Material>(result.ResultPath)));
        }

        private Material CreateBuiltInStandardSource(string name)
        {
            Material material = CreateMaterialAsset(name, RequireShader(StandardShaderName));
            material.SetTexture("_MainTex", CreateTextureAsset(name + "_Base", new Color(0.7f, 0.4f, 0.2f, 1f)));
            material.SetColor("_Color", new Color(0.8f, 0.6f, 0.5f, 0.75f));
            material.SetTexture("_BumpMap", CreateTextureAsset(name + "_Normal", new Color(0.5f, 0.5f, 1f, 1f), linear: true));
            material.SetTexture("_MetallicGlossMap", CreateTextureAsset(name + "_Metallic", new Color(0.6f, 0f, 0f, 0.5f), linear: true));
            material.SetFloat("_Metallic", 0.6f);
            material.SetFloat("_Glossiness", 0.5f);
            material.SetTexture("_OcclusionMap", CreateTextureAsset(name + "_Occlusion", new Color(0f, 0.8f, 0f, 1f), linear: true));
            material.SetTexture("_EmissionMap", CreateTextureAsset(name + "_Emission", new Color(0.2f, 0.3f, 0.9f, 1f)));
            material.SetColor("_EmissionColor", new Color(0.3f, 0.4f, 1f, 1f));
            material.EnableKeyword("_EMISSION");
            material.SetFloat("_Mode", 3f);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private Material CreateUrpLitSource(string name, bool transparent)
        {
            Material material = CreateMaterialAsset(name, RequireShader(UrpLitShaderName));
            material.SetTexture("_BaseMap", CreateTextureAsset(name + "_Base", new Color(0.2f, 0.5f, 0.8f, transparent ? 0.6f : 1f)));
            material.SetColor("_BaseColor", new Color(0.3f, 0.6f, 0.8f, transparent ? 0.6f : 1f));
            material.SetTexture("_BumpMap", CreateTextureAsset(name + "_Normal", new Color(0.5f, 0.5f, 1f, 1f), linear: true));
            material.SetTexture("_MetallicGlossMap", CreateTextureAsset(name + "_Metallic", new Color(0.7f, 0f, 0f, 0.4f), linear: true));
            material.SetTexture("_OcclusionMap", CreateTextureAsset(name + "_Occlusion", new Color(0f, 0.65f, 0f, 1f), linear: true));
            material.SetTexture("_EmissionMap", CreateTextureAsset(name + "_Emission", new Color(0.9f, 0.6f, 0.1f, 1f)));
            material.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.2f, 1f));
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_Metallic", 0.7f);
            material.SetFloat("_Smoothness", 0.4f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_OcclusionStrength", 0.65f);
            material.EnableKeyword("_EMISSION");
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            if (transparent)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private Material CreateHdrpLitSource(string name)
        {
            Material material = CreateMaterialAsset(name, RequireShader(HdrpLitShaderName));
            material.SetTexture("_BaseColorMap", CreateTextureAsset(name + "_Base", new Color(0.5f, 0.2f, 0.8f, 1f)));
            material.SetColor("_BaseColor", new Color(0.6f, 0.3f, 0.8f, 1f));
            material.SetTexture("_NormalMap", CreateTextureAsset(name + "_Normal", new Color(0.5f, 0.5f, 1f, 1f), linear: true));
            material.SetTexture("_MaskMap", CreateTextureAsset(name + "_Mask", new Color(0.8f, 0.55f, 1f, 0.35f), linear: true));
            material.SetTexture("_EmissiveColorMap", CreateTextureAsset(name + "_Emission", new Color(0.1f, 0.9f, 0.7f, 1f)));
            material.SetColor("_EmissiveColor", new Color(0.2f, 1f, 0.8f, 1f));
            material.SetFloat("_Metallic", 0.8f);
            material.SetFloat("_Smoothness", 0.35f);
            material.SetFloat("_AORemapMax", 0.55f);
            material.SetFloat("_SurfaceType", 0f);
            material.SetFloat("_MaterialID", 1f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private Material CreateMaterialAsset(string name, Shader shader)
        {
            Material material = new Material(shader) { name = name };
            string path = Path.Combine(testFolderPath, name + ".mat").Replace('\\', '/');
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private Texture2D CreateTextureAsset(string name, Color color, bool linear = false)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear)
            {
                name = name
            };

            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply(false, false);

            string path = Path.Combine(testFolderPath, name + ".png").Replace('\\', '/');
            File.WriteAllBytes(AssetPathToFileSystemPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = !linear;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Shader RequireShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, $"Required shader '{shaderName}' is missing.");
            return shader;
        }

        private static string AssetPathToFileSystemPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? throw new InvalidOperationException("Unable to resolve Unity project root.");
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }

        private void DeleteNewRawAssets()
        {
            foreach (string filePath in Directory.GetFiles(Application.dataPath, "raw_*", SearchOption.TopDirectoryOnly))
            {
                bool existedBefore = Array.Exists(rawAssetPathsBefore, existingPath => string.Equals(existingPath, filePath, StringComparison.OrdinalIgnoreCase));
                if (!existedBefore)
                {
                    AssetDatabase.DeleteAsset("Assets/" + Path.GetFileName(filePath));
                }
            }
        }

        private static void AssertColorsClose(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
        }
    }
}
