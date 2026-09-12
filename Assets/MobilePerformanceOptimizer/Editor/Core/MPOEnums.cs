namespace MobilePerformanceOptimizer
{
    public enum MPOTargetPlatform
    {
        Android = 0,
        iOS = 1
    }

    public enum MPODeviceTier
    {
        LowEnd = 0,
        MidRange = 1,
        HighEnd = 2
    }

    public enum MPOSeverity
    {
        Suggestion = 0,
        Warning = 1,
        Critical = 2
    }

    public enum MPOImpactLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum MPOCategory
    {
        Scene = 0,
        Textures = 1,
        Meshes = 2,
        Materials = 3,
        Lighting = 4,
        URP = 5,
        Particles = 6,
        Physics = 7,
        UI = 8,
        Audio = 9,
        Build = 10,
        Quality = 11
    }

    public enum MPOFixSafety
    {
        Manual = 0,
        ReviewRequired = 1,
        Safe = 2
    }

    public enum MPOFixKind
    {
        None = 0,
        DisableDevelopmentBuildFlags = 1,
        DisableTextureReadWrite = 2,
        DisableMeshReadWrite = 3,
        StreamLongAudio = 4,
        SetTexturePlatformMaxSize = 5,
        EnableMaterialGpuInstancing = 6,
        DisableTextureMipmaps = 7,
        ReviewSettings = 8
    }
}
