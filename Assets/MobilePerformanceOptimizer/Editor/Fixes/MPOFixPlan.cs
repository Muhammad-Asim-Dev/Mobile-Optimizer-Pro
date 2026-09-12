using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MobilePerformanceOptimizer
{
    public enum MPOApplyStatus { Applied, Skipped, Failed }

    public sealed class MPOSettingChoice
    {
        public string Key { get; internal set; }
        public object Current { get; internal set; }
        public object Recommended { get; internal set; }
        public object Selected { get; set; }
        public bool Enabled { get; set; }
        public object[] Choices { get; internal set; }
        public string DisplayName
        {
            get
            {
                if (Key.EndsWith(".antiAliasing")) return "Quality MSAA";
                if (Key.EndsWith(".shadowDistance")) return "Quality Shadow Distance";
                if (Key.EndsWith(".lodBias")) return "Quality LOD Bias";
                switch (Key)
                {
                    case "m_RenderScale": return "Render Scale";
                    case "m_MSAA": return "MSAA";
                    case "m_ShadowDistance": return "Shadow Distance";
                    case "m_ShadowCascadeCount": return "Shadow Cascades";
                    case "m_SupportsHDR": return "HDR";
                    case "m_AdditionalLightShadowsSupported": return "Additional Light Shadows";
                    case "m_SoftShadowsSupported": return "Soft Shadows";
                    case "m_RequireDepthTexture": return "Depth Texture";
                    case "m_RequireOpaqueTexture": return "Opaque Texture";
                    default: return Key.Replace("iPhone ", "iOS ");
                }
            }
        }
        internal Func<Object, object> Read;
        internal Action<Object, object> Write;
        internal Func<object, bool> Validate;
        internal bool RecommendedEnabled;
    }

    // A reviewable edit set owned by the existing fix workflow. Never stored on scan results.
    public sealed class MPOFixPlan
    {
        public MPOIssue Issue { get; }
        public List<MPOSettingChoice> Settings { get; } = new List<MPOSettingChoice>();
        public string Error { get; internal set; }
        internal Object Target;
        internal bool Importer;
        internal string TargetId;
        public bool Custom { get; private set; }
        private bool _linkedEmissionOff;
        internal MPOFixPlan(MPOIssue issue) { Issue = issue; }

        internal MPOFixPlan CopyForReview()
        {
            var copy = new MPOFixPlan(Issue) { Target = Target, TargetId = TargetId, Importer = Importer, Error = Error, Custom = Custom, _linkedEmissionOff = _linkedEmissionOff };
            foreach (var s in Settings)
                copy.Settings.Add(new MPOSettingChoice { Key = s.Key, Current = s.Current, Recommended = s.Recommended,
                    Selected = s.Selected, Enabled = s.Enabled, RecommendedEnabled = s.RecommendedEnabled,
                    Choices = s.Choices, Read = s.Read, Write = s.Write, Validate = s.Validate });
            return copy;
        }

        public void SetCustom(bool custom)
        {
            Custom = custom;
            _linkedEmissionOff = false;
            foreach (var setting in Settings)
            {
                setting.Selected = setting.RecommendedEnabled ? setting.Recommended : setting.Current;
                setting.Enabled = setting.RecommendedEnabled;
            }
        }

        public string Preview
        {
            get { UpdateLinkedSettings(); return Error ?? string.Join("\n", Settings.Where(s => s.Enabled).Select(s =>
                s.DisplayName + ": Current " + s.Current + " | Recommended " + s.Recommended + " | Selected " + s.Selected)); }
        }

        internal void UpdateLinkedSettings()
        {
            var emission = Settings.FirstOrDefault(s => s.Key == "Emission");
            if (emission == null) return;
            var color = Settings.First(s => s.Key == "Emission Color");
            var flags = Settings.First(s => s.Key == "Emission GI Flags");
            if (!emission.Enabled || !Equals(emission.Selected, false))
            {
                if (_linkedEmissionOff)
                {
                    color.Enabled = flags.Enabled = false;
                    color.Selected = color.Current;
                    flags.Selected = flags.Current;
                    _linkedEmissionOff = false;
                }
                return;
            }
            _linkedEmissionOff = true;
            color.Enabled = flags.Enabled = true;
            color.Selected = Color.black;
            flags.Selected = (MaterialGlobalIlluminationFlags)flags.Current | MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        internal void Add(string key, object recommended, bool enabled, Func<Object, object> read,
            Action<Object, object> write, object[] choices = null, Func<object, bool> validate = null)
        {
            object current = read(Target);
            Settings.Add(new MPOSettingChoice { Key = key, Current = current, Recommended = recommended ?? current,
                Selected = enabled ? recommended : current, Enabled = enabled, RecommendedEnabled = enabled,
                Read = read, Write = write, Choices = choices, Validate = validate });
        }
    }

    /// <summary>
    /// A small category-level preset used by the production UI. It lets the user choose
    /// one custom value once and reuses it for every compatible fix in that category.
    /// Individual plans still validate the value against the real Unity object before Apply.
    /// </summary>
    public sealed class MPOBulkSettingChoice
    {
        public string Key { get; internal set; }
        public string DisplayName { get; internal set; }
        public object Recommended { get; internal set; }
        public object Selected { get; set; }
        public bool Enabled { get; set; }
        public object[] Choices { get; internal set; }
        internal bool DefaultEnabled;
        internal System.Type ValueType;
    }

    public sealed class MPOBulkFixPreset
    {
        public MPOCategory Category { get; private set; }
        public List<MPOBulkSettingChoice> Settings { get; } = new List<MPOBulkSettingChoice>();
        public bool Custom { get; private set; }

        public bool HasEditableSettings => Settings.Count > 0;

        public void SetCustom(bool custom)
        {
            Custom = custom;
            foreach (MPOBulkSettingChoice setting in Settings)
            {
                setting.Enabled = custom && setting.DefaultEnabled;
                setting.Selected = setting.Recommended;
            }
        }

        public void ResetToRecommended()
        {
            SetCustom(false);
        }

        internal void ApplyTo(MPOFixPlan plan)
        {
            if (plan == null || plan.Error != null || !Custom)
                return;

            plan.SetCustom(true);
            foreach (MPOBulkSettingChoice bulk in Settings.Where(item => item.Enabled))
            {
                MPOSettingChoice target = plan.Settings.FirstOrDefault(setting => setting.Key == bulk.Key);
                if (target == null || bulk.Selected == null || target.Current == null)
                    continue;

                object value = bulk.Selected;
                if (value.GetType() != target.Current.GetType())
                    continue;
                if (target.Choices != null && !target.Choices.Contains(value))
                    continue;
                if (target.Validate != null && !target.Validate(value))
                    continue;

                target.Enabled = true;
                target.Selected = value;
            }
        }

        public static MPOBulkFixPreset Create(MPOCategory category, IEnumerable<MPOIssue> source)
        {
            var preset = new MPOBulkFixPreset { Category = category };
            if (source == null)
                return preset;

            List<MPOIssue> issues = source
                .Where(issue => issue != null && issue.Category == category && issue.CanFix && issue.FixSafety != MPOFixSafety.Manual)
                .ToList();

            // Build only one representative plan per distinct action shape. This keeps the category
            // toolbar instant even when the project contains thousands of textures or models.
            IEnumerable<MPOIssue> representatives = issues
                .GroupBy(issue => issue.FixKind + "|" + issue.FixStringValue + "|" +
                                  string.Join(",", issue.SettingRecommendations.Keys.OrderBy(key => key)))
                .Select(group => group.First());

            foreach (MPOIssue issue in representatives)
            {
                if (issue.FixKind == MPOFixKind.DisableDevelopmentBuildFlags)
                    continue;

                MPOFixPlan plan = MPOFixPlans.Create(issue);
                if (plan == null || plan.Error != null)
                    continue;

                foreach (MPOSettingChoice setting in plan.Settings)
                {
                    if (!IsBulkFriendly(setting))
                        continue;

                    MPOBulkSettingChoice existing = preset.Settings.FirstOrDefault(item => item.Key == setting.Key);
                    if (existing == null)
                    {
                        preset.Settings.Add(new MPOBulkSettingChoice
                        {
                            Key = setting.Key,
                            DisplayName = setting.DisplayName,
                            Recommended = setting.Recommended,
                            Selected = setting.Recommended,
                            Enabled = false,
                            DefaultEnabled = setting.RecommendedEnabled,
                            Choices = setting.Choices,
                            ValueType = setting.Current != null ? setting.Current.GetType() : null
                        });
                    }
                    else
                    {
                        existing.DefaultEnabled |= setting.RecommendedEnabled;
                        if (setting.RecommendedEnabled)
                        {
                            existing.Recommended = setting.Recommended;
                            if (!preset.Custom)
                                existing.Selected = setting.Recommended;
                        }

                        if (existing.Choices != null && setting.Choices != null)
                            existing.Choices = existing.Choices.Where(value => setting.Choices.Contains(value)).ToArray();
                    }
                }
            }

            preset.Settings.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            return preset;
        }

        private static bool IsBulkFriendly(MPOSettingChoice setting)
        {
            if (setting == null || setting.Current == null)
                return false;

            string key = setting.Key ?? string.Empty;
            // Keep category-level editing conservative. Platform format/crunch and linked material
            // emission values can require per-asset compatibility checks, so they stay in the single-item preview.
            if (key.EndsWith(" Format", StringComparison.Ordinal) ||
                key.EndsWith(" Crunch", StringComparison.Ordinal) ||
                key.EndsWith(" Override", StringComparison.Ordinal) ||
                key.IndexOf("Sample Override", StringComparison.Ordinal) >= 0 ||
                key.StartsWith("Emission", StringComparison.Ordinal))
                return false;

            System.Type type = setting.Current.GetType();
            return type == typeof(bool) || type == typeof(int) || type == typeof(float) ||
                   (setting.Choices != null && setting.Choices.Length > 0);
        }
    }

    public static class MPOFixPlans
    {
        private static readonly object[] Sizes = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
        private static object[] Values<T>() => Enum.GetValues(typeof(T)).Cast<object>().Distinct().ToArray();

        public static MPOFixPlan Create(MPOIssue issue)
        {
            var plan = new MPOFixPlan(issue);
            try
            {
                if (issue == null) throw new InvalidOperationException("No issue selected.");
                plan.Target = string.IsNullOrEmpty(issue.AssetPath) ? null : AssetImporter.GetAtPath(issue.AssetPath);
                plan.Importer = plan.Target != null;

                if (issue.Category == MPOCategory.Materials)
                {
                    // Material fixes must resolve the actual .mat asset, not the first object returned by LoadAllAssetsAtPath.
                    plan.Target = issue.ContextObject as Material;
                    if (plan.Target == null && !string.IsNullOrEmpty(issue.AssetPath))
                        plan.Target = AssetDatabase.LoadAssetAtPath<Material>(issue.AssetPath);
                    if (plan.Target == null && !string.IsNullOrEmpty(issue.AssetPath))
                        plan.Target = AssetDatabase.LoadAllAssetsAtPath(issue.AssetPath).OfType<Material>().FirstOrDefault();
                    plan.Importer = false;
                }
                else if (issue.Category == MPOCategory.URP || issue.Category == MPOCategory.Quality || issue.Category == MPOCategory.Physics)
                {
                    plan.Target = AssetDatabase.LoadAllAssetsAtPath(issue.AssetPath).FirstOrDefault();
                    plan.Importer = false;
                }

                if (issue.ContextObject is ParticleSystem) { plan.Target = issue.ContextObject; plan.Importer = false; }
                if (plan.Target == null) throw new InvalidOperationException("Editable asset/importer is unavailable.");
                plan.TargetId = GlobalObjectId.GetGlobalObjectIdSlow(plan.Target).ToString();
                if (plan.Target is TextureImporter) Texture(plan);
                else if (plan.Target is AudioImporter) Audio(plan);
                else if (plan.Target is ModelImporter) Model(plan);
                else if (plan.Target is Material) Material(plan);
                else if (plan.Target is ParticleSystem) Particle(plan);
                else if (issue.Category == MPOCategory.URP || issue.Category == MPOCategory.Quality || issue.Category == MPOCategory.Physics) Pipeline(plan);
                if (plan.Settings.Count == 0) throw new InvalidOperationException("No compatible optimization settings are available.");
            }
            catch (Exception e) { plan.Error = e.Message; }
            return plan;
        }

        private static void Texture(MPOFixPlan p)
        {
            var importer = (TextureImporter)p.Target;

            if (p.Issue.FixKind == MPOFixKind.DisableTextureReadWrite)
            {
                p.Add("Read/Write", false, true,
                    o => ((TextureImporter)o).isReadable,
                    (o, v) => ((TextureImporter)o).isReadable = (bool)v);
            }
            else if (p.Issue.FixKind == MPOFixKind.DisableTextureMipmaps)
            {
                p.Add("Mip Maps", false, true,
                    o => ((TextureImporter)o).mipmapEnabled,
                    (o, v) => ((TextureImporter)o).mipmapEnabled = (bool)v);
            }
            else if (p.Issue.FixKind == MPOFixKind.SetTexturePlatformMaxSize)
            {
                string platform = p.Issue.FixStringValue;
                if (platform != "Android" && platform != "iPhone")
                    throw new InvalidOperationException("Texture fix target platform is missing.");

                Action<string, object, bool, Func<TextureImporterPlatformSettings, object>, Action<TextureImporterPlatformSettings, object>, object[], Func<object, bool>> add =
                    (key, recommended, enabled, read, write, choices, validate) => p.Add(platform + " " + key, recommended, enabled,
                        o => read(((TextureImporter)o).GetPlatformTextureSettings(platform)),
                        (o, v) =>
                        {
                            var textureImporter = (TextureImporter)o;
                            var settings = textureImporter.GetPlatformTextureSettings(platform);
                            settings.name = platform;
                            write(settings, v);
                            textureImporter.SetPlatformTextureSettings(settings);
                        }, choices, validate);

                add("Override", true, true, s => s.overridden, (s, v) => s.overridden = (bool)v, null, null);
                add("Max Size", p.Issue.FixIntValue, true, s => s.maxTextureSize,
                    (s, v) => s.maxTextureSize = (int)v, Sizes, null);

                // Optional user-controlled importer values. They are disabled by default and appear only
                // when the user chooses Custom mode; the recommended fix remains unchanged.
                BuildTarget target = platform == "Android" ? BuildTarget.Android : BuildTarget.iOS;
                object[] formats = Values<TextureImporterFormat>()
                    .Where(v => TextureImporter.IsPlatformTextureFormatValid(importer.textureType, target, (TextureImporterFormat)v))
                    .ToArray();
                add("Format", null, false, s => s.format, (s, v) => s.format = (TextureImporterFormat)v, formats, null);
                add("Compression", null, false, s => s.textureCompression, (s, v) => s.textureCompression = (TextureImporterCompression)v, Values<TextureImporterCompression>(), null);
                add("Compression Quality", null, false, s => s.compressionQuality, (s, v) => s.compressionQuality = (int)v, null, v => (int)v >= 0 && (int)v <= 100);
                add("Crunch", null, false, s => s.crunchedCompression, (s, v) => s.crunchedCompression = (bool)v, null, null);
            }
            else
            {
                throw new InvalidOperationException("This texture finding is informational in the focused release.");
            }
        }

        private static void Audio(MPOFixPlan p)
        {
            string platform = p.Issue.FixStringValue;
            bool mobile = platform == "Android" || platform == "iPhone";

            Func<Object, AudioImporterSampleSettings> read = o =>
                mobile && ((AudioImporter)o).ContainsSampleSettingsOverride(platform)
                    ? ((AudioImporter)o).GetOverrideSampleSettings(platform)
                    : ((AudioImporter)o).defaultSampleSettings;

            Action<Object, AudioImporterSampleSettings> write = (o, settings) =>
            {
                var importer = (AudioImporter)o;
                if (mobile)
                {
                    if (!importer.SetOverrideSampleSettings(platform, settings))
                        throw new InvalidOperationException("Unity rejected the audio override.");
                }
                else
                {
                    importer.defaultSampleSettings = settings;
                }
            };

            if (mobile)
            {
                p.Add(platform + " Sample Override", true, true,
                    o => ((AudioImporter)o).ContainsSampleSettingsOverride(platform),
                    (o, v) =>
                    {
                        var importer = (AudioImporter)o;
                        if ((bool)v)
                        {
                            if (!importer.ContainsSampleSettingsOverride(platform))
                                importer.SetOverrideSampleSettings(platform, importer.defaultSampleSettings);
                        }
                        else
                        {
                            importer.ClearSampleSettingOverride(platform);
                        }
                    });
            }

            p.Add((mobile ? platform : "Default") + " Load Type", AudioClipLoadType.Streaming, true,
                o => read(o).loadType,
                (o, v) =>
                {
                    var settings = read(o);
                    settings.loadType = (AudioClipLoadType)v;
                    write(o, settings);
                },
                Values<AudioClipLoadType>());

            p.Add("Preload Audio Data", false, true,
                o => read(o).preloadAudioData,
                (o, v) =>
                {
                    var settings = read(o);
                    settings.preloadAudioData = (bool)v;
                    write(o, settings);
                });

            if (read(p.Target).compressionFormat == AudioCompressionFormat.Vorbis)
                p.Add("Audio Quality", null, false, o => read(o).quality,
                    (o, v) => { var s = read(o); s.quality = (float)v; write(o, s); },
                    validate: v => (float)v >= 0 && (float)v <= 1);

            p.Add("Force Mono", null, false,
                o => ((AudioImporter)o).forceToMono,
                (o, v) => ((AudioImporter)o).forceToMono = (bool)v);

            p.Add("Load In Background", null, false,
                o => ((AudioImporter)o).loadInBackground,
                (o, v) => ((AudioImporter)o).loadInBackground = (bool)v);
        }

        private static void Model(MPOFixPlan p)
        {
            p.Add("Read/Write", false, true,
                o => ((ModelImporter)o).isReadable,
                (o, v) => ((ModelImporter)o).isReadable = (bool)v);

            p.Add("Mesh Compression", null, false,
                o => ((ModelImporter)o).meshCompression,
                (o, v) => ((ModelImporter)o).meshCompression = (ModelImporterMeshCompression)v,
                Values<ModelImporterMeshCompression>());
        }

        private static void Material(MPOFixPlan p)
        {
            var material = (Material)p.Target;
            if (material.shader == null || material.isVariant || !AssetDatabase.IsMainAsset(material))
                throw new InvalidOperationException("Missing shaders, variants and embedded materials require manual review.");

            if (p.Issue.FixKind != MPOFixKind.EnableMaterialGpuInstancing)
                throw new InvalidOperationException("This material finding is informational in the focused release.");

            if (!MPOMaterialOptimizationUtility.SupportsGpuInstancing(material))
                throw new InvalidOperationException("This shader does not expose a supported GPU Instancing workflow.");

            p.Add("GPU Instancing", true, true,
                o => ((Material)o).enableInstancing,
                (o, v) => ((Material)o).enableInstancing = (bool)v);

            // Emission/keyword editing is intentionally disabled for the focused Asset Store release.
            if (MPOConstants.EnableAdvancedCustomSettings)
            {
                bool knownEmission = material.shader.name == "Standard" ||
                    material.shader.name == "Standard (Specular setup)" ||
                    material.shader.name.StartsWith("Universal Render Pipeline/Lit", StringComparison.Ordinal) ||
                    material.shader.name.StartsWith("Universal Render Pipeline/Simple Lit", StringComparison.Ordinal);

                if (knownEmission && material.HasProperty("_EmissionColor") &&
                    material.shader.keywordSpace.FindKeyword("_EMISSION").isValid)
                {
                    p.Add("Emission Color", null, false,
                        o => ((Material)o).GetColor("_EmissionColor"),
                        (o, v) => ((Material)o).SetColor("_EmissionColor", (Color)v));

                    p.Add("Emission GI Flags", null, false,
                        o => ((Material)o).globalIlluminationFlags,
                        (o, v) => ((Material)o).globalIlluminationFlags = (MaterialGlobalIlluminationFlags)v);

                    p.Add("Emission", null, false,
                        o => ((Material)o).IsKeywordEnabled("_EMISSION"),
                        (o, v) =>
                        {
                            var m = (Material)o;
                            if ((bool)v) m.EnableKeyword("_EMISSION");
                            else m.DisableKeyword("_EMISSION");
                        });
                }
            }
        }

        private static void Particle(MPOFixPlan p)
        {
            if (string.IsNullOrEmpty(((ParticleSystem)p.Target).gameObject.scene.path)) throw new InvalidOperationException("Save the scene before configuring particle optimization so its restore target is persistent.");
            if (PrefabUtility.IsPartOfImmutablePrefab(p.Target)) throw new InvalidOperationException("Immutable prefab requires manual review.");
            foreach (var pair in p.Issue.SettingRecommendations)
            {
                if (pair.Key == "Max Particles") p.Add(pair.Key, pair.Value, true,
                    o => ((ParticleSystem)o).main.maxParticles, (o,v) => { var m = ((ParticleSystem)o).main; m.maxParticles = (int)v; },
                    validate: v => (int)v >= 1 && (int)v <= 1000000);
            }
        }

        private static void Pipeline(MPOFixPlan p)
        {
            // Recommendations are attached by the analyzer using the same thresholds it detects.
            foreach (var pair in p.Issue.SettingRecommendations)
            {
                string key = pair.Key;
                if (key == "Fixed Timestep")
                {
                    p.Add(key, pair.Value, true, o => Time.fixedDeltaTime, (o,v) => Time.fixedDeltaTime = (float)v,
                        validate: v => (float)v >= 0.001f && (float)v <= 1f);
                    continue;
                }
                using (var so = new SerializedObject(p.Target))
                {
                    var prop = so.FindProperty(key);
                    if (prop == null) continue;
                    var type = prop.propertyType;
                    object[] choices = key.EndsWith(".antiAliasing") ? new object[] { 0, 2, 4, 8 } : key == "m_MSAA" ? new object[] { 1, 2, 4, 8 } :
                        key == "m_ShadowCascadeCount" ? new object[] { 1, 2, 3, 4 } : null;
                    p.Add(key, pair.Value, true, o => {
                        using (var s = new SerializedObject(o)) {
                            var v = s.FindProperty(key);
                            return type == SerializedPropertyType.Boolean ? (object)v.boolValue : (type == SerializedPropertyType.Integer || type == SerializedPropertyType.Enum) ? v.intValue : (object)v.floatValue;
                        }
                    }, (o,v) => {
                        using (var s = new SerializedObject(o)) {
                            var propToWrite = s.FindProperty(key);
                            if (type == SerializedPropertyType.Boolean) propToWrite.boolValue = (bool)v;
                            else if (type == SerializedPropertyType.Integer || type == SerializedPropertyType.Enum) propToWrite.intValue = (int)v;
                            else propToWrite.floatValue = (float)v;
                            s.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }, choices, v => !(v is float f) || (!float.IsNaN(f) && !float.IsInfinity(f) && f >= (key == "Fixed Timestep" ? 0.001f : key.EndsWith(".lodBias") ? 0.01f : key == "m_RenderScale" ? 0.1f : 0f) && f <= (key == "Fixed Timestep" ? 1f : key == "m_RenderScale" ? 2f : 10000f)));
                }
            }
        }

        public static MPOApplyStatus Apply(MPOFixPlan plan, out string message)
        {
            message = plan?.Error;
            if (plan == null || message != null) { message = message ?? "No plan."; return MPOApplyStatus.Skipped; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || MPOScanRunner.IsRunning)
            { message = "Wait for Play Mode or the current scan to finish."; return MPOApplyStatus.Skipped; }
            plan.UpdateLinkedSettings();
            var fresh = Create(plan.Issue);
            if (fresh.Error != null || fresh.TargetId != plan.TargetId)
            { message = fresh.Error ?? "The target asset changed. Reopen preview."; return MPOApplyStatus.Skipped; }
            string path = plan.Issue.AssetPath;
            bool sceneObject = fresh.Target is Component;
            bool projectSettings = path == "ProjectSettings/QualitySettings.asset" || path == "ProjectSettings/TimeManager.asset";
            if ((!sceneObject && !projectSettings && !path.StartsWith("Assets/", StringComparison.Ordinal)) ||
                (!sceneObject && !AssetDatabase.IsOpenForEdit(path)) ||
                (plan.Importer && !AssetDatabase.IsOpenForEdit(path + ".meta")))
            { message = "Asset or importer is read-only; check it out before applying."; return MPOApplyStatus.Skipped; }
            var selected = new List<MPOSettingChoice>();
            foreach (var requested in plan.Settings.Where(s => s.Enabled))
            {
                var setting = fresh.Settings.FirstOrDefault(s => s.Key == requested.Key);
                object value = requested.Selected;
                if (setting == null || value == null || value.GetType() != setting.Current.GetType() ||
                    (setting.Choices != null && !setting.Choices.Contains(value)) || (setting.Validate != null && !setting.Validate(value)))
                { message = "Incompatible value for " + requested.Key + ". Reopen preview."; return MPOApplyStatus.Skipped; }
                if (Same(setting.Current, value)) continue;
                if (!Same(setting.Current, requested.Current))
                { message = requested.Key + " changed since preview. Reopen preview."; return MPOApplyStatus.Skipped; }
                setting.Selected = value;
                selected.Add(setting);
            }
            if (selected.Count == 0) { message = "Requested values are already applied, or no changes selected."; return MPOApplyStatus.Skipped; }
            if (fresh.Target is TextureImporter texture)
            {
                foreach (string platform in new[] { "Android", "iPhone" })
                {
                    var s = texture.GetPlatformTextureSettings(platform);
                    Func<string, object, object> chosen = (key, current) => plan.Settings.FirstOrDefault(c => c.Enabled && c.Key == platform + " " + key)?.Selected ?? current;
                    bool overridden = (bool)chosen("Override", s.overridden);
                    if (!overridden && selected.Any(c => c.Key.StartsWith(platform + " ") && c.Key != platform + " Override"))
                    { message = "Enable " + platform + " Override to change its settings."; return MPOApplyStatus.Skipped; }
                    var format = (TextureImporterFormat)chosen("Format", s.format);
                    bool crunch = (bool)chosen("Crunch", s.crunchedCompression);
                    bool touchesCompression = selected.Any(c => c.Key == platform + " Format" || c.Key == platform + " Crunch" || c.Key == platform + " Compression");
                    if (touchesCompression && crunch && format != TextureImporterFormat.ETC_RGB4Crunched && format != TextureImporterFormat.ETC2_RGBA8Crunched)
                    { message = platform + " Crunch requires a compatible explicit ETC crunched format. Disable Crunch or choose a supported format."; return MPOApplyStatus.Skipped; }
                }
            }
            string original;
            try
            {
                original = EditorJsonUtility.ToJson(fresh.Target);
            }
            catch (Exception e) { message = "Could not capture original state: " + e.Message; return MPOApplyStatus.Failed; }
            var snapshot = new MPOFixSnapshotData { key = "settings|" + Guid.NewGuid(), kind = (int)MPOFixKind.ReviewSettings,
                assetPath = path, bool0 = fresh.Importer, string0 = plan.Issue.FixStringValue,
                int0 = (int)plan.Issue.Category, int1 = (int)plan.Issue.FixKind, string1 = fresh.TargetId,
                settings = selected.Select(s => new MPOSettingSnapshot { key = s.Key,
                    before = SerializeValue(s.Current),
                    after = SerializeValue(s.Selected), type = s.Current.GetType().AssemblyQualifiedName }).ToList() };
            try
            {
                MPOFixSession.Add(snapshot);
                MPOFixUndo.Track(fresh.Target);
                Undo.RegisterCompleteObjectUndo(fresh.Target, "Mobile Performance Optimization");
                foreach (var s in selected) s.Write(fresh.Target, s.Selected);
                Persist(fresh.Target);
                var verified = Create(plan.Issue);
                if (verified.Error != null) throw new InvalidOperationException(verified.Error);
                foreach (var requested in plan.Settings.Where(s => s.Enabled))
                {
                    var setting = verified.Settings.FirstOrDefault(s => s.Key == requested.Key);
                    if (setting == null || !Same(setting.Read(verified.Target), requested.Selected))
                        throw new InvalidOperationException("Unity did not retain " + requested.Key + ".");
                }
                message = "Applied and verified " + selected.Count + " setting(s): " + path;
                return MPOApplyStatus.Applied;
            }
            catch (Exception e)
            {
                try { if (!RestoreObject(fresh.Target, original)) throw new InvalidOperationException("Target unavailable."); message = e.Message + " Original state restored."; }
                catch (Exception restore) { message = e.Message + " Restore failed: " + restore.Message + ". Use Revert Last Fix Session."; }
                return MPOApplyStatus.Failed;
            }
        }

        private static string SerializeValue(object value) => value is Color color ? JsonUtility.ToJson(color) : Convert.ToString(value, CultureInfo.InvariantCulture);

        private static bool Same(object a, object b)
        {
            // Unity 6 stores timestep as rational time and rounds floats during serialization.
            if (a is float x && b is float y) return Mathf.Abs(x - y) <= 0.000001f * Mathf.Max(1f, Mathf.Abs(y));
            return Equals(a, b);
        }

        internal static void Persist(Object target)
        {
            EditorUtility.SetDirty(target);
            if (target is AssetImporter importer)
            {
                importer.SaveAndReimport();
            }
            else if (target is Component component)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
            else
            {
                // Material/URP/Quality assets must be serialized to disk before verification.
                AssetDatabase.SaveAssetIfDirty(target);
                AssetDatabase.SaveAssets();

                if (target is Material)
                {
                    string path = AssetDatabase.GetAssetPath(target);
                    if (!string.IsNullOrWhiteSpace(path))
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        internal static bool RestoreSettings(MPOFixSnapshotData snapshot)
        {
            if (snapshot.settings == null) return false;
            var issue = new MPOIssue((MPOCategory)snapshot.int0, MPOSeverity.Suggestion, "Restore", "", "", 0,
                contextObject: GlobalObjectId.TryParse(snapshot.string1, out var id) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) : null,
                assetPath: snapshot.assetPath, fixKind: (MPOFixKind)snapshot.int1, fixStringValue: snapshot.string0);
            Func<MPOSettingSnapshot, string, object> parse = (s,v) => {
                Type type = Type.GetType(s.type, true);
                if (type == typeof(Color)) return JsonUtility.FromJson<Color>(v);
                return type.IsEnum ? Enum.Parse(type, v) : Convert.ChangeType(v, type, CultureInfo.InvariantCulture);
            };
            foreach (var setting in snapshot.settings) issue.SettingRecommendations[setting.key] = parse(setting, setting.before);
            var plan = Create(issue);
            if (plan.Error != null) throw new InvalidOperationException(plan.Error);
            if (plan.TargetId != snapshot.string1) throw new InvalidOperationException("The original target was replaced; restore was skipped.");
            var restore = new List<MPOSettingChoice>();
            foreach (var saved in snapshot.settings)
            {
                var setting = plan.Settings.FirstOrDefault(s => s.Key == saved.key);
                if (setting == null) throw new InvalidOperationException("Restore setting no longer supported: " + saved.key);
                var before = parse(saved, saved.before);
                if (Same(setting.Current, before)) continue;
                if (!Same(setting.Current, parse(saved, saved.after)))
                    throw new InvalidOperationException("Restore conflict: " + saved.key + " was edited after Apply. Restore its applied value before retrying.");
                setting.Selected = before;
                restore.Add(setting);
            }
            MPOFixUndo.Track(plan.Target);
            Undo.RegisterCompleteObjectUndo(plan.Target, "Revert Mobile Performance Optimization");
            foreach (var setting in Enumerable.Reverse(restore)) setting.Write(plan.Target, setting.Selected);
            Persist(plan.Target);
            var verified = Create(issue);
            if (verified.Error != null) return false;
            return restore.All(s => Same(s.Read(verified.Target), s.Selected));
        }

        private static bool RestoreObject(Object target, string json)
        {
            if (target == null) return false;
            EditorJsonUtility.FromJsonOverwrite(json, target);
            Persist(target);
            return true;
        }

    }
}
