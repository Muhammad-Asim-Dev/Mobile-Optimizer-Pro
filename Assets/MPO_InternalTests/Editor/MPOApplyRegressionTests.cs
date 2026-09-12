using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using MobilePerformanceOptimizer;

public sealed class MPOApplyRegressionTests
{
    private string root;
    private byte[] session;
    private bool hadSession;
    private UnityEngine.Object[] selection;

    [OneTimeSetUp] public void PrepareExplicitlyIsolatedProject()
    {
        const string marker = ".mpo-isolated-validation";
        if (!File.Exists(marker) || File.ReadAllText(marker).Trim() != "Mobile Performance Optimizer isolated regression project") return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/MPO_ValidationStartup.unity");
    }

    [SetUp] public void Setup()
    {
        selection = Selection.objects;
        root = "Assets/MPO_QA_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", root.Substring(7));
        hadSession = File.Exists(MPOConstants.SessionFile);
        session = hadSession ? File.ReadAllBytes(MPOConstants.SessionFile) : null;
        MPOFixSession.Clear();
    }

    [TearDown] public void Cleanup()
    {
        MPOScanRunner.CancelAndDetachCallbacks();
        MPOFixSession.Clear();
        AssetDatabase.DeleteAsset(root);
        Selection.objects = selection;
        if (hadSession) File.WriteAllBytes(MPOConstants.SessionFile, session);
        else if (File.Exists(MPOConstants.SessionFile)) File.Delete(MPOConstants.SessionFile);
        typeof(MPOFixSession).GetField("_session", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
    }

    private string Texture(string name = "texture", int size = 2048)
    {
        string path = root + "/" + name + ".png";
        var tex = new Texture2D(size, 8, TextureFormat.RGBA32, false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = true;
        importer.SaveAndReimport();
        return path;
    }

    private MPOScanResult Scan(IMPOAnalyzer analyzer, MPOTargetPlatform platform = MPOTargetPlatform.Android)
    {
        Assert.IsTrue(MPOScanScope.TryCreate(MPOScanScopeMode.SelectedFolder, root, out var scope, out var error), error);
        var result = new MPOScanResult();
        if (analyzer is ParticleAnalyzer) scope = MPOScanScope.FullProject();
        analyzer.Analyze(new MPOScanContext(MPOProfile.Create(platform, MPODeviceTier.LowEnd), result, scope, analyzer.Name, null));
        Assert.IsEmpty(result.RecoverableWarnings);
        return result;
    }

    private MPOIssue SizeIssue(string path, MPOTargetPlatform platform) => Scan(new TextureAnalyzer(), platform).AllIssues.Single(i => i.AssetPath == path && i.FixKind == MPOFixKind.SetTexturePlatformMaxSize);
    private void Apply(MPOFixPlan plan) => Assert.AreEqual(MPOApplyStatus.Applied, MPOFixPlans.Apply(plan, out var message), message);

    [TestCase(MPOTargetPlatform.Android, "Android")]
    [TestCase(MPOTargetPlatform.iOS, "iPhone")]
    public void RecommendedRescanUsesSelectedPlatform(MPOTargetPlatform platform, string platformName)
    {
        var path = Texture();
        var issue = SizeIssue(path, platform);
        var originalDefault = ((TextureImporter)AssetImporter.GetAtPath(path)).maxTextureSize;
        Assert.IsTrue(MPOFixEngine.Apply(issue, out var message), message);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.AreEqual(issue.FixIntValue, importer.GetPlatformTextureSettings(platformName).maxTextureSize);
        Assert.AreEqual(originalDefault, importer.maxTextureSize);
        var after = Scan(new TextureAnalyzer(), platform).AllIssues.Where(i => i.AssetPath == path).ToList();
        Assert.IsFalse(after.Any(i => i.RuleId == "textures.oversized-mobile"));
        Assert.IsTrue(after.Any(i => i.RuleId == "textures.read-write"));
        Assert.IsTrue(MPOFixSession.RevertLastSession(out message), message);
        Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(path)).GetPlatformTextureSettings(platformName).overridden);
        Assert.IsNotNull(SizeIssue(path, platform));
    }

    [Test] public void CustomSizeDifferentFromRecommendationPersistsAndRemainsHonest()
    {
        var path = Texture(size: 4096);
        var issue = SizeIssue(path, MPOTargetPlatform.Android);
        var plan = MPOFixPlans.Create(issue);
        plan.SetCustom(true);
        int custom = issue.FixIntValue == 1024 ? 2048 : 1024;
        plan.Settings.Single(s => s.Key == "Android Max Size").Selected = custom;
        Apply(plan);
        Assert.AreEqual(custom, ((TextureImporter)AssetImporter.GetAtPath(path)).GetPlatformTextureSettings("Android").maxTextureSize);
        Assert.AreEqual(custom > issue.FixIntValue, Scan(new TextureAnalyzer()).AllIssues.Any(i => i.RuleId == "textures.oversized-mobile"));
        Assert.AreEqual(MPOApplyStatus.Skipped, MPOFixPlans.Apply(plan, out var message), message);
    }

    [Test] public void ExistingMobileOverrideLargerThanDesktopIsDetected()
    {
        var path = Texture();
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.maxTextureSize = 32;
        var mobile = importer.GetPlatformTextureSettings("Android");
        mobile.overridden = true; mobile.maxTextureSize = 2048;
        importer.SetPlatformTextureSettings(mobile); importer.SaveAndReimport();
        Assert.IsNotNull(SizeIssue(path, MPOTargetPlatform.Android));
    }

    [Test] public void ReadWriteAndMipmapsAreIndependent()
    {
        var path = Texture("icon", 4096);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.mipmapEnabled = true; importer.SaveAndReimport();
        var first = Scan(new TextureAnalyzer()).AllIssues.Single(i => i.RuleId == "textures.read-write");
        Assert.IsTrue(MPOFixEngine.Apply(first, out var message), message);
        var remaining = Scan(new TextureAnalyzer()).AllIssues.ToList();
        Assert.IsFalse(remaining.Any(i => i.RuleId == "textures.read-write"));
        Assert.IsTrue(remaining.Any(i => i.RuleId == "textures.ui-mipmaps"));
        Assert.IsTrue(remaining.Any(i => i.RuleId == "textures.oversized-mobile"));
    }

    [Test] public void RepeatedFreshScansDoNotDuplicate()
    {
        Texture();
        var a = Scan(new TextureAnalyzer()).AllIssues.Select(i => i.IdentityKey).ToArray();
        var b = Scan(new TextureAnalyzer()).AllIssues.Select(i => i.IdentityKey).ToArray();
        CollectionAssert.AreEquivalent(a, b);
        Assert.AreEqual(b.Length, b.Distinct().Count());
    }

    [Test] public void InvalidBulkValueSkipsBeforeAnyMutation()
    {
        var one = Texture("one"); var two = Texture("two"); var three = Texture("three");
        var good = MPOFixPlans.Create(SizeIssue(one, MPOTargetPlatform.Android));
        var secondGood = MPOFixPlans.Create(SizeIssue(two, MPOTargetPlatform.Android));
        var bad = MPOFixPlans.Create(SizeIssue(three, MPOTargetPlatform.Android));
        bad.SetCustom(true);
        bad.Settings.Single(s => s.Key == "Android Max Size").Selected = 123;
        Apply(good); Apply(secondGood);
        Assert.IsTrue(((TextureImporter)AssetImporter.GetAtPath(two)).GetPlatformTextureSettings("Android").overridden);
        Assert.AreEqual(MPOApplyStatus.Skipped, MPOFixPlans.Apply(bad, out var message), message);
        Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(three)).GetPlatformTextureSettings("Android").overridden);
    }

    [Test] public void StalePreviewDoesNotOverwriteExternalEdit()
    {
        var path = Texture();
        var plan = MPOFixPlans.Create(SizeIssue(path, MPOTargetPlatform.Android));
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var mobile = importer.GetPlatformTextureSettings("Android"); mobile.overridden = true; mobile.maxTextureSize = 32;
        importer.SetPlatformTextureSettings(mobile); importer.SaveAndReimport();
        Assert.AreEqual(MPOApplyStatus.Skipped, MPOFixPlans.Apply(plan, out var message), message);
        Assert.AreEqual(32, ((TextureImporter)AssetImporter.GetAtPath(path)).GetPlatformTextureSettings("Android").maxTextureSize);
    }

    [Test] public void RevertPreservesUnrelatedEdit()
    {
        var path = Texture(); Apply(MPOFixPlans.Create(SizeIssue(path, MPOTargetPlatform.Android)));
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode = FilterMode.Point; importer.SaveAndReimport();
        Assert.IsTrue(MPOFixSession.RevertLastSession(out var message), message);
        Assert.AreEqual(FilterMode.Point, ((TextureImporter)AssetImporter.GetAtPath(path)).filterMode);
    }

    [Test] public void FormatsAreValidatedForEachMobileTarget()
    {
        var path = Texture(); var plan = MPOFixPlans.Create(SizeIssue(path, MPOTargetPlatform.Android));
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        foreach (string platform in new[] { "Android", "iPhone" })
            foreach (var value in plan.Settings.Single(s => s.Key == platform + " Format").Choices)
                Assert.IsTrue(TextureImporter.IsPlatformTextureFormatValid(importer.textureType, platform == "Android" ? BuildTarget.Android : BuildTarget.iOS, (TextureImporterFormat)value));
    }

    [Test] public void AudioOverrideStreamingLeavesIndependentPcmFinding()
    {
        var path = root + "/music.wav";
        const int samples = 44100 * 70;
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + samples * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(44100); w.Write(88200); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(samples * 2); w.Write(new byte[samples * 2]);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var defaults = importer.defaultSampleSettings;
        defaults.loadType = AudioClipLoadType.Streaming; importer.defaultSampleSettings = defaults;
        var android = defaults; android.loadType = AudioClipLoadType.DecompressOnLoad; android.compressionFormat = AudioCompressionFormat.PCM;
        importer.SetOverrideSampleSettings("Android", android); importer.SaveAndReimport();
        var issue = Scan(new AudioAnalyzer()).AllIssues.Single(i => i.FixKind == MPOFixKind.StreamLongAudio);
        Apply(MPOFixPlans.Create(issue));
        var after = Scan(new AudioAnalyzer()).AllIssues.ToList();
        Assert.IsFalse(after.Any(i => i.RuleId == "audio.long-clip-load-type"));
        Assert.IsTrue(after.Any(i => i.RuleId == "audio.long-clip-pcm"));
        Assert.IsTrue(MPOFixSession.RevertLastSession(out var message), message);
        Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, ((AudioImporter)AssetImporter.GetAtPath(path)).GetOverrideSampleSettings("Android").loadType);
    }

    [Test] public void ModelReadWriteUsesImporterAndRestores()
    {
        var path = root + "/triangle.obj";
        File.WriteAllText(path, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path); importer.isReadable = true; importer.SaveAndReimport();
        var issue = Scan(new MeshAnalyzer()).AllIssues.First(i => i.FixKind == MPOFixKind.DisableMeshReadWrite);
        Apply(MPOFixPlans.Create(issue));
        Assert.IsFalse(Scan(new MeshAnalyzer()).AllIssues.Any(i => i.FixKind == MPOFixKind.DisableMeshReadWrite));
        Assert.IsTrue(MPOFixSession.RevertLastSession(out var message), message);
        Assert.IsTrue(((ModelImporter)AssetImporter.GetAtPath(path)).isReadable);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MaterialMultipleSelectedSettingsAndNativeUndo(bool nativeUndo)
    {
        var shader = Shader.Find("Standard"); Assert.IsNotNull(shader);
        var material = new Material(shader); material.enableInstancing = false; material.EnableKeyword("_EMISSION");
        var path = root + "/material.mat"; AssetDatabase.CreateAsset(material, path);
        var issue = new MPOIssue(MPOCategory.Materials, MPOSeverity.Warning, "Instancing", "", "", 1,
            assetPath: path, fixKind: MPOFixKind.EnableMaterialGpuInstancing);
        material.SetColor("_EmissionColor", Color.white); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive; material.shaderKeywords = new[] { "_EMISSION" }; EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
        Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), "Fixture emission must initially be enabled.");
        var plan = MPOFixPlans.Create(issue); Assert.IsNull(plan.Error);
        plan.SetCustom(true);
        var emission = plan.Settings.Single(s => s.Key == "Emission"); Assert.AreEqual(true, emission.Current, "Plan must capture enabled emission."); emission.Enabled = true; emission.Selected = false;
        Undo.IncrementCurrentGroup();
        Assert.AreEqual(true, MPOFixPlans.Create(issue).Settings.Single(s => s.Key == "Emission").Current, "Fresh plan must capture enabled emission.");
        Apply(plan); Undo.FlushUndoRecordObjects();
        Assert.IsTrue(material.enableInstancing); Assert.IsFalse(material.IsKeywordEnabled("_EMISSION"));
        if (nativeUndo) Undo.PerformUndo();
        else Assert.IsTrue(MPOFixSession.RevertLastSession(out var message), message);
        Assert.IsFalse(material.enableInstancing); Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), "Undo must restore the persisted emission keyword.");
        Assert.AreEqual(Color.white, material.GetColor("_EmissionColor"));
        Assert.AreEqual(MaterialGlobalIlluminationFlags.BakedEmissive, material.globalIlluminationFlags);
    }
    [Test] public void PhysicsRecommendedRescanAndRestore()
    {
        float original = Time.fixedDeltaTime;
        try
        {
            Time.fixedDeltaTime = 0.005f;
            var issue = Scan(new PhysicsAnalyzer()).AllIssues.Single(i => i.RuleId == "physics.fixed-timestep");
            Apply(MPOFixPlans.Create(issue));
            Assert.IsFalse(Scan(new PhysicsAnalyzer()).AllIssues.Any(i => i.RuleId == "physics.fixed-timestep"));
            Assert.IsTrue(MPOFixSession.RevertLastSession(out var message), message);
            Assert.AreEqual(0.005f, Time.fixedDeltaTime, 0.00001f);
        }
        finally { Time.fixedDeltaTime = original; }
    }

    [Test] public void QualityCustomValueAndRestore()
    {
        float original = QualitySettings.shadowDistance;
        try
        {
            QualitySettings.shadowDistance = 500;
            var issue = Scan(new QualitySettingsAnalyzer()).AllIssues.Single(i => i.RuleId == "quality.active-level");
            var plan = MPOFixPlans.Create(issue); plan.SetCustom(true);
            foreach (var setting in plan.Settings) setting.Enabled = setting.Key.EndsWith(".shadowDistance");
            plan.Settings.Single(s => s.Enabled).Selected = 12f;
            Apply(plan);
            Assert.AreEqual(12f, QualitySettings.shadowDistance);
            Assert.IsTrue(MPOFixSession.RevertLastSession(out var message), message);
            Assert.AreEqual(500f, QualitySettings.shadowDistance);
        }
        finally { QualitySettings.shadowDistance = original; }
    }

    [Test] public void ParticleCustomCapacityLeavesCollisionFindingAndRestores()
    {
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool temporaryEmptyScene = string.IsNullOrEmpty(activeScene.path);
        if (temporaryEmptyScene)
        {
            Assert.IsTrue(activeScene.rootCount == 0 && UnityEngine.SceneManagement.SceneManager.sceneCount == 1,
                "Save existing scenes before running the particle regression; it never saves user-created untitled content. Scene count: " + UnityEngine.SceneManagement.SceneManager.sceneCount + "; roots: " + string.Join(", ", activeScene.GetRootGameObjects().Select(g => g.name + " [" + g.hideFlags + "]")));
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene, root + "/initial.unity");
        }
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
        try
        {
            var go = new GameObject("QA particles"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var ps = go.AddComponent<ParticleSystem>(); var main = ps.main; main.maxParticles = 10000;
            var collision = ps.collision; collision.enabled = true;
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, root + "/particles.unity");
            var issue = Scan(new ParticleAnalyzer()).AllIssues.Single(i => i.ContextObject == ps);
            var plan = MPOFixPlans.Create(issue); plan.SetCustom(true);
            plan.Settings.Single(s => s.Key == "Max Particles").Selected = 32;
            Apply(plan); Assert.AreEqual(32, ps.main.maxParticles);
            Assert.IsTrue(Scan(new ParticleAnalyzer()).AllIssues.Any(i => i.ContextObject == ps));
            Assert.IsTrue(MPOFixSession.RevertLastSession(out var message), message);
            Assert.AreEqual(10000, ps.main.maxParticles);
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            if (temporaryEmptyScene) UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        }
    }

    [Test] public void ImporterNativeUndoReimportsOriginalValue()
    {
        var path = Texture();
        var issue = Scan(new TextureAnalyzer()).AllIssues.Single(i => i.RuleId == "textures.read-write");
        Undo.IncrementCurrentGroup(); Apply(MPOFixPlans.Create(issue)); Undo.FlushUndoRecordObjects();
        Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(path)).isReadable);
        Undo.PerformUndo();
        Assert.IsTrue(((TextureImporter)AssetImporter.GetAtPath(path)).isReadable);
        Assert.IsTrue(Scan(new TextureAnalyzer()).AllIssues.Any(i => i.RuleId == "textures.read-write"));
    }

    [UnityTest] public IEnumerator ApplyCallbackRefreshesWindowWithFreshScan()
    {
        var path = Texture();
        var before = Scan(new TextureAnalyzer());
        var issue = before.AllIssues.Single(i => i.RuleId == "textures.read-write");
        var window = ScriptableObject.CreateInstance<MobilePerformanceOptimizerWindow>();
        try
        {
            window.CreateGUI();
            var type = typeof(MobilePerformanceOptimizerWindow);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            type.GetField("_scanResult", flags).SetValue(window, before);
            MPOScanScope.TryCreate(MPOScanScopeMode.SelectedFolder, root, out var scope, out var error);
            type.GetField("_scanScope", flags).SetValue(window, scope);
            Assert.IsTrue(MPOFixEngine.Apply(issue, out var message), message);
            type.GetMethod("RefreshAfterOptimization", flags).Invoke(window, null);
            double deadline = EditorApplication.timeSinceStartup + 30;
            while (MPOScanRunner.IsRunning && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.IsFalse(MPOScanRunner.IsRunning, "Refresh scan timed out.");
            var after = (MPOScanResult)type.GetField("_scanResult", flags).GetValue(window);
            Assert.AreNotSame(before, after);
            Assert.IsFalse(after.AllIssues.Any(i => i.AssetPath == path && i.RuleId == "textures.read-write"));
        }
        finally { UnityEngine.Object.DestroyImmediate(window); MPOScanRunner.CancelAndDetachCallbacks(); }
    }

    [Test] public void CustomTextureFormatAndCombinedSettingsPersist()
    {
        var path = Texture();
        var plan = MPOFixPlans.Create(SizeIssue(path, MPOTargetPlatform.Android)); plan.SetCustom(true);
        var format = plan.Settings.Single(s => s.Key == "Android Format");
        format.Selected = TextureImporterFormat.ASTC_6x6; format.Enabled = true;
        var readable = plan.Settings.Single(s => s.Key == "Read/Write"); readable.Enabled = true; readable.Selected = false;
        Apply(plan);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.AreEqual(TextureImporterFormat.ASTC_6x6, importer.GetPlatformTextureSettings("Android").format);
        Assert.IsFalse(importer.isReadable);
        Assert.IsTrue(MPOFixSession.RevertLastSession(out var message), message);
        Assert.IsTrue(((TextureImporter)AssetImporter.GetAtPath(path)).isReadable);
    }

    [Test] public void UrpRecommendedValuesResolveAndCustomSelectionPersists()
    {
        var type = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
        Assert.IsNotNull(type, "URP must be installed for this regression suite.");
        var pipeline = (UnityEngine.Rendering.RenderPipelineAsset)ScriptableObject.CreateInstance(type);
        string path = root + "/pipeline.asset"; AssetDatabase.CreateAsset(pipeline, path);
        var rendererType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
        var renderer = ScriptableObject.CreateInstance(rendererType); AssetDatabase.CreateAsset(renderer, root + "/renderer.asset");
        using (var so = new SerializedObject(pipeline))
        {
            var renderers = so.FindProperty("m_RendererDataList"); renderers.arraySize = 1; renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.FindProperty("m_RenderScale").floatValue = 1.5f;
            so.FindProperty("m_ShadowDistance").floatValue = 300f;
            so.FindProperty("m_MSAA").intValue = 8;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var oldDefault = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        var oldQuality = QualitySettings.renderPipeline;
        try
        {
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var issue = Scan(new URPAnalyzer()).AllIssues.Single(i => i.RuleId == "urp.settings-review");
            Apply(MPOFixPlans.Create(issue));
            Assert.IsFalse(Scan(new URPAnalyzer()).AllIssues.Any(i => i.RuleId == "urp.settings-review"));
            Assert.IsTrue(MPOFixSession.RevertLastSession(out var message), message);
            issue = Scan(new URPAnalyzer()).AllIssues.Single(i => i.RuleId == "urp.settings-review");
            var plan = MPOFixPlans.Create(issue); plan.SetCustom(true);
            foreach (var setting in plan.Settings) setting.Enabled = setting.Key == "m_RenderScale";
            plan.Settings.Single(s => s.Enabled).Selected = 0.6f;
            Apply(plan);
            using (var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path))) Assert.AreEqual(0.6f, so.FindProperty("m_RenderScale").floatValue);
            Assert.IsTrue(Scan(new URPAnalyzer()).AllIssues.Any(i => i.RuleId == "urp.settings-review"), "Independent shadow/MSAA problems must remain.");
            Assert.IsTrue(MPOFixSession.RevertLastSession(out message), message);
        }
        finally { UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = oldDefault; QualitySettings.renderPipeline = oldQuality; }
    }

    [Test] public void BulkActionsKeepDistinctSceneTargets()
    {
        var first = new GameObject("First"); var second = new GameObject("Second");
        try
        {
            var a = new MPOIssue(MPOCategory.Particles, MPOSeverity.Warning, "Particles", "", "", 1, first,
                fixKind: MPOFixKind.ReviewSettings, ruleId: "particles.system-review");
            var b = new MPOIssue(MPOCategory.Particles, MPOSeverity.Warning, "Particles", "", "", 1, second,
                fixKind: MPOFixKind.ReviewSettings, ruleId: "particles.system-review");
            Assert.AreNotEqual(MPOFixEngine.GetActionKey(a), MPOFixEngine.GetActionKey(b));
        }
        finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
    }

}
