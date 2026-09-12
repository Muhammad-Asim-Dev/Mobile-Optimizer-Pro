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
                if (issue.Category == MPOCategory.Materials || issue.Category == MPOCategory.URP || issue.Category == MPOCategory.Quality || issue.Category == MPOCategory.Physics)
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
            var t = (TextureImporter)p.Target;
            p.Add("Read/Write", false, p.Issue.FixKind == MPOFixKind.DisableTextureReadWrite,
                o => ((TextureImporter)o).isReadable, (o,v) => ((TextureImporter)o).isReadable = (bool)v);
            p.Add("Mip Maps", false, p.Issue.FixKind == MPOFixKind.DisableTextureMipmaps,
                o => ((TextureImporter)o).mipmapEnabled, (o,v) => ((TextureImporter)o).mipmapEnabled = (bool)v);
            p.Add("Default Max Size", null, false, o => ((TextureImporter)o).maxTextureSize,
                (o,v) => ((TextureImporter)o).maxTextureSize = (int)v, Sizes);
            p.Add("Default Compression", null, false, o => ((TextureImporter)o).textureCompression,
                (o,v) => ((TextureImporter)o).textureCompression = (TextureImporterCompression)v, Values<TextureImporterCompression>());
            p.Add("Default Compression Quality", null, false, o => ((TextureImporter)o).compressionQuality,
                (o,v) => ((TextureImporter)o).compressionQuality = (int)v, validate: v => (int)v >= 0 && (int)v <= 100);
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                bool recommended = p.Issue.FixKind == MPOFixKind.SetTexturePlatformMaxSize && p.Issue.FixStringValue == platform;
                Action<string, object, bool, Func<TextureImporterPlatformSettings, object>, Action<TextureImporterPlatformSettings, object>, object[], Func<object,bool>> add =
                    (key, rec, enabled, read, write, choices, validate) => p.Add(platform + " " + key, rec, enabled,
                        o => read(((TextureImporter)o).GetPlatformTextureSettings(platform)), (o,v) => {
                            var importer = (TextureImporter)o;
                            var settings = importer.GetPlatformTextureSettings(platform);
                            settings.name = platform;
                            write(settings, v);
                            importer.SetPlatformTextureSettings(settings);
                        }, choices, validate);
                add("Override", true, recommended, s => s.overridden, (s,v) => s.overridden = (bool)v, null, null);
                add("Max Size", recommended ? (object)p.Issue.FixIntValue : null, recommended, s => s.maxTextureSize,
                    (s,v) => s.maxTextureSize = (int)v, Sizes, null);
                var target = platform == "Android" ? BuildTarget.Android : BuildTarget.iOS;
                var formats = Values<TextureImporterFormat>().Where(v => TextureImporter.IsPlatformTextureFormatValid(t.textureType, target, (TextureImporterFormat)v)).ToArray();
                add("Format", null, false, s => s.format, (s,v) => s.format = (TextureImporterFormat)v, formats, null);
                add("Compression", null, false, s => s.textureCompression, (s,v) => s.textureCompression = (TextureImporterCompression)v, Values<TextureImporterCompression>(), null);
                add("Compression Quality", null, false, s => s.compressionQuality, (s,v) => s.compressionQuality = (int)v, null, v => (int)v >= 0 && (int)v <= 100);
                add("Crunch", null, false, s => s.crunchedCompression, (s,v) => s.crunchedCompression = (bool)v, null, null);
            }
        }

        private static void Audio(MPOFixPlan p)
        {
            string platform = p.Issue.FixStringValue;
            bool mobile = platform == "Android" || platform == "iPhone";
            Func<Object, AudioImporterSampleSettings> read = o => mobile && ((AudioImporter)o).ContainsSampleSettingsOverride(platform)
                ? ((AudioImporter)o).GetOverrideSampleSettings(platform) : ((AudioImporter)o).defaultSampleSettings;
            Action<Object, AudioImporterSampleSettings> write = (o,s) => {
                if (mobile) {
                    if (!((AudioImporter)o).SetOverrideSampleSettings(platform, s)) throw new InvalidOperationException("Unity rejected the audio override.");
                } else ((AudioImporter)o).defaultSampleSettings = s;
            };
            if (mobile)
                p.Add(platform + " Sample Override", true, true,
                    o => ((AudioImporter)o).ContainsSampleSettingsOverride(platform), (o,v) => {
                        var importer = (AudioImporter)o;
                        if ((bool)v) { if (!importer.ContainsSampleSettingsOverride(platform)) importer.SetOverrideSampleSettings(platform, importer.defaultSampleSettings); }
                        else importer.ClearSampleSettingOverride(platform);
                    });
            p.Add((mobile ? platform : "Default") + " Load Type", AudioClipLoadType.Streaming, true, o => read(o).loadType,
                (o,v) => { var s = read(o); s.loadType = (AudioClipLoadType)v; write(o,s); }, Values<AudioClipLoadType>());
            p.Add("Preload Audio Data", false, true, o => read(o).preloadAudioData,
                (o,v) => { var s = read(o); s.preloadAudioData = (bool)v; write(o,s); });
            if (read(p.Target).compressionFormat == AudioCompressionFormat.Vorbis) p.Add("Audio Quality", null, false, o => read(o).quality,
                (o,v) => { var s = read(o); s.quality = (float)v; write(o,s); }, validate: v => (float)v >= 0 && (float)v <= 1);
            p.Add("Force Mono", null, false, o => ((AudioImporter)o).forceToMono, (o,v) => ((AudioImporter)o).forceToMono = (bool)v);
            p.Add("Load In Background", null, false, o => ((AudioImporter)o).loadInBackground, (o,v) => ((AudioImporter)o).loadInBackground = (bool)v);
        }

        private static void Model(MPOFixPlan p)
        {
            p.Add("Read/Write", false, true, o => ((ModelImporter)o).isReadable, (o,v) => ((ModelImporter)o).isReadable = (bool)v);
            p.Add("Mesh Compression", null, false, o => ((ModelImporter)o).meshCompression,
                (o,v) => ((ModelImporter)o).meshCompression = (ModelImporterMeshCompression)v, Values<ModelImporterMeshCompression>());
        }

        private static void Material(MPOFixPlan p)
        {
            var m = (Material)p.Target;
            if (m.shader == null || m.isVariant || !AssetDatabase.IsMainAsset(m))
                throw new InvalidOperationException("Missing shaders, variants and embedded materials require manual review.");
            if (m.shader.keywordSpace.FindKeyword("INSTANCING_ON").isValid)
                p.Add("GPU Instancing", true, true, o => ((Material)o).enableInstancing, (o,v) => ((Material)o).enableInstancing = (bool)v);
            // Only expose emission for shaders declaring both the property and the local keyword.
            bool knownEmission = m.shader.name == "Standard" || m.shader.name == "Standard (Specular setup)" ||
                m.shader.name.StartsWith("Universal Render Pipeline/Lit", StringComparison.Ordinal) ||
                m.shader.name.StartsWith("Universal Render Pipeline/Simple Lit", StringComparison.Ordinal);
            if (knownEmission && m.HasProperty("_EmissionColor") && m.shader.keywordSpace.FindKeyword("_EMISSION").isValid)
            {
                p.Add("Emission Color", null, false, o => ((Material)o).GetColor("_EmissionColor"),
                    (o,v) => ((Material)o).SetColor("_EmissionColor", (Color)v), validate: v => {
                        var color = (Color)v;
                        return new[] { color.r, color.g, color.b, color.a }.All(c => !float.IsNaN(c) && !float.IsInfinity(c) && c >= 0);
                    });
                p.Add("Emission GI Flags", null, false, o => ((Material)o).globalIlluminationFlags,
                    (o,v) => ((Material)o).globalIlluminationFlags = (MaterialGlobalIlluminationFlags)v, Enumerable.Range(0, 8).Select(v => (object)(MaterialGlobalIlluminationFlags)v).ToArray());
                p.Add("Emission", null, false, o => ((Material)o).IsKeywordEnabled("_EMISSION"), (o,v) => {
                    var material = (Material)o;
                    var keywords = material.shaderKeywords.Where(k => k != "_EMISSION").ToList();
                    if ((bool)v) keywords.Add("_EMISSION");
                    // Assign the serialized keyword set so Save/Undo retain the requested state.
                    material.shaderKeywords = keywords.ToArray();
                });
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
            if (target is AssetImporter importer) importer.SaveAndReimport();
            else if (target is Component component)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
            else AssetDatabase.SaveAssets();
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
