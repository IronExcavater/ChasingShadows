using System;
using UnityEngine;

namespace IronByte.Tools.MaterialConversion.Editor
{
    public enum MaterialConversionTarget
    {
        BuiltInStandard,
        BuiltInStandardSpecular,
        BuiltInUnlitTexture,
        URPLit,
        URPSimpleLit,
        URPUnlit,
        URPParticlesLit,
        URPParticlesUnlit,
        URPTerrainLit,
        HDRPLit,
        HDRPUnlit,
        HDRPTerrainLit
    }

    public enum MaterialConversionMode
    {
        Copy,
        Replace
    }

    public enum MaterialConversionConfidence
    {
        Official,
        Mapped,
        Heuristic,
        Unsupported
    }

    public enum MaterialSourceFamily
    {
        Unknown,
        BuiltInStandard,
        BuiltInStandardSpecular,
        BuiltInLegacyLit,
        BuiltInLegacyUnlit,
        BuiltInTerrain,
        BuiltInParticle,
        URPLit,
        URPSimpleLit,
        URPUnlit,
        URPParticlesLit,
        URPParticlesUnlit,
        URPTerrainLit,
        HDRPLit,
        HDRPUnlit,
        HDRPTerrainLit,
        Custom
    }

    public enum MaterialWorkflow
    {
        Unlit,
        Metallic,
        Specular,
        Terrain,
        ParticleLit,
        ParticleUnlit
    }

    public enum MaterialAlphaMode
    {
        Opaque,
        Cutout,
        Fade,
        Transparent,
        Premultiply,
        Additive,
        Multiply
    }

    [Serializable]
    public sealed class MaterialConversionRequest
    {
        public MaterialConversionRequest(Material sourceMaterial, MaterialConversionTarget target, MaterialConversionMode mode, string copySuffix = "_Converted")
        {
            SourceMaterial = sourceMaterial;
            Target = target;
            Mode = mode;
            CopySuffix = string.IsNullOrWhiteSpace(copySuffix) ? "_Converted" : copySuffix;
        }

        public Material SourceMaterial { get; }
        public MaterialConversionTarget Target { get; }
        public MaterialConversionMode Mode { get; }
        public string CopySuffix { get; }
        public bool OverwriteGeneratedAssets { get; set; } = true;
        public bool AllowGeneratedHelperTextures { get; set; } = true;
        internal System.Collections.Generic.List<string> GeneratedAssetPaths { get; } = new System.Collections.Generic.List<string>();
    }

    [Serializable]
    public sealed class MaterialConversionResult
    {
        public Material SourceMaterial { get; set; }
        public Material ResultMaterial { get; set; }
        public string SourcePath { get; set; }
        public string ResultPath { get; set; }
        public MaterialSourceFamily SourceFamily { get; set; }
        public MaterialConversionTarget Target { get; set; }
        public MaterialConversionConfidence Confidence { get; set; }
        public bool Success { get; set; }
        public string Summary { get; set; }
        public string[] Notes { get; set; } = Array.Empty<string>();
        public string[] Losses { get; set; } = Array.Empty<string>();
        public string[] ExpectedGeneratedAssets { get; set; } = Array.Empty<string>();
        public string[] ExpectedGeneratedAssetPaths { get; set; } = Array.Empty<string>();
        public string[] GeneratedAssets { get; set; } = Array.Empty<string>();
        public int StrengthScore { get; set; }
        public string StrengthLabel { get; set; } = string.Empty;
        public string StrengthSummary { get; set; } = string.Empty;
        public bool Skipped { get; set; }
        public int RemappedReferenceCount { get; set; }
        public string[] Warnings
        {
            get => Notes;
            set => Notes = value ?? Array.Empty<string>();
        }
    }

    [Serializable]
    public sealed class MaterialSemanticData
    {
        public Material SourceMaterial { get; set; }
        public MaterialSourceFamily SourceFamily { get; set; }
        public MaterialWorkflow Workflow { get; set; }
        public MaterialAlphaMode AlphaMode { get; set; }
        public bool AlphaClip { get; set; }
        public float Cutoff { get; set; }
        public int CullMode { get; set; } = 2;
        public bool DoubleSided { get; set; }
        public bool IsTerrain { get; set; }
        public bool IsParticle { get; set; }
        public Texture BaseMap { get; set; }
        public Color BaseColor { get; set; } = Color.white;
        public Vector2 BaseMapScale { get; set; } = Vector2.one;
        public Vector2 BaseMapOffset { get; set; } = Vector2.zero;
        public Texture NormalMap { get; set; }
        public float NormalScale { get; set; } = 1f;
        public float Metallic { get; set; }
        public Texture MetallicGlossMap { get; set; }
        public float Smoothness { get; set; } = 0.5f;
        public int SmoothnessTextureChannel { get; set; }
        public Color SpecularColor { get; set; } = new Color(0.2f, 0.2f, 0.2f, 1f);
        public Texture SpecularGlossMap { get; set; }
        public Texture OcclusionMap { get; set; }
        public float OcclusionStrength { get; set; } = 1f;
        public Texture EmissionMap { get; set; }
        public Color EmissionColor { get; set; } = Color.black;
        public bool EmissionEnabled { get; set; }
        public Texture HeightMap { get; set; }
        public float HeightScale { get; set; }
        public Texture DetailMaskMap { get; set; }
        public Texture DetailAlbedoMap { get; set; }
        public Texture DetailNormalMap { get; set; }
        public float DetailNormalScale { get; set; } = 1f;
        public Texture MaskMap { get; set; }
        public Texture[] TerrainDiffuseMaps { get; } = new Texture[4];
        public Texture[] TerrainNormalMaps { get; } = new Texture[4];
        public Texture[] TerrainMaskMaps { get; } = new Texture[4];
        public float[] TerrainMetallicValues { get; } = new float[4];
        public float[] TerrainSmoothnessValues { get; } = new float[4];
        public Texture TerrainControlMap { get; set; }
        public Texture TerrainHolesTexture { get; set; }
        public float TerrainHeightBlend { get; set; }
        public float TerrainHeightTransition { get; set; }
    }
}
