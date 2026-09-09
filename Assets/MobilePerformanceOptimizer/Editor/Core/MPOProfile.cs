using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class MPOProfile
    {
        public MPOTargetPlatform TargetPlatform { get; }
        public MPODeviceTier DeviceTier { get; }

        public long MaxLoadedSceneTriangles { get; }
        public int MaxLoadedSceneRenderers { get; }
        public int MaxRealtimeLights { get; }
        public int MaxRealtimeShadowLights { get; }
        public int MaxMaterialsPerRenderer { get; }
        public int MaxMeshTriangles { get; }
        public int HighPolyMeshLodThreshold { get; }
        public int MaxEnvironmentTextureSize { get; }
        public int MaxCharacterTextureSize { get; }
        public int MaxUiTextureSize { get; }
        public int MaxOtherTextureSize { get; }
        public float MaxUrpRenderScale { get; }
        public int MaxUrpMsaaSamples { get; }
        public float MaxShadowDistance { get; }
        public int MaxShadowCascades { get; }
        public bool RecommendHdrOff { get; }

        public int MaxParticleSystems { get; }
        public int MaxParticlesPerSystem { get; }
        public int MaxCollisionParticleSystems { get; }
        public int MaxDynamicRigidbodies { get; }
        public int MaxMeshColliders { get; }
        public float MinRecommendedFixedDeltaTime { get; }
        public int MaxCanvases { get; }
        public int MaxRaycastTargets { get; }
        public int MaxLayoutComponents { get; }
        public int LongAudioSeconds { get; }
        public int VeryLongAudioSeconds { get; }
        public int MaxQualityMsaaSamples { get; }
        public float MaxQualityShadowDistance { get; }
        public float MinLodBias { get; }

        private MPOProfile(
            MPOTargetPlatform targetPlatform,
            MPODeviceTier deviceTier,
            long maxLoadedSceneTriangles,
            int maxLoadedSceneRenderers,
            int maxRealtimeLights,
            int maxRealtimeShadowLights,
            int maxMaterialsPerRenderer,
            int maxMeshTriangles,
            int highPolyMeshLodThreshold,
            int maxEnvironmentTextureSize,
            int maxCharacterTextureSize,
            int maxUiTextureSize,
            int maxOtherTextureSize,
            float maxUrpRenderScale,
            int maxUrpMsaaSamples,
            float maxShadowDistance,
            int maxShadowCascades,
            bool recommendHdrOff,
            int maxParticleSystems,
            int maxParticlesPerSystem,
            int maxCollisionParticleSystems,
            int maxDynamicRigidbodies,
            int maxMeshColliders,
            float minRecommendedFixedDeltaTime,
            int maxCanvases,
            int maxRaycastTargets,
            int maxLayoutComponents,
            int longAudioSeconds,
            int veryLongAudioSeconds,
            int maxQualityMsaaSamples,
            float maxQualityShadowDistance,
            float minLodBias)
        {
            TargetPlatform = targetPlatform;
            DeviceTier = deviceTier;
            MaxLoadedSceneTriangles = maxLoadedSceneTriangles;
            MaxLoadedSceneRenderers = maxLoadedSceneRenderers;
            MaxRealtimeLights = maxRealtimeLights;
            MaxRealtimeShadowLights = maxRealtimeShadowLights;
            MaxMaterialsPerRenderer = maxMaterialsPerRenderer;
            MaxMeshTriangles = maxMeshTriangles;
            HighPolyMeshLodThreshold = highPolyMeshLodThreshold;
            MaxEnvironmentTextureSize = maxEnvironmentTextureSize;
            MaxCharacterTextureSize = maxCharacterTextureSize;
            MaxUiTextureSize = maxUiTextureSize;
            MaxOtherTextureSize = maxOtherTextureSize;
            MaxUrpRenderScale = maxUrpRenderScale;
            MaxUrpMsaaSamples = maxUrpMsaaSamples;
            MaxShadowDistance = maxShadowDistance;
            MaxShadowCascades = maxShadowCascades;
            RecommendHdrOff = recommendHdrOff;
            MaxParticleSystems = maxParticleSystems;
            MaxParticlesPerSystem = maxParticlesPerSystem;
            MaxCollisionParticleSystems = maxCollisionParticleSystems;
            MaxDynamicRigidbodies = maxDynamicRigidbodies;
            MaxMeshColliders = maxMeshColliders;
            MinRecommendedFixedDeltaTime = minRecommendedFixedDeltaTime;
            MaxCanvases = maxCanvases;
            MaxRaycastTargets = maxRaycastTargets;
            MaxLayoutComponents = maxLayoutComponents;
            LongAudioSeconds = longAudioSeconds;
            VeryLongAudioSeconds = veryLongAudioSeconds;
            MaxQualityMsaaSamples = maxQualityMsaaSamples;
            MaxQualityShadowDistance = maxQualityShadowDistance;
            MinLodBias = minLodBias;
        }

        public static MPOProfile Create(MPOTargetPlatform platform, MPODeviceTier tier)
        {
            switch (tier)
            {
                case MPODeviceTier.LowEnd:
                    return new MPOProfile(
                        platform, tier,
                        1_500_000, 1800, 4, 2, 3,
                        80_000, 35_000,
                        1024, 2048, 2048, 1024,
                        0.85f, 2, 30f, 2, true,
                        60, 2500, 3,
                        80, 35, 1f / 50f,
                        6, 250, 80,
                        30, 120,
                        2, 35f, 0.7f);

                case MPODeviceTier.HighEnd:
                    return new MPOProfile(
                        platform, tier,
                        5_000_000, 5000, 14, 8, 6,
                        300_000, 120_000,
                        4096, 4096, 4096, 2048,
                        1.0f, 4, 80f, 4, false,
                        180, 12000, 12,
                        260, 120, 1f / 90f,
                        16, 1000, 300,
                        60, 240,
                        4, 100f, 1.5f);

                default:
                    return new MPOProfile(
                        platform, tier,
                        3_000_000, 3200, 8, 4, 4,
                        160_000, 70_000,
                        2048, 2048, 2048, 2048,
                        1.0f, 4, 50f, 2, false,
                        110, 6000, 7,
                        150, 70, 1f / 60f,
                        10, 550, 160,
                        45, 180,
                        4, 60f, 1.0f);
            }
        }

        public string PlatformTextureSettingsName =>
            TargetPlatform == MPOTargetPlatform.Android ? "Android" : "iPhone";

        public UnityEditor.BuildTargetGroup BuildTargetGroup =>
            TargetPlatform == MPOTargetPlatform.Android ? UnityEditor.BuildTargetGroup.Android : UnityEditor.BuildTargetGroup.iOS;

        public string DisplayName => $"{TargetPlatform} / {DeviceTier}";

        public string PreferredTextureCompression =>
            TargetPlatform == MPOTargetPlatform.Android
                ? "ASTC where supported, with an ETC2-compatible fallback strategy for broader Android coverage"
                : "ASTC for supported iOS devices";

        public string PlatformGuidance =>
            TargetPlatform == MPOTargetPlatform.Android
                ? "Android hardware varies widely, so validate memory, thermals and GPU cost on at least one device near the low end of your supported range."
                : "iOS hardware is more consistent, but older supported devices can still be limited by memory bandwidth, thermals and GPU fill-rate.";

        public int MaxSkinnedMeshBones =>
            DeviceTier == MPODeviceTier.LowEnd ? 60 : DeviceTier == MPODeviceTier.HighEnd ? 160 : 100;

        public int MaxBlendShapes =>
            DeviceTier == MPODeviceTier.LowEnd ? 16 : DeviceTier == MPODeviceTier.HighEnd ? 64 : 32;

        public int GetRecommendedTextureSize(string assetPath, UnityEditor.TextureImporter importer)
        {
            if (importer != null && importer.textureType == UnityEditor.TextureImporterType.Sprite)
                return MaxUiTextureSize;

            string lower = (assetPath ?? string.Empty).ToLowerInvariant();

            if (ContainsAny(lower, "/ui/", "/gui/", "icon", "hud", "button", "panel"))
                return MaxUiTextureSize;

            if (ContainsAny(lower, "character", "player", "enemy", "npc", "dino", "creature"))
                return MaxCharacterTextureSize;

            if (ContainsAny(lower, "environment", "terrain", "ground", "rock", "tree", "building", "prop"))
                return MaxEnvironmentTextureSize;

            return MaxOtherTextureSize;
        }

        private static bool ContainsAny(string source, params string[] tokens)
        {
            foreach (string token in tokens)
            {
                if (source.Contains(token))
                    return true;
            }

            return false;
        }
    }
}
