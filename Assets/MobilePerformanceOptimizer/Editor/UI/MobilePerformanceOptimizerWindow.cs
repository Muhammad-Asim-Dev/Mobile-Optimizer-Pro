using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MobilePerformanceOptimizer
{
    /// <summary>
    /// Retained-mode production Editor UI. The scanner remains cooperative/incremental while
    /// UI Toolkit ListView virtualization keeps large finding sets responsive.
    /// </summary>
    public sealed class MobilePerformanceOptimizerWindow : EditorWindow
    {
        private enum Page
        {
            Overview,
            Issues,
            Fixes,
            Reports
        }

        private enum IssueSortMode
        {
            Severity = 0,
            EstimatedImpact = 1,
            Category = 2,
            AssetName = 3
        }

        private enum FindingsViewMode
        {
            Groups = 0,
            Assets = 1
        }

        private static readonly string[] CategoryOptions = BuildCategoryOptions();

        private MPOScanResult _scanResult;
        private MPOTargetPlatform _targetPlatform;
        private MPODeviceTier _deviceTier;
        private MPOTargetPlatform _scanPlatform;
        private MPODeviceTier _scanTier;
        private MPOScanScopeMode _scanScopeMode;
        private MPOScanScope _scanScope;
        private string _selectedFolderPath = "Assets";
        private Page _page;
        private bool _needsRescan;
        private string _currentHistoryId = string.Empty;
        private string _lastExportPath = string.Empty;

        private string _search = string.Empty;
        private bool _showCritical = true;
        private bool _showWarnings = true;
        private bool _showSuggestions = true;
        private bool _showOnlyFixable;
        private bool _showOnlyHighImpact;
        private IssueSortMode _sortMode = IssueSortMode.Severity;
        private FindingsViewMode _findingsViewMode = FindingsViewMode.Groups;
        private int _categoryFilterIndex;
        private string _groupFilterKey = string.Empty;

        private readonly List<MPOIssue> _activeIssueCache = new List<MPOIssue>();
        private List<MPOIssue> _visibleIssueCache = new List<MPOIssue>();
        private List<MPOIssueGroup> _visibleGroupCache = new List<MPOIssueGroup>();
        private string _visibleGroupCacheKey = string.Empty;
        private List<MPOIssue> _fixableIssueCache = new List<MPOIssue>();
        private readonly Dictionary<MPOCategory, int> _activeCategoryCounts = new Dictionary<MPOCategory, int>();
        private bool _activeIssueCacheDirty = true;
        private string _visibleIssueCacheKey = string.Empty;
        private int _cacheVersion;
        private int _fixableCacheVersion = -1;
        private int _cachedOverallScore = 100;
        private int _cachedCriticalCount;
        private int _cachedWarningCount;
        private int _cachedSuggestionCount;
        private int _cachedFixableCount;
        private int _cachedIgnoredCount;
        private int _cachedSafeFixCount;
        private int _cachedReviewFixCount;
        private int _cachedManualCount;
        private MPOScanSnapshotData _liveSnapshotCache;
        private int _liveSnapshotCacheVersion = -1;

        private VisualElement _contentHost;
        private VisualElement _scanBanner;
        private ProgressBar _scanProgress;
        private Label _scanTitle;
        private Label _scanStatus;
        private EnumField _platformField;
        private EnumField _tierField;
        private Button _scanButton;
        private Label _topPlatformBadge;
        private Label _topTierBadge;
        private Label _scopeStatusLabel;
        private ObjectField _folderField;
        private readonly Dictionary<MPOTargetPlatform, Button> _platformTabs = new Dictionary<MPOTargetPlatform, Button>();
        private readonly Dictionary<MPODeviceTier, Button> _tierTabs = new Dictionary<MPODeviceTier, Button>();
        private readonly Dictionary<MPOScanScopeMode, Button> _scopeTabs = new Dictionary<MPOScanScopeMode, Button>();
        private readonly Dictionary<int, Button> _categoryTabs = new Dictionary<int, Button>();
        private Label _sidebarTarget;
        private Label _sidebarScore;
        private readonly Dictionary<Page, Button> _navButtons = new Dictionary<Page, Button>();

        private ToolbarSearchField _searchField;
        private DropdownField _categoryField;
        private EnumField _sortField;
        private Button _criticalFilter;
        private Button _warningFilter;
        private Button _suggestionFilter;
        private Button _fixableFilter;
        private Button _impactFilter;
        private Button _groupedViewButton;
        private Button _assetViewButton;
        private Label _issueCountLabel;
        private ListView _issueList;
        private VisualElement _issueDetailHost;
        private MPOIssue _selectedIssue;
        private MPOIssueGroup _selectedIssueGroup;

        private ListView _fixList;
        private VisualElement _fixDetailHost;
        private MPOIssue _selectedFixIssue;

        [MenuItem("Tools/Mobile Performance Optimizer/Open Optimizer", priority = 1000)]
        public static void Open()
        {
            var window = GetWindow<MobilePerformanceOptimizerWindow>();
            window.titleContent = new GUIContent("Mobile Optimizer");
            window.minSize = new Vector2(980f, 640f);
            window.Show();
        }

        private void OnEnable()
        {
            _targetPlatform = MPOEditorPreferences.TargetPlatform;
            _deviceTier = MPOEditorPreferences.DeviceTier;
            _scanPlatform = _targetPlatform;
            _scanTier = _deviceTier;
            _scanScopeMode = MPOEditorPreferences.ScanScope;
            _selectedFolderPath = MPOEditorPreferences.SelectedFolder;
            _scanScope = MPOScanScope.FullProject();
        }

        public void CreateGUI()
        {
            BuildShell();
            RenderCurrentPage();
            UpdateScanUi();
        }

        private void OnDisable()
        {
            if (MPOScanRunner.IsRunning)
                MPOScanRunner.CancelAndDetachCallbacks();
        }

        private void BuildShell()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            MPOUI.ApplyTheme(root);

            root.Add(BuildTopBar());
            root.Add(BuildScanBanner());

            var shell = new VisualElement();
            shell.AddToClassList("mpo-shell");
            shell.Add(BuildSidebar());

            _contentHost = new VisualElement();
            _contentHost.AddToClassList("mpo-content");
            shell.Add(_contentHost);
            root.Add(shell);
        }

        private VisualElement BuildTopBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("mpo-topbar");

            var brandRow = new VisualElement();
            brandRow.AddToClassList("mpo-brand-row");

            var mark = new VisualElement();
            mark.AddToClassList("mpo-brand-mark");
            mark.Add(new Label("M"));
            brandRow.Add(mark);

            var brandCopy = new VisualElement();
            var titleRow = new VisualElement();
            titleRow.AddToClassList("mpo-row");
            titleRow.Add(MPOUI.Text("Mobile Performance Optimizer Pro", "mpo-brand-title"));
            titleRow.Add(MPOUI.Text(MPOConstants.Version, "mpo-version"));
            brandCopy.Add(titleRow);
            brandCopy.Add(MPOUI.Text("Optimize. Lighter builds. Smoother games.", "mpo-brand-subtitle"));
            brandRow.Add(brandCopy);
            bar.Add(brandRow);

            var controls = new VisualElement();
            controls.AddToClassList("mpo-target-controls");

            _topPlatformBadge = MPOUI.Text(_targetPlatform.ToString(), "mpo-top-target-badge");
            _topTierBadge = MPOUI.Text(FriendlyTier(_deviceTier), "mpo-top-target-badge");
            controls.Add(_topPlatformBadge);
            controls.Add(_topTierBadge);

            var scanNav = MPOUI.ActionButton("Scan", () => SetPage(Page.Overview), "mpo-button");
            scanNav.AddToClassList("mpo-primary");
            controls.Add(scanNav);
            bar.Add(controls);

            return bar;
        }

        private VisualElement BuildScanBanner()
        {
            _scanBanner = new VisualElement();
            _scanBanner.AddToClassList("mpo-scan-banner");

            var copy = new VisualElement();
            copy.AddToClassList("mpo-scan-copy");
            _scanTitle = MPOUI.Text("Scanning project", "mpo-scan-title");
            _scanStatus = MPOUI.Text("Preparing…", "mpo-scan-status");
            copy.Add(_scanTitle);
            copy.Add(_scanStatus);
            _scanBanner.Add(copy);

            _scanProgress = new ProgressBar { lowValue = 0f, highValue = 100f, value = 0f, title = "0%" };
            _scanProgress.AddToClassList("mpo-progress");
            _scanBanner.Add(_scanProgress);

            var cancel = MPOUI.ActionButton("Cancel", () => MPOScanRunner.Cancel(), "mpo-button");
            cancel.AddToClassList("mpo-ghost");
            _scanBanner.Add(cancel);
            _scanBanner.style.display = DisplayStyle.None;
            return _scanBanner;
        }

        private VisualElement BuildSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.AddToClassList("mpo-sidebar");
            sidebar.Add(MPOUI.Text("WORKSPACE", "mpo-nav-caption"));

            AddNavButton(sidebar, Page.Overview, "Scan");
            AddNavButton(sidebar, Page.Issues, "Problems");
            AddNavButton(sidebar, Page.Fixes, "Fixes");
            AddNavButton(sidebar, Page.Reports, "Reports");

            var spacer = new VisualElement();
            spacer.AddToClassList("mpo-sidebar-spacer");
            sidebar.Add(spacer);

            var targetCard = new VisualElement();
            targetCard.AddToClassList("mpo-sidebar-card");
            targetCard.Add(MPOUI.Text("CURRENT TARGET", "mpo-sidebar-kicker"));
            _sidebarTarget = MPOUI.Text(string.Empty, "mpo-sidebar-value");
            _sidebarScore = MPOUI.Text(string.Empty, "mpo-small");
            targetCard.Add(_sidebarTarget);
            targetCard.Add(_sidebarScore);
            sidebar.Add(targetCard);
            sidebar.Add(MPOUI.Text("Use this tool to find and prioritize problems. Validate final performance with Unity Profiler and real devices.", "mpo-sidebar-foot"));

            RefreshSidebar();
            return sidebar;
        }

        private void AddNavButton(VisualElement sidebar, Page page, string text)
        {
            var button = new Button(() => SetPage(page)) { text = text };
            button.AddToClassList("mpo-nav-button");
            _navButtons[page] = button;
            sidebar.Add(button);
        }

        private void SetPage(Page page)
        {
            _page = page;
            UpdateNavState();
            RenderCurrentPage();
        }

        private void UpdateNavState()
        {
            foreach (KeyValuePair<Page, Button> pair in _navButtons)
            {
                if (pair.Key == _page)
                    pair.Value.AddToClassList("mpo-nav-active");
                else
                    pair.Value.RemoveFromClassList("mpo-nav-active");
            }
        }

        private void RefreshSidebar()
        {
            if (_sidebarTarget == null)
                return;

            _sidebarTarget.text = _targetPlatform + "  /  " + FriendlyTier(_deviceTier);
            if (_scanResult == null)
                _sidebarScore.text = "No scan yet";
            else
            {
                EnsureActiveIssueCache();
                _sidebarScore.text = "Last score  " + _cachedOverallScore + "/100  •  " + _cachedCriticalCount + " critical\n" + (_scanScope != null ? _scanScope.DisplayName : "Full Project");
            }
            UpdateNavState();
        }

        private void RenderCurrentPage()
        {
            if (_contentHost == null)
                return;

            _contentHost.Clear();
            UpdateNavState();

            switch (_page)
            {
                case Page.Issues:
                    _contentHost.Add(BuildIssuesPage());
                    break;
                case Page.Fixes:
                    _contentHost.Add(BuildFixesPage());
                    break;
                case Page.Reports:
                    _contentHost.Add(BuildReportsPage());
                    break;
                default:
                    _contentHost.Add(BuildOverviewPage());
                    break;
            }
        }

        private VisualElement BuildOverviewPage()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("mpo-scroll-page");
            scroll.contentContainer.AddToClassList("mpo-scroll-content");
            scroll.Add(BuildPageHeader("Scan Your Project", "Choose a target and scan only what you need. Start broad, then use focused scans while fixing specific content."));
            AddStatusBanners(scroll);

            var setup = MPOUI.Card("mpo-scan-setup-card");
            setup.Add(MPOUI.Text("1  Choose Platform", "mpo-step-title"));
            var platformRow = new VisualElement();
            platformRow.AddToClassList("mpo-choice-row");
            _platformTabs.Clear();
            platformRow.Add(BuildPlatformChoice(MPOTargetPlatform.Android, "Android"));
            platformRow.Add(BuildPlatformChoice(MPOTargetPlatform.iOS, "iOS"));
            setup.Add(platformRow);

            setup.Add(MPOUI.Text("2  Select Device Tier", "mpo-step-title"));
            var tierRow = new VisualElement();
            tierRow.AddToClassList("mpo-choice-row");
            _tierTabs.Clear();
            tierRow.Add(BuildTierChoice(MPODeviceTier.LowEnd, "Low End"));
            tierRow.Add(BuildTierChoice(MPODeviceTier.MidRange, "Mid Range"));
            tierRow.Add(BuildTierChoice(MPODeviceTier.HighEnd, "High End"));
            setup.Add(tierRow);

            setup.Add(MPOUI.Text("3  Choose What To Scan", "mpo-step-title"));
            var scopeRow = new VisualElement();
            scopeRow.AddToClassList("mpo-scope-grid");
            _scopeTabs.Clear();
            scopeRow.Add(BuildScopeChoice(MPOScanScopeMode.FullProject, "Full Project", "Scan everything"));
            scopeRow.Add(BuildScopeChoice(MPOScanScopeMode.BuildScenes, "Build Scenes", "Enabled build content"));
            scopeRow.Add(BuildScopeChoice(MPOScanScopeMode.CurrentScene, "Current Scene", "Active scene + dependencies"));
            scopeRow.Add(BuildScopeChoice(MPOScanScopeMode.SelectedFolder, "Selected Folder", "One folder + subfolders"));
            scopeRow.Add(BuildScopeChoice(MPOScanScopeMode.SelectedAssets, "Selected Assets", "Current Project selection"));
            setup.Add(scopeRow);

            if (_scanScopeMode == MPOScanScopeMode.SelectedFolder)
            {
                var folderRow = new VisualElement();
                folderRow.AddToClassList("mpo-folder-row");
                folderRow.Add(MPOUI.Text("Folder", "mpo-folder-label"));
                _folderField = new ObjectField
                {
                    objectType = typeof(DefaultAsset),
                    allowSceneObjects = false
                };
                _folderField.AddToClassList("mpo-folder-field");
                if (!string.IsNullOrWhiteSpace(_selectedFolderPath) && AssetDatabase.IsValidFolder(_selectedFolderPath))
                    _folderField.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(_selectedFolderPath));
                _folderField.RegisterValueChangedCallback(evt =>
                {
                    string path = evt.newValue != null ? AssetDatabase.GetAssetPath(evt.newValue) : string.Empty;
                    if (!string.IsNullOrWhiteSpace(path) && AssetDatabase.IsValidFolder(path))
                    {
                        _selectedFolderPath = path;
                        MPOEditorPreferences.SelectedFolder = path;
                        UpdateScopeStatus();
                    }
                    else if (evt.newValue != null)
                    {
                        _folderField.SetValueWithoutNotify(null);
                        _selectedFolderPath = string.Empty;
                        MPOEditorPreferences.SelectedFolder = string.Empty;
                        UpdateScopeStatus();
                    }
                });
                folderRow.Add(_folderField);
                setup.Add(folderRow);
            }

            _scopeStatusLabel = MPOUI.Text(string.Empty, "mpo-scope-hint");
            setup.Add(_scopeStatusLabel);
            UpdateScopeStatus();

            var actionRow = new VisualElement();
            actionRow.AddToClassList("mpo-scan-action-row");
            _scanButton = MPOUI.ActionButton(_scanResult == null ? "Start Scan" : "Start New Scan", RunScan, "mpo-button");
            _scanButton.AddToClassList("mpo-primary");
            _scanButton.AddToClassList("mpo-scan-cta");
            actionRow.Add(_scanButton);
            actionRow.Add(MPOUI.Text("The scan stays responsive and can be cancelled at any time.", "mpo-scan-action-hint"));
            setup.Add(actionRow);
            scroll.Add(setup);

            if (_scanResult == null)
            {
                var intro = MPOUI.Card("mpo-friendly-card");
                intro.Add(MPOUI.Text("Start simple", "mpo-next-title"));
                intro.Add(MPOUI.Text("For a first audit use Full Project. When working on one scene, folder or asset set, use a focused scope to avoid thousands of unrelated findings.", "mpo-copy"));
                scroll.Add(intro);
                return scroll;
            }

            EnsureActiveIssueCache();
            scroll.Add(MPOUI.SectionHeader("Last Scan Results", (_scanScope != null ? _scanScope.DisplayName : "Full Project") + "  •  " + _scanPlatform + " / " + FriendlyTier(_scanTier)));

            var topRow = new VisualElement();
            topRow.AddToClassList("mpo-card-row");
            topRow.Add(BuildScoreCard());
            topRow.Add(BuildStatCard("CRITICAL", _cachedCriticalCount, "Fix these first"));
            topRow.Add(BuildStatCard("WARNINGS", _cachedWarningCount, "Worth reviewing"));
            topRow.Add(BuildStatCard("SAFE FIXES", _cachedSafeFixCount, "Can be fixed automatically"));
            scroll.Add(topRow);

            var quick = new VisualElement();
            quick.AddToClassList("mpo-home-actions");
            var problems = MPOUI.ActionButton("Review Problems", () => SetPage(Page.Issues));
            problems.AddToClassList("mpo-primary");
            quick.Add(problems);
            Action quickFixAction = _cachedSafeFixCount > 0
                ? (Action)FixAllSafeIssues
                : () => SetPage(Page.Fixes);
            var fixes = MPOUI.ActionButton(_cachedSafeFixCount > 0 ? "Fix All Safe Issues" : "Open Fixes", quickFixAction);
            if (_cachedSafeFixCount > 0) fixes.AddToClassList("mpo-safe-action");
            quick.Add(fixes);
            quick.Add(MPOUI.ActionButton("Export Report", () => SetPage(Page.Reports)));
            scroll.Add(quick);

            VisualElement diagnostics = BuildDiagnosticsCard();
            if (diagnostics != null)
                scroll.Add(diagnostics);

            scroll.Add(MPOUI.SectionHeader("Top Problems", "Focus on the biggest repeated problems first. Open a group only when you need to inspect the affected assets."));
            scroll.Add(BuildTopPriorityGroups());
            return scroll;
        }

        private Button BuildPlatformChoice(MPOTargetPlatform platform, string label)
        {
            var button = new Button(() => SetTargetPlatform(platform)) { text = label };
            button.AddToClassList("mpo-segment");
            _platformTabs[platform] = button;
            RefreshChoiceState(button, platform == _targetPlatform);
            return button;
        }

        private Button BuildTierChoice(MPODeviceTier tier, string label)
        {
            var button = new Button(() => SetDeviceTier(tier)) { text = label };
            button.AddToClassList("mpo-segment");
            _tierTabs[tier] = button;
            RefreshChoiceState(button, tier == _deviceTier);
            return button;
        }

        private Button BuildScopeChoice(MPOScanScopeMode mode, string title, string subtitle)
        {
            var button = new Button(() => SetScanScopeMode(mode));
            button.AddToClassList("mpo-scope-choice");
            button.Add(MPOUI.Text(title, "mpo-scope-choice-title"));
            button.Add(MPOUI.Text(subtitle, "mpo-scope-choice-copy"));
            _scopeTabs[mode] = button;
            RefreshChoiceState(button, mode == _scanScopeMode);
            return button;
        }

        private static void RefreshChoiceState(VisualElement element, bool active)
        {
            if (element == null)
                return;
            if (active) element.AddToClassList("mpo-choice-active");
            else element.RemoveFromClassList("mpo-choice-active");
        }

        private void SetTargetPlatform(MPOTargetPlatform platform)
        {
            if (MPOScanRunner.IsRunning) return;
            if (_targetPlatform == platform)
                return;
            _targetPlatform = platform;
            MPOEditorPreferences.TargetPlatform = platform;
            if (_scanResult != null) _needsRescan = true;
            foreach (var pair in _platformTabs) RefreshChoiceState(pair.Value, pair.Key == _targetPlatform);
            RefreshTargetBadges();
            RefreshSidebar();
        }

        private void SetDeviceTier(MPODeviceTier tier)
        {
            if (MPOScanRunner.IsRunning) return;
            if (_deviceTier == tier)
                return;
            _deviceTier = tier;
            MPOEditorPreferences.DeviceTier = tier;
            if (_scanResult != null) _needsRescan = true;
            foreach (var pair in _tierTabs) RefreshChoiceState(pair.Value, pair.Key == _deviceTier);
            RefreshTargetBadges();
            RefreshSidebar();
        }

        private void SetScanScopeMode(MPOScanScopeMode mode)
        {
            if (MPOScanRunner.IsRunning) return;
            if (_scanScopeMode == mode)
                return;
            _scanScopeMode = mode;
            MPOEditorPreferences.ScanScope = mode;
            if (_scanResult != null) _needsRescan = true;
            // Folder/selection controls are conditional, so rebuild this lightweight page only.
            RenderCurrentPage();
        }

        private void RefreshTargetBadges()
        {
            if (_topPlatformBadge != null) _topPlatformBadge.text = _targetPlatform.ToString();
            if (_topTierBadge != null) _topTierBadge.text = FriendlyTier(_deviceTier);
        }

        private void UpdateScopeStatus()
        {
            if (_scopeStatusLabel == null)
                return;

            switch (_scanScopeMode)
            {
                case MPOScanScopeMode.BuildScenes:
                    int buildCount = (EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>()).Count(scene => scene != null && scene.enabled);
                    _scopeStatusLabel.text = buildCount > 0
                        ? buildCount + " enabled build scene(s). Referenced assets are scanned; loaded build scenes also receive scene-component checks."
                        : "No enabled Build Settings scenes found.";
                    break;
                case MPOScanScopeMode.CurrentScene:
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    _scopeStatusLabel.text = scene.IsValid() ? "Current scene: " + (string.IsNullOrWhiteSpace(scene.name) ? "Unnamed Scene" : scene.name) : "No active scene.";
                    break;
                case MPOScanScopeMode.SelectedFolder:
                    _scopeStatusLabel.text = !string.IsNullOrWhiteSpace(_selectedFolderPath) && AssetDatabase.IsValidFolder(_selectedFolderPath)
                        ? "Folder: " + _selectedFolderPath
                        : "Choose a folder inside Assets.";
                    break;
                case MPOScanScopeMode.SelectedAssets:
                    int selectionCount = (Selection.objects ?? Array.Empty<UnityEngine.Object>()).Count(obj => obj != null && !string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(obj)));
                    _scopeStatusLabel.text = selectionCount > 0 ? selectionCount + " selected project item(s) will be scanned with their dependencies." : "Select one or more assets in the Project window.";
                    break;
                default:
                    _scopeStatusLabel.text = "Full Project scans all supported project assets plus currently loaded scene content.";
                    break;
            }
        }

        private VisualElement BuildPageHeader(string title, string subtitle)
        {
            var head = new VisualElement();
            head.AddToClassList("mpo-page-head");
            var main = new VisualElement();
            main.AddToClassList("mpo-page-head-main");
            main.Add(MPOUI.Text(title, "mpo-page-title"));
            main.Add(MPOUI.Text(subtitle, "mpo-page-subtitle"));
            head.Add(main);
            return head;
        }

        private void AddStatusBanners(VisualElement parent)
        {
            if (parent == null || _scanResult == null)
                return;

            if (_needsRescan)
                parent.Add(BuildBanner("Target/profile or project state changed. Re-scan before trusting exports or the score.", true));

            if (_scanResult.WasCancelled)
                parent.Add(BuildBanner("The previous scan was cancelled. Results are partial.", true));

            if (_scanResult.AnalyzerErrors.Count > 0)
            {
                string message = "Scan continued after " + _scanResult.AnalyzerErrors.Count + " analyzer failure(s). Completed " +
                                 _scanResult.CompletedAnalyzerCount + "/" + _scanResult.TotalAnalyzerCount + " analyzers.";
                parent.Add(BuildBanner(message, true));
            }

            if (_scanResult.RecoverableErrorCount > 0)
            {
                string message = _scanResult.RecoverableErrorCount + " unreadable/broken asset or component item(s) were skipped safely. The rest of the scan completed.";
                parent.Add(BuildActionBanner(message, "View Diagnostics", () => MPODiagnosticsWindow.ShowFor(_scanResult)));
            }
        }

        private static VisualElement BuildBanner(string text, bool warning)
        {
            var banner = MPOUI.Text(text, "mpo-banner");
            banner.AddToClassList(warning ? "mpo-banner-warning" : "mpo-banner-info");
            return banner;
        }

        private static VisualElement BuildActionBanner(string text, string actionLabel, Action action)
        {
            var banner = new VisualElement();
            banner.AddToClassList("mpo-banner");
            banner.AddToClassList("mpo-banner-info");
            banner.AddToClassList("mpo-banner-action");

            var copy = MPOUI.Text(text, "mpo-banner-copy");
            banner.Add(copy);

            var button = MPOUI.ActionButton(actionLabel, action, "mpo-button");
            button.AddToClassList("mpo-ghost");
            banner.Add(button);
            return banner;
        }


        private VisualElement BuildDiagnosticsCard()
        {
            if (_scanResult == null || (_scanResult.AnalyzerErrors.Count == 0 && _scanResult.RecoverableErrorCount == 0))
                return null;

            var card = MPOUI.Card("mpo-no-right-margin");
            card.Add(MPOUI.Text("SCAN DIAGNOSTICS", "mpo-stat-kicker"));
            card.Add(MPOUI.Text(
                _scanResult.FailedAnalyzerCount + " failed analyzer(s)  •  " +
                _scanResult.RecoverableErrorCount + " skipped item(s)  •  " +
                _scanResult.CompletedAnalyzerCount + "/" + _scanResult.TotalAnalyzerCount + " analyzers completed",
                "mpo-next-title"));

            foreach (string error in _scanResult.AnalyzerErrors.Take(4))
                card.Add(MPOUI.Text("Analyzer — " + error, "mpo-stat-copy"));
            foreach (string warning in _scanResult.RecoverableWarnings.Take(6))
                card.Add(MPOUI.Text("Skipped — " + warning, "mpo-stat-copy"));

            int hidden = Math.Max(0, _scanResult.RecoverableErrorCount - Math.Min(6, _scanResult.RecoverableWarnings.Count));
            if (hidden > 0)
                card.Add(MPOUI.Text("… plus " + hidden + " additional skipped item(s). Full diagnostic totals are preserved in exported reports.", "mpo-stat-copy"));

            var actions = new VisualElement();
            actions.AddToClassList("mpo-action-row");
            actions.Add(MPOUI.ActionButton("View Full Diagnostics", () => MPODiagnosticsWindow.ShowFor(_scanResult)));
            card.Add(actions);
            return card;
        }

        private VisualElement BuildScoreCard()
        {
            int score = _cachedOverallScore;
            var card = MPOUI.Card("mpo-score-card");
            card.Add(MPOUI.Text("PROJECT HEALTH", "mpo-stat-kicker"));

            var main = new VisualElement();
            main.AddToClassList("mpo-score-main");
            var ring = new VisualElement();
            ring.AddToClassList("mpo-score-ring");
            ring.style.borderTopColor = ScoreColor(score);
            ring.style.borderRightColor = ScoreColor(score);
            ring.style.borderBottomColor = ScoreColor(score);
            ring.style.borderLeftColor = ScoreColor(score);
            ring.Add(MPOUI.Text(score.ToString(), "mpo-score-number"));
            ring.Add(MPOUI.Text("OUT OF 100", "mpo-score-denom"));
            main.Add(ring);

            var copy = new VisualElement();
            copy.AddToClassList("mpo-flex");
            copy.Add(MPOUI.Text(MPOScoreCalculator.GetRating(score), "mpo-score-rating"));
            copy.Add(MPOUI.Text(_scanPlatform + "  •  " + FriendlyTier(_scanTier), "mpo-score-caption"));
            copy.Add(MPOUI.Text("Static project-health estimate. Runtime profiling is still required.", "mpo-score-caption"));
            main.Add(copy);
            card.Add(main);

            var scoring = new Foldout
            {
                text = "How this score is calculated",
                value = false
            };
            scoring.AddToClassList("mpo-score-foldout");
            scoring.Add(MPOUI.Text(
                "Each analyzer reports active findings into a category score. Critical, warning and suggestion penalties are capped per category, then category scores are combined with mobile-focused weights. Ignored findings are excluded. The score is a static project-health heuristic — it does not predict FPS.",
                "mpo-score-explain"));
            card.Add(scoring);
            return card;
        }

        private static VisualElement BuildStatCard(string title, int value, string copy)
        {
            var card = MPOUI.Card("mpo-stat-card");
            card.Add(MPOUI.Text(title, "mpo-stat-kicker"));
            card.Add(MPOUI.Text(value.ToString("N0"), "mpo-stat-value"));
            card.Add(MPOUI.Text(copy, "mpo-stat-copy"));
            return card;
        }

        private static VisualElement BuildImpactCard(string title, int count)
        {
            var card = MPOUI.Card("mpo-stat-card");
            card.Add(MPOUI.Text(title, "mpo-stat-kicker"));
            card.Add(MPOUI.Text(count > 0 ? count + " high" : "No high", "mpo-stat-value"));
            card.Add(MPOUI.Text(count > 0 ? "Priority findings to validate" : "No high-impact findings", "mpo-stat-copy"));
            return card;
        }

        private VisualElement BuildTopPriorityGroups()
        {
            var host = new VisualElement();
            host.AddToClassList("mpo-priority-list");

            List<MPOIssueGroup> groups = MPOIssueGroup.Build(_activeIssueCache).Take(5).ToList();
            if (groups.Count == 0)
            {
                var card = MPOUI.Card("mpo-no-right-margin");
                card.Add(MPOUI.Text("No active static findings", "mpo-next-title"));
                card.Add(MPOUI.Text("Validate representative gameplay with Unity Profiler and real target devices before shipping.", "mpo-copy"));
                host.Add(card);
                return host;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                MPOIssueGroup group = groups[i];
                var row = MPOUI.Card("mpo-priority-row");

                var rank = new VisualElement();
                rank.AddToClassList("mpo-priority-rank");
                rank.Add(MPOUI.Text((i + 1).ToString(), "mpo-priority-rank-text"));
                row.Add(rank);

                var copy = new VisualElement();
                copy.AddToClassList("mpo-priority-copy");
                var badges = new VisualElement();
                badges.AddToClassList("mpo-row");
                badges.Add(MPOUI.Badge(SeverityLabel(group.Severity), MPOUI.SeverityClass(group.Severity)));
                badges.Add(MPOUI.Badge(group.Category.ToString().ToUpperInvariant(), "mpo-impact-none"));
                badges.Add(MPOUI.Badge(group.Count.ToString("N0") + " FINDINGS", "mpo-impact-none"));
                copy.Add(badges);
                copy.Add(MPOUI.Text(group.Title, "mpo-priority-title"));

                string breadth = group.DistinctAssetCount > 0
                    ? group.DistinctAssetCount.ToString("N0") + " affected asset(s)"
                    : group.Count.ToString("N0") + " affected item(s)";
                copy.Add(MPOUI.Text(
                    breadth + "  •  Highest impact " + MPOImpactUtility.Label(group.HighestImpact) +
                    "  •  " + (group.SafeFixCount > 0 ? group.SafeFixCount + " safe fix(es)" : group.FixableCount > 0 ? group.FixableCount + " review fix(es)" : "manual review"),
                    "mpo-priority-meta"));
                row.Add(copy);

                var action = MPOUI.ActionButton("Open Group", () => ReviewIssueGroup(group));
                action.AddToClassList("mpo-primary");
                row.Add(action);
                host.Add(row);
            }

            return host;
        }

        private void ReviewIssueGroup(MPOIssueGroup group)
        {
            if (group == null || group.Representative == null)
                return;

            // A group review is an explicit drill-down action. Clear ordinary filters so they cannot
            // silently hide hundreds of members from the selected group. The exact group key is
            // retained separately and the individual-asset view exposes every underlying finding.
            _search = string.Empty;
            _categoryFilterIndex = 0;
            _showCritical = true;
            _showWarnings = true;
            _showSuggestions = true;
            _showOnlyFixable = false;
            _showOnlyHighImpact = false;
            _sortMode = IssueSortMode.Severity;
            _findingsViewMode = FindingsViewMode.Assets;
            _groupFilterKey = group.Key;
            _selectedIssueGroup = group;
            _selectedIssue = group.Representative;

            SetPage(Page.Issues);
        }

        private VisualElement BuildGroupReviewBanner()
        {
            if (string.IsNullOrWhiteSpace(_groupFilterKey))
                return null;

            MPOIssue representative = FindRepresentativeForGroup(_groupFilterKey);
            int count = CountIssuesInGroup(_groupFilterKey);
            if (representative == null || count <= 0)
                return null;

            var banner = MPOUI.Card("mpo-group-review-banner");
            var copy = new VisualElement();
            copy.AddToClassList("mpo-group-review-copy");
            copy.Add(MPOUI.Text("PROBLEM GROUP", "mpo-stat-kicker"));
            copy.Add(MPOUI.Text(representative.Title, "mpo-group-review-title"));
            copy.Add(MPOUI.Text(
                count.ToString("N0") + " affected item(s) are shown below. Filters were cleared so you can review the complete group.",
                "mpo-group-review-meta"));
            banner.Add(copy);

            var exit = MPOUI.ActionButton("Back to all problems", () =>
            {
                _groupFilterKey = string.Empty;
                _selectedIssueGroup = null;
                _selectedIssue = null;
                _findingsViewMode = FindingsViewMode.Groups;
                SetPage(Page.Issues);
            });
            exit.AddToClassList("mpo-ghost");
            banner.Add(exit);
            return banner;
        }

        private int CountIssuesInGroup(string groupKey)
        {
            if (string.IsNullOrWhiteSpace(groupKey))
                return 0;

            EnsureActiveIssueCache();
            return _activeIssueCache.Count(issue =>
                issue != null && string.Equals(MPOIssueGroup.GetKey(issue), groupKey, StringComparison.Ordinal));
        }

        private MPOIssue FindRepresentativeForGroup(string groupKey)
        {
            if (string.IsNullOrWhiteSpace(groupKey))
                return null;

            EnsureActiveIssueCache();
            return _activeIssueCache
                .Where(issue => issue != null && string.Equals(MPOIssueGroup.GetKey(issue), groupKey, StringComparison.Ordinal))
                .OrderByDescending(issue => issue.Severity)
                .ThenByDescending(issue => issue.ImpactScore)
                .ThenByDescending(issue => issue.Penalty)
                .FirstOrDefault();
        }

        private VisualElement BuildNextStepCard()
        {
            var card = MPOUI.Card("mpo-next-card");
            MPOIssue issue = _activeIssueCache
                .OrderByDescending(x => x.Severity)
                .ThenByDescending(x => x.ImpactScore)
                .ThenByDescending(x => x.Penalty)
                .FirstOrDefault();

            if (issue == null)
            {
                card.Add(MPOUI.Text("NEXT STEP", "mpo-next-kicker"));
                card.Add(MPOUI.Text("Validate on target hardware", "mpo-next-title"));
                card.Add(MPOUI.Text("No active static findings are reported. Profile representative gameplay on real Android/iOS hardware before shipping.", "mpo-copy"));
                return card;
            }

            card.Add(MPOUI.Text("RECOMMENDED NEXT ACTION", "mpo-next-kicker"));
            card.Add(MPOUI.Text(issue.Title, "mpo-next-title"));
            card.Add(MPOUI.Text(issue.Recommendation, "mpo-copy"));
            var actions = new VisualElement();
            actions.AddToClassList("mpo-action-row");
            var open = MPOUI.ActionButton("Open Finding", () =>
            {
                _selectedIssue = issue;
                SetPage(Page.Issues);
            });
            open.AddToClassList("mpo-primary");
            actions.Add(open);
            if (issue.CanFix)
                actions.Add(MPOUI.ActionButton("Preview Fix", () => OpenFixPreview(issue)));
            card.Add(actions);
            return card;
        }

        private VisualElement BuildCategoryCard(MPOCategory category)
        {
            var card = MPOUI.Card("mpo-category-card");
            int score = 100;
            int issues = 0;
            int checks = 0;
            MPOCategoryResult result = null;
            if (_scanResult.Categories.TryGetValue(category, out result))
            {
                score = MPOScoreCalculator.CalculateCategory(result);
                checks = result.TotalChecks;
            }
            _activeCategoryCounts.TryGetValue(category, out issues);

            var top = new VisualElement();
            top.AddToClassList("mpo-category-top");
            top.Add(MPOUI.Text(category.ToString(), "mpo-category-name"));
            top.Add(MPOUI.Text(score + "%", "mpo-category-score"));
            card.Add(top);
            card.Add(MPOUI.Text(issues + " finding(s)  •  " + checks + " checks", "mpo-category-meta"));
            if (result != null && result.Metrics.Count > 0)
            {
                string metrics = string.Join("  •  ", result.Metrics.Take(2).Select(pair => pair.Key + " " + pair.Value));
                card.Add(MPOUI.Text(metrics, "mpo-category-meta"));
            }

            var meter = new VisualElement();
            meter.AddToClassList("mpo-meter");
            var fill = new VisualElement();
            fill.AddToClassList("mpo-meter-fill");
            fill.style.width = new Length(score, LengthUnit.Percent);
            fill.style.backgroundColor = ScoreColor(score);
            meter.Add(fill);
            card.Add(meter);
            return card;
        }

        private VisualElement BuildIssuesPage()
        {
            var page = new VisualElement();
            page.AddToClassList("mpo-page");
            page.Add(BuildPageHeader("Problems", "Start with grouped problems. Open individual assets only when you need to review or fix a specific item."));
            AddStatusBanners(page);

            if (_scanResult == null)
            {
                page.Add(MPOUI.EmptyState("No problems yet", "Go to Scan and run an analysis first."));
                return page;
            }

            EnsureActiveIssueCache();
            VisualElement quickFixBar = BuildProblemsQuickFixBar();
            if (quickFixBar != null)
                page.Add(quickFixBar);
            page.Add(BuildIssueToolbar());
            VisualElement groupReviewBanner = BuildGroupReviewBanner();
            if (groupReviewBanner != null)
                page.Add(groupReviewBanner);

            var meta = new VisualElement();
            meta.AddToClassList("mpo-results-meta");
            _issueCountLabel = MPOUI.Text(string.Empty, "mpo-results-count");
            meta.Add(_issueCountLabel);
            meta.Add(MPOUI.Text("Virtualized rows keep large projects smooth.", "mpo-results-hint"));
            page.Add(meta);

            var split = new VisualElement();
            split.AddToClassList("mpo-split");

            var listPane = new VisualElement();
            listPane.AddToClassList("mpo-list-pane");
            _issueList = new ListView();
            _issueList.AddToClassList("mpo-list");
            _issueList.selectionType = SelectionType.Single;
            _issueList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _issueList.fixedItemHeight = 86f;
            _issueList.makeItem = MakeFindingRow;
            _issueList.bindItem = BindFindingRow;
            _issueList.unbindItem = (element, index) => { };
            _issueList.selectedIndicesChanged += OnIssueSelectionChanged;
            listPane.Add(_issueList);
            split.Add(listPane);

            _issueDetailHost = new VisualElement();
            _issueDetailHost.AddToClassList("mpo-detail-pane");
            split.Add(_issueDetailHost);
            page.Add(split);

            RefreshIssueList();
            return page;
        }

        private VisualElement BuildProblemsQuickFixBar()
        {
            EnsureActiveIssueCache();
            if (_cachedFixableCount <= 0)
                return null;

            var card = MPOUI.Card("mpo-quick-fix-bar");
            var copy = new VisualElement();
            copy.AddToClassList("mpo-flex");
            copy.Add(MPOUI.Text("QUICK FIX", "mpo-stat-kicker"));
            copy.Add(MPOUI.Text(_cachedFixableCount.ToString("N0") + " automatic fix action(s) available", "mpo-next-title"));
            copy.Add(MPOUI.Text("Includes mobile texture-size recommendations, GPU Instancing candidates and other reversible importer/project fixes. Manual issues are never changed.", "mpo-stat-copy"));
            card.Add(copy);

            var actions = new VisualElement();
            actions.AddToClassList("mpo-action-row");
            var fixAll = MPOUI.ActionButton("Fix All Recommended...", () => OpenAutomaticFixes(_activeIssueCache));
            fixAll.AddToClassList("mpo-primary");
            actions.Add(fixAll);

            if (_categoryFilterIndex > 0)
            {
                MPOCategory category = (MPOCategory)(_categoryFilterIndex - 1);
                List<MPOIssue> categoryFixes = _activeIssueCache.Where(issue => issue != null && issue.Category == category && issue.CanFix).ToList();
                if (categoryFixes.Count > 0)
                    actions.Add(MPOUI.ActionButton("Fix " + category + "...", () => OpenAutomaticFixes(categoryFixes)));
            }

            actions.Add(MPOUI.ActionButton("Open Fixes Page", () => SetPage(Page.Fixes)));
            card.Add(actions);
            return card;
        }

        private VisualElement BuildIssueToolbar()
        {
            var block = new VisualElement();

            var viewRow = new VisualElement();
            viewRow.AddToClassList("mpo-view-row");
            viewRow.Add(MPOUI.Text("SHOW", "mpo-stat-kicker"));
            _groupedViewButton = CreateFilterChip("Grouped Problems", _findingsViewMode == FindingsViewMode.Groups, () =>
            {
                _groupFilterKey = string.Empty;
                _selectedIssueGroup = null;
                _selectedIssue = null;
                _findingsViewMode = FindingsViewMode.Groups;
                RefreshFindingViewButtons();
                RefreshIssueList();
            });
            _assetViewButton = CreateFilterChip("Individual Assets", _findingsViewMode == FindingsViewMode.Assets, () =>
            {
                _findingsViewMode = FindingsViewMode.Assets;
                RefreshFindingViewButtons();
                RefreshIssueList();
            });
            viewRow.Add(_groupedViewButton);
            viewRow.Add(_assetViewButton);

            if (!string.IsNullOrWhiteSpace(_groupFilterKey))
            {
                int scopedCount = CountIssuesInGroup(_groupFilterKey);
                viewRow.Add(MPOUI.Badge("GROUP  •  " + scopedCount.ToString("N0") + " ITEMS", "mpo-impact-medium"));
                var clearGroup = MPOUI.ActionButton("Back to all", () =>
                {
                    _groupFilterKey = string.Empty;
                    _selectedIssueGroup = null;
                    _selectedIssue = null;
                    _findingsViewMode = FindingsViewMode.Groups;
                    SetPage(Page.Issues);
                }, "mpo-button");
                clearGroup.AddToClassList("mpo-ghost");
                viewRow.Add(clearGroup);
            }
            block.Add(viewRow);

            var categories = new VisualElement();
            categories.AddToClassList("mpo-category-tabs");
            _categoryTabs.Clear();
            categories.Add(BuildCategoryTab(0, "All"));
            foreach (MPOCategory category in Enum.GetValues(typeof(MPOCategory)))
                categories.Add(BuildCategoryTab((int)category + 1, category.ToString()));
            block.Add(categories);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("mpo-toolbar");
            _searchField = new ToolbarSearchField();
            _searchField.value = _search;
            _searchField.tooltip = "Search problems or asset names.";
            _searchField.AddToClassList("mpo-search");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                RefreshIssueList();
            });
            toolbar.Add(_searchField);

            var clear = MPOUI.ActionButton("Reset", ClearIssueFilters, "mpo-button");
            clear.AddToClassList("mpo-ghost");
            toolbar.Add(clear);
            block.Add(toolbar);

            var advanced = new Foldout { text = "More filters", value = false };
            advanced.AddToClassList("mpo-filter-foldout");
            var filters = new VisualElement();
            filters.AddToClassList("mpo-filter-strip");
            _criticalFilter = CreateFilterChip("Critical", _showCritical, () => { _showCritical = !_showCritical; RefreshFilterChip(_criticalFilter, _showCritical); RefreshIssueList(); });
            _warningFilter = CreateFilterChip("Warnings", _showWarnings, () => { _showWarnings = !_showWarnings; RefreshFilterChip(_warningFilter, _showWarnings); RefreshIssueList(); });
            _suggestionFilter = CreateFilterChip("Suggestions", _showSuggestions, () => { _showSuggestions = !_showSuggestions; RefreshFilterChip(_suggestionFilter, _showSuggestions); RefreshIssueList(); });
            _fixableFilter = CreateFilterChip("Fixable only", _showOnlyFixable, () => { _showOnlyFixable = !_showOnlyFixable; RefreshFilterChip(_fixableFilter, _showOnlyFixable); RefreshIssueList(); });
            _impactFilter = CreateFilterChip("High impact", _showOnlyHighImpact, () => { _showOnlyHighImpact = !_showOnlyHighImpact; RefreshFilterChip(_impactFilter, _showOnlyHighImpact); RefreshIssueList(); });
            filters.Add(_criticalFilter);
            filters.Add(_warningFilter);
            filters.Add(_suggestionFilter);
            filters.Add(_fixableFilter);
            filters.Add(_impactFilter);

            _sortField = new EnumField("Sort", _sortMode);
            _sortField.AddToClassList("mpo-sort");
            _sortField.RegisterValueChangedCallback(evt =>
            {
                _sortMode = (IssueSortMode)evt.newValue;
                RefreshIssueList();
            });
            filters.Add(_sortField);
            advanced.Add(filters);
            block.Add(advanced);
            return block;
        }

        private Button BuildCategoryTab(int index, string label)
        {
            var button = new Button(() =>
            {
                _categoryFilterIndex = index;
                RefreshCategoryTabs();
                RefreshIssueList();
            }) { text = label };
            button.AddToClassList("mpo-category-tab");
            _categoryTabs[index] = button;
            if (_categoryFilterIndex == index)
                button.AddToClassList("mpo-category-tab-active");
            return button;
        }

        private void RefreshCategoryTabs()
        {
            foreach (var pair in _categoryTabs)
            {
                if (pair.Key == _categoryFilterIndex) pair.Value.AddToClassList("mpo-category-tab-active");
                else pair.Value.RemoveFromClassList("mpo-category-tab-active");
            }
        }

        private void RefreshFindingViewButtons()
        {
            RefreshFilterChip(_groupedViewButton, _findingsViewMode == FindingsViewMode.Groups);
            RefreshFilterChip(_assetViewButton, _findingsViewMode == FindingsViewMode.Assets);
        }

        private static Button CreateFilterChip(string text, bool active, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList("mpo-filter-chip");
            if (active)
                button.AddToClassList("mpo-filter-active");
            return button;
        }

        private static void RefreshFilterChip(Button button, bool active)
        {
            if (button == null)
                return;
            if (active)
                button.AddToClassList("mpo-filter-active");
            else
                button.RemoveFromClassList("mpo-filter-active");
        }

        private void ClearIssueFilters()
        {
            bool wasReviewingGroup = !string.IsNullOrWhiteSpace(_groupFilterKey);
            _search = string.Empty;
            _categoryFilterIndex = 0;
            _showCritical = _showWarnings = _showSuggestions = true;
            _showOnlyFixable = false;
            _showOnlyHighImpact = false;
            _sortMode = IssueSortMode.Severity;
            _groupFilterKey = string.Empty;
            _selectedIssueGroup = null;

            if (_searchField != null) _searchField.SetValueWithoutNotify(string.Empty);
            if (_categoryField != null) _categoryField.SetValueWithoutNotify(CategoryOptions[0]);
            RefreshCategoryTabs();
            if (_sortField != null) _sortField.SetValueWithoutNotify(_sortMode);
            RefreshFilterChip(_criticalFilter, true);
            RefreshFilterChip(_warningFilter, true);
            RefreshFilterChip(_suggestionFilter, true);
            RefreshFilterChip(_fixableFilter, false);
            RefreshFilterChip(_impactFilter, false);
            if (wasReviewingGroup)
            {
                _findingsViewMode = FindingsViewMode.Groups;
                SetPage(Page.Issues);
            }
            else
            {
                RefreshIssueList();
            }
        }

        private VisualElement MakeFindingRow()
        {
            var row = new VisualElement();
            row.AddToClassList("mpo-issue-row");
            var stripe = new VisualElement { name = "stripe" };
            stripe.AddToClassList("mpo-issue-stripe");
            row.Add(stripe);

            var main = new VisualElement();
            main.AddToClassList("mpo-issue-row-main");
            var top = new VisualElement();
            top.AddToClassList("mpo-issue-row-top");
            var title = MPOUI.Text(string.Empty, "mpo-issue-row-title");
            title.name = "title";
            var safety = MPOUI.Text(string.Empty, "mpo-small");
            safety.name = "safety";
            top.Add(title);
            top.Add(safety);
            main.Add(top);
            var meta = MPOUI.Text(string.Empty, "mpo-issue-row-meta");
            meta.name = "meta";
            main.Add(meta);
            var impact = MPOUI.Text(string.Empty, "mpo-issue-row-impact");
            impact.name = "impact";
            main.Add(impact);
            row.Add(main);
            return row;
        }

        private void BindFindingRow(VisualElement element, int index)
        {
            if (_findingsViewMode == FindingsViewMode.Groups)
            {
                if (index < 0 || index >= _visibleGroupCache.Count)
                    return;

                MPOIssueGroup group = _visibleGroupCache[index];
                element.Q<Label>("title").text = group.Title;
                element.Q<Label>("safety").text = group.SafeFixCount > 0
                    ? group.SafeFixCount + " SAFE"
                    : group.FixableCount > 0 ? group.FixableCount + " REVIEW" : "MANUAL";
                element.Q<Label>("meta").text = SeverityLabel(group.Severity) + "  •  " + group.Category +
                                               "  •  " + group.Count.ToString("N0") + " finding(s)" +
                                               (group.DistinctAssetCount > 0 ? "  •  " + group.DistinctAssetCount.ToString("N0") + " asset(s)" : string.Empty);
                element.Q<Label>("impact").text = "Impact  " + MPOImpactUtility.Label(group.HighestImpact) +
                                                 (group.FixableCount > 0 ? "  •  " + group.FixableCount.ToString("N0") + " fixable" : "  •  Manual review");
                element.Q<VisualElement>("stripe").style.backgroundColor = SeverityColor(group.Severity);
                element.tooltip = group.Count.ToString("N0") + " similar findings from rule " + group.RuleId;
                return;
            }

            if (index < 0 || index >= _visibleIssueCache.Count)
                return;

            MPOIssue issue = _visibleIssueCache[index];
            element.Q<Label>("title").text = issue.Title;
            element.Q<Label>("safety").text = FixLabel(issue);
            element.Q<Label>("meta").text = SeverityLabel(issue.Severity) + "  •  " + issue.Category +
                                           (string.IsNullOrWhiteSpace(issue.AssetPath) ? string.Empty : "  •  " + ShortAssetName(issue.AssetPath));
            element.Q<Label>("impact").text = "Impact  " + MPOImpactUtility.Label(issue.HighestImpact) +
                                             (issue.CanFix ? "  •  " + (issue.FixSafety == MPOFixSafety.Safe ? "Safe fix" : "Review fix") : "  •  Manual review");
            element.Q<VisualElement>("stripe").style.backgroundColor = SeverityColor(issue.Severity);
            element.tooltip = string.IsNullOrWhiteSpace(issue.AssetPath) ? issue.Title : issue.AssetPath;
        }

        private void OnIssueSelectionChanged(IEnumerable<int> indices)
        {
            foreach (int index in indices)
            {
                if (_findingsViewMode == FindingsViewMode.Groups)
                {
                    if (index >= 0 && index < _visibleGroupCache.Count)
                    {
                        _selectedIssueGroup = _visibleGroupCache[index];
                        _selectedIssue = _selectedIssueGroup.Representative;
                        RefreshIssueDetail();
                    }
                }
                else if (index >= 0 && index < _visibleIssueCache.Count)
                {
                    _selectedIssue = _visibleIssueCache[index];
                    _selectedIssueGroup = null;
                    RefreshIssueDetail();
                }
                break;
            }
        }

        private void RefreshIssueList()
        {
            if (_issueList == null)
                return;

            List<MPOIssue> visible = GetVisibleIssuesCached();
            if (_findingsViewMode == FindingsViewMode.Groups)
            {
                List<MPOIssueGroup> groups = GetVisibleGroupsCached();
                _issueList.itemsSource = groups;
                _issueList.Rebuild();

                if (_issueCountLabel != null)
                    _issueCountLabel.text = groups.Count.ToString("N0") + " problem group(s)  •  " + visible.Count.ToString("N0") + " affected item(s)";

                if (_selectedIssueGroup == null || !groups.Contains(_selectedIssueGroup))
                    _selectedIssueGroup = groups.FirstOrDefault();
                _selectedIssue = _selectedIssueGroup != null ? _selectedIssueGroup.Representative : null;
            }
            else
            {
                _issueList.itemsSource = visible;
                _issueList.Rebuild();

                if (_issueCountLabel != null)
                    _issueCountLabel.text = visible.Count.ToString("N0") + " affected item(s)";

                if (_selectedIssue == null || !visible.Contains(_selectedIssue))
                    _selectedIssue = visible.FirstOrDefault();
                _selectedIssueGroup = null;
            }

            RefreshIssueDetail();
        }

        private void RefreshIssueDetail()
        {
            if (_issueDetailHost == null)
                return;

            _issueDetailHost.Clear();
            if (_findingsViewMode == FindingsViewMode.Groups)
            {
                if (_selectedIssueGroup == null)
                {
                    _issueDetailHost.Add(MPOUI.EmptyState("No matching problem groups", "Adjust the filters or run a fresh scan."));
                    return;
                }

                _issueDetailHost.Add(BuildIssueGroupDetail(_selectedIssueGroup));
                return;
            }

            if (_selectedIssue == null)
            {
                _issueDetailHost.Add(MPOUI.EmptyState("No matching findings", "Adjust the filters or run a fresh scan."));
                return;
            }

            _issueDetailHost.Add(BuildIssueDetailCard(_selectedIssue, false));
        }

        private VisualElement BuildIssueGroupDetail(MPOIssueGroup group)
        {
            var card = MPOUI.Card("mpo-detail-card");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("mpo-detail-scroll");

            var head = new VisualElement();
            head.AddToClassList("mpo-detail-head");
            head.Add(MPOUI.Badge(SeverityLabel(group.Severity), MPOUI.SeverityClass(group.Severity)));
            head.Add(MPOUI.Badge(group.Category.ToString().ToUpperInvariant(), "mpo-impact-none"));
            scroll.Add(head);

            scroll.Add(MPOUI.Text(group.Title, "mpo-detail-title"));
            string breadth = group.DistinctAssetCount > 0
                ? group.DistinctAssetCount.ToString("N0") + " affected asset(s)"
                : group.Count.ToString("N0") + " affected item(s)";
            scroll.Add(MPOUI.Text(breadth, "mpo-detail-path"));

            if (group.Representative != null)
            {
                var why = new VisualElement();
                why.AddToClassList("mpo-simple-section");
                why.Add(MPOUI.Text("Why it matters", "mpo-simple-section-title"));
                why.Add(MPOUI.Text(BuildWhyItMatters(group.Representative), "mpo-simple-section-copy"));
                scroll.Add(why);

                var action = new VisualElement();
                action.AddToClassList("mpo-simple-section");
                action.Add(MPOUI.Text("What you should do", "mpo-simple-section-title"));
                action.Add(BuildRecommendationSection(string.Empty, group.Representative.Recommendation));
                scroll.Add(action);
            }

            var quick = new VisualElement();
            quick.AddToClassList("mpo-group-summary-strip");
            quick.Add(MPOUI.Text(group.Count.ToString("N0"), "mpo-group-summary-value"));
            quick.Add(MPOUI.Text("findings", "mpo-group-summary-label"));
            quick.Add(MPOUI.Text(group.FixableCount.ToString("N0"), "mpo-group-summary-value"));
            quick.Add(MPOUI.Text("fixable", "mpo-group-summary-label"));
            quick.Add(MPOUI.Text(MPOImpactUtility.Label(group.HighestImpact), "mpo-group-summary-value"));
            quick.Add(MPOUI.Text("highest impact", "mpo-group-summary-label"));
            scroll.Add(quick);

            var technical = new Foldout { text = "Show technical details and sample assets", value = false };
            technical.AddToClassList("mpo-technical-foldout");
            if (group.Representative != null)
                technical.Add(BuildStructuredDetailSection("CURRENT VALUES", group.Representative.Description));

            var impact = new VisualElement();
            impact.AddToClassList("mpo-action-row");
            MPOUI.AddImpactChip(impact, "CPU", group.CpuImpact);
            MPOUI.AddImpactChip(impact, "GPU", group.GpuImpact);
            MPOUI.AddImpactChip(impact, "MEM", group.MemoryImpact);
            MPOUI.AddImpactChip(impact, "BUILD", group.BuildSizeImpact);
            MPOUI.AddImpactChip(impact, "THERMAL", group.ThermalImpact);
            technical.Add(impact);

            List<string> paths = group.Issues
                .Select(x => x.AssetPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
            foreach (string path in paths)
            {
                Label pathLabel = MPOUI.Text("•  " + CompactPath(path, 90), "mpo-group-path");
                pathLabel.tooltip = path;
                technical.Add(pathLabel);
            }
            technical.Add(MPOUI.Text("Rule  " + group.RuleId + "  •  Priority " + group.PriorityScore + "  •  Static penalty " + group.TotalPenalty, "mpo-small"));
            scroll.Add(technical);
            card.Add(scroll);

            var actions = new VisualElement();
            actions.AddToClassList("mpo-action-row");
            var assets = MPOUI.ActionButton(group.DistinctAssetCount > 0 ? "Review " + group.DistinctAssetCount.ToString("N0") + " Assets" : "Review Items", () => ReviewIssueGroup(group));
            assets.AddToClassList("mpo-primary");
            actions.Add(assets);

            if (group.SafeFixCount > 0)
            {
                var safeButton = MPOUI.ActionButton("Fix Safe Items", () => FixSafeIssues(group.Issues));
                safeButton.AddToClassList("mpo-safe-action");
                actions.Add(safeButton);
            }

            if (group.FixableCount - group.SafeFixCount > 0)
                actions.Add(MPOUI.ActionButton("Review Fixes", () => ReviewRecommendedFixes(group.Issues)));

            if (group.Representative != null)
                actions.Add(MPOUI.ActionButton("Ignore…", () => ShowIgnoreMenu(group.Representative)));
            card.Add(actions);
            return card;
        }

        private VisualElement BuildIssueDetailCard(MPOIssue issue, bool fixContext)
        {
            var card = MPOUI.Card("mpo-detail-card");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("mpo-detail-scroll");

            var head = new VisualElement();
            head.AddToClassList("mpo-detail-head");
            head.Add(MPOUI.Badge(SeverityLabel(issue.Severity), MPOUI.SeverityClass(issue.Severity)));
            head.Add(MPOUI.Badge(issue.Category.ToString().ToUpperInvariant(), "mpo-impact-none"));
            if (issue.CanFix)
                head.Add(MPOUI.Badge(issue.FixSafety == MPOFixSafety.Safe ? "SAFE FIX" : "REVIEW BEFORE FIX", issue.FixSafety == MPOFixSafety.Safe ? "mpo-impact-low" : "mpo-impact-medium"));
            scroll.Add(head);

            scroll.Add(MPOUI.Text(issue.Title, "mpo-detail-title"));
            if (!string.IsNullOrWhiteSpace(issue.AssetPath))
            {
                Label path = MPOUI.Text(CompactPath(issue.AssetPath, 105), "mpo-detail-path");
                path.tooltip = issue.AssetPath;
                scroll.Add(path);
            }

            var why = new VisualElement();
            why.AddToClassList("mpo-simple-section");
            why.Add(MPOUI.Text("Why it matters", "mpo-simple-section-title"));
            why.Add(MPOUI.Text(BuildWhyItMatters(issue), "mpo-simple-section-copy"));
            scroll.Add(why);

            var action = new VisualElement();
            action.AddToClassList("mpo-simple-section");
            action.Add(MPOUI.Text("What you should do", "mpo-simple-section-title"));
            action.Add(BuildRecommendationSection(string.Empty, issue.Recommendation));
            scroll.Add(action);

            if (issue.CanFix)
            {
                var suggested = new VisualElement();
                suggested.AddToClassList("mpo-suggested-fix");
                suggested.Add(MPOUI.Text(issue.FixSafety == MPOFixSafety.Safe ? "Safe automatic fix available" : "Recommended fix available", "mpo-suggested-fix-title"));
                string preview = string.IsNullOrWhiteSpace(issue.FixPreview) ? issue.Recommendation : issue.FixPreview;
                suggested.Add(MPOUI.Text(SimplifyPreview(preview), "mpo-suggested-fix-copy"));
                scroll.Add(suggested);
            }

            var technical = new Foldout { text = "Show technical details", value = false };
            technical.AddToClassList("mpo-technical-foldout");
            technical.Add(BuildStructuredDetailSection("CURRENT VALUES", issue.Description));

            var impact = new VisualElement();
            impact.AddToClassList("mpo-action-row");
            MPOUI.AddImpactChip(impact, "CPU", issue.CpuImpact);
            MPOUI.AddImpactChip(impact, "GPU", issue.GpuImpact);
            MPOUI.AddImpactChip(impact, "MEM", issue.MemoryImpact);
            MPOUI.AddImpactChip(impact, "BUILD", issue.BuildSizeImpact);
            MPOUI.AddImpactChip(impact, "THERMAL", issue.ThermalImpact);
            technical.Add(impact);

            MPOConfidenceLevel confidence = MPOConfidenceUtility.Get(issue);
            technical.Add(MPOUI.Text("Rule  " + issue.RuleId + "   •   Confidence  " + MPOConfidenceUtility.Label(confidence) + "   •   Static penalty  " + issue.Penalty, "mpo-small"));
            technical.Add(MPOUI.Text("This is static guidance, not an FPS measurement. Validate important visual or gameplay changes on a representative device.", "mpo-score-explain"));
            scroll.Add(technical);
            card.Add(scroll);

            var actions = new VisualElement();
            actions.AddToClassList("mpo-action-row");
            if (issue.ContextObject != null || !string.IsNullOrWhiteSpace(issue.AssetPath))
                actions.Add(MPOUI.ActionButton("Ping Asset", () => PingIssue(issue)));

            if (issue.CanFix)
            {
                var fix = MPOUI.ActionButton(issue.FixSafety == MPOFixSafety.Safe ? "Apply Safe Fix" : "Review Fix", () => OpenFixPreview(issue));
                fix.AddToClassList("mpo-primary");
                actions.Add(fix);
            }

            if (!fixContext)
                actions.Add(MPOUI.ActionButton("Ignore…", () => ShowIgnoreMenu(issue)));
            card.Add(actions);
            return card;
        }

        private static string BuildWhyItMatters(MPOIssue issue)
        {
            if (issue == null)
                return "This item may be worth reviewing for mobile performance.";

            var reasons = new List<string>();
            if (issue.MemoryImpact == MPOImpactLevel.High) reasons.Add("it can use a lot of memory");
            else if (issue.MemoryImpact == MPOImpactLevel.Medium) reasons.Add("it can increase memory use");
            if (issue.GpuImpact == MPOImpactLevel.High) reasons.Add("it can add significant GPU work");
            else if (issue.GpuImpact == MPOImpactLevel.Medium) reasons.Add("it can add GPU work");
            if (issue.CpuImpact == MPOImpactLevel.High) reasons.Add("it can add significant CPU work");
            else if (issue.CpuImpact == MPOImpactLevel.Medium) reasons.Add("it can add CPU work");
            if (issue.ThermalImpact == MPOImpactLevel.High) reasons.Add("it can increase heat and battery pressure");
            else if (issue.ThermalImpact == MPOImpactLevel.Medium) reasons.Add("it can contribute to heat on longer sessions");
            if (issue.BuildSizeImpact == MPOImpactLevel.High) reasons.Add("it can noticeably increase build size");

            if (reasons.Count == 0)
                return "This is a smaller optimization opportunity. Fix it after higher-priority problems unless it is easy to clean up.";

            string joined = reasons.Count == 1
                ? reasons[0]
                : string.Join(", ", reasons.Take(reasons.Count - 1)) + " and " + reasons[reasons.Count - 1];
            return "On the selected mobile target, " + joined + ".";
        }

        private static string SimplifyPreview(string preview)
        {
            if (string.IsNullOrWhiteSpace(preview))
                return "Preview the change before applying it.";

            string[] lines = preview
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            return string.Join("  •  ", lines.Take(3)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private static VisualElement BuildStructuredDetailSection(string title, string copy)
        {
            var section = new VisualElement();
            section.AddToClassList("mpo-detail-section");
            if (!string.IsNullOrWhiteSpace(title))
                section.Add(MPOUI.Text(title, "mpo-detail-label"));

            string value = string.IsNullOrWhiteSpace(copy) ? "No additional guidance." : copy.Trim();
            string[] lines = value
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            int structuredLines = lines.Count(line =>
            {
                int colon = line.IndexOf(':');
                return colon > 0 && colon <= 34;
            });

            if (lines.Length >= 2 && structuredLines >= 2)
            {
                var table = new VisualElement();
                table.AddToClassList("mpo-detail-table");

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim().TrimStart('•', '-', ' ');
                    int colon = line.IndexOf(':');
                    if (colon > 0 && colon <= 34)
                    {
                        var row = new VisualElement();
                        row.AddToClassList("mpo-detail-kv-row");
                        row.Add(MPOUI.Text(line.Substring(0, colon).Trim(), "mpo-detail-key"));
                        row.Add(MPOUI.Text(line.Substring(colon + 1).Trim(), "mpo-detail-value"));
                        table.Add(row);
                    }
                    else
                    {
                        table.Add(MPOUI.Text(line, "mpo-detail-copy"));
                    }
                }

                section.Add(table);
            }
            else
            {
                section.Add(MPOUI.Text(value, "mpo-detail-copy"));
            }

            return section;
        }

        private static VisualElement BuildRecommendationSection(string title, string copy)
        {
            var section = new VisualElement();
            section.AddToClassList("mpo-detail-section");
            if (!string.IsNullOrWhiteSpace(title))
                section.Add(MPOUI.Text(title, "mpo-detail-label"));

            string value = string.IsNullOrWhiteSpace(copy) ? "No additional guidance." : copy.Trim();
            string[] lines = value
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1)
            {
                section.Add(MPOUI.Text(value, "mpo-detail-copy"));
                return section;
            }

            int shown = 0;
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim().TrimStart('•', '-', ' ');
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var bullet = new VisualElement();
                bullet.AddToClassList("mpo-recommendation-row");
                bullet.Add(MPOUI.Text("•", "mpo-recommendation-bullet"));
                bullet.Add(MPOUI.Text(line, "mpo-detail-copy"));
                section.Add(bullet);

                shown++;
                if (shown >= 5)
                    break;
            }

            if (lines.Length > shown)
                section.Add(MPOUI.Text("+ " + (lines.Length - shown) + " more note(s) in technical guidance", "mpo-small"));
            return section;
        }

        private VisualElement BuildFixesPage()
        {
            var page = new VisualElement();
            page.AddToClassList("mpo-page");
            page.Add(BuildPageHeader("Fixes", "Apply safe fixes automatically. Review quality-changing fixes before applying them. Manual problems are never changed for you."));
            AddStatusBanners(page);

            if (_scanResult == null)
            {
                page.Add(MPOUI.EmptyState("Nothing to fix yet", "Run a scan first. Safe and review-required fixes will appear here."));
                return page;
            }

            EnsureActiveIssueCache();
            var stats = new VisualElement();
            stats.AddToClassList("mpo-card-row");
            stats.Add(BuildFixActionCard("SAFE FIXES", _cachedSafeFixCount, "Low-risk and fully revertible.", "Fix All Safe Issues", FixAllSafeIssues, _cachedSafeFixCount > 0, "mpo-fix-safe-card"));
            stats.Add(BuildFixActionCard("RECOMMENDED FIXES", _cachedReviewFixCount, "Texture sizing, GPU Instancing and other changes that need one preview.", "Fix All Recommended...", () => OpenAutomaticFixes(_activeIssueCache.Where(issue => issue != null && issue.FixSafety == MPOFixSafety.ReviewRequired)), _cachedReviewFixCount > 0, "mpo-fix-review-card"));
            stats.Add(BuildFixActionCard("MANUAL", _cachedManualCount, "Needs a human decision. The tool will not auto-change these.", "View Problems", () => SetPage(Page.Issues), _cachedManualCount > 0, "mpo-fix-manual-card"));
            page.Add(stats);

            var utilityRow = new VisualElement();
            utilityRow.AddToClassList("mpo-card-row");
            utilityRow.Add(BuildRevertCard());
            utilityRow.Add(BuildIgnoreCard());
            page.Add(utilityRow);

            page.Add(MPOUI.SectionHeader("Individual recommended fixes", "Use this list when you want to review one asset at a time."));
            var split = new VisualElement();
            split.AddToClassList("mpo-split");

            var listPane = new VisualElement();
            listPane.AddToClassList("mpo-list-pane");
            _fixList = new ListView();
            _fixList.AddToClassList("mpo-list");
            _fixList.selectionType = SelectionType.Single;
            _fixList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _fixList.fixedItemHeight = 72f;
            _fixList.makeItem = MakeFixRow;
            _fixList.bindItem = BindFixRow;
            _fixList.unbindItem = (element, index) => { };
            _fixList.selectedIndicesChanged += OnFixSelectionChanged;
            listPane.Add(_fixList);
            split.Add(listPane);

            _fixDetailHost = new VisualElement();
            _fixDetailHost.AddToClassList("mpo-detail-pane");
            split.Add(_fixDetailHost);
            page.Add(split);

            RefreshFixList();
            return page;
        }

        private VisualElement BuildFixActionCard(string title, int value, string copy, string actionLabel, Action action, bool enabled, string styleClass)
        {
            var card = MPOUI.Card("mpo-fix-stat");
            if (!string.IsNullOrWhiteSpace(styleClass)) card.AddToClassList(styleClass);
            card.Add(MPOUI.Text(title, "mpo-stat-kicker"));
            card.Add(MPOUI.Text(value.ToString("N0"), "mpo-stat-value"));
            card.Add(MPOUI.Text(copy, "mpo-stat-copy"));
            var button = MPOUI.ActionButton(actionLabel, action);
            button.SetEnabled(enabled);
            button.style.marginTop = 12f;
            if (title == "SAFE FIXES") button.AddToClassList("mpo-safe-action");
            else if (title == "RECOMMENDED FIXES") button.AddToClassList("mpo-primary");
            card.Add(button);
            return card;
        }

        private VisualElement BuildRevertCard()
        {
            var card = MPOUI.Card("mpo-report-card");
            card.Add(MPOUI.Text("REVERT SESSION", "mpo-stat-kicker"));
            if (MPOFixSession.HasRevertableSession)
            {
                card.Add(MPOUI.Text(MPOFixSession.ChangeCount + " captured change(s)", "mpo-next-title"));
                card.Add(MPOUI.Text("Started " + FormatUtcDate(MPOFixSession.StartedAtUtc), "mpo-stat-copy"));
                var button = MPOUI.ActionButton("Revert Last Session", RevertLastSession);
                button.AddToClassList("mpo-danger");
                button.style.marginTop = 10f;
                card.Add(button);
            }
            else
            {
                card.Add(MPOUI.Text("No revertable session", "mpo-next-title"));
                card.Add(MPOUI.Text("Automatic fixes snapshot original values before they change.", "mpo-stat-copy"));
            }
            return card;
        }

        private VisualElement BuildIgnoreCard()
        {
            var card = MPOUI.Card("mpo-report-card");
            card.Add(MPOUI.Text("IGNORED FINDINGS", "mpo-stat-kicker"));
            card.Add(MPOUI.Text(MPOIgnoreStore.TotalCount.ToString("N0"), "mpo-next-title"));
            card.Add(MPOUI.Text("Rules " + MPOIgnoreStore.RuleCount + "  •  Assets " + MPOIgnoreStore.AssetCount + "  •  Folders " + MPOIgnoreStore.FolderCount + "  •  Once " + MPOIgnoreStore.OnceCount, "mpo-stat-copy"));
            var row = new VisualElement();
            row.AddToClassList("mpo-action-row");
            var manage = MPOUI.ActionButton("Manage Ignored", () => MPOIgnoredItemsWindow.ShowFor(() =>
            {
                InvalidateIssueCache();
                RefreshAllData();
            }));
            manage.SetEnabled(MPOIgnoreStore.PersistentCount > 0);
            row.Add(manage);
            var clearOnce = MPOUI.ActionButton("Clear Once", () =>
            {
                MPOIgnoreStore.ClearOnce();
                InvalidateIssueCache();
                RefreshAllData();
            });
            clearOnce.SetEnabled(MPOIgnoreStore.OnceCount > 0);
            row.Add(clearOnce);
            var clearAll = MPOUI.ActionButton("Clear All", ClearAllIgnored);
            clearAll.SetEnabled(MPOIgnoreStore.TotalCount > 0);
            row.Add(clearAll);
            card.Add(row);
            return card;
        }

        private VisualElement MakeFixRow()
        {
            var row = new VisualElement();
            row.AddToClassList("mpo-fix-list-row");
            var title = MPOUI.Text(string.Empty, "mpo-fix-title");
            title.name = "title";
            var meta = MPOUI.Text(string.Empty, "mpo-fix-meta");
            meta.name = "meta";
            row.Add(title);
            row.Add(meta);
            return row;
        }

        private void BindFixRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _fixableIssueCache.Count)
                return;

            MPOIssue issue = _fixableIssueCache[index];
            element.Q<Label>("title").text = issue.Title;
            element.Q<Label>("meta").text = (issue.FixSafety == MPOFixSafety.Safe ? "SAFE FIX" : "REVIEW") + "  •  " + issue.Category + "  •  " + SeverityLabel(issue.Severity) +
                                                 (string.IsNullOrWhiteSpace(issue.AssetPath) ? string.Empty : "  •  " + ShortAssetName(issue.AssetPath));
        }

        private void OnFixSelectionChanged(IEnumerable<int> indices)
        {
            foreach (int index in indices)
            {
                if (index >= 0 && index < _fixableIssueCache.Count)
                {
                    _selectedFixIssue = _fixableIssueCache[index];
                    RefreshFixDetail();
                }
                break;
            }
        }

        private void RefreshFixList()
        {
            if (_fixList == null)
                return;
            List<MPOIssue> fixable = GetFixableIssuesCached();
            _fixList.itemsSource = fixable;
            _fixList.Rebuild();
            if (_selectedFixIssue == null || !fixable.Contains(_selectedFixIssue))
                _selectedFixIssue = fixable.FirstOrDefault();
            RefreshFixDetail();
        }

        private void RefreshFixDetail()
        {
            if (_fixDetailHost == null)
                return;
            _fixDetailHost.Clear();
            if (_selectedFixIssue == null)
            {
                _fixDetailHost.Add(MPOUI.EmptyState("No automatic fixes available", "Manual recommendations remain visible on the Findings page."));
                return;
            }
            _fixDetailHost.Add(BuildIssueDetailCard(_selectedFixIssue, true));
        }

        private VisualElement BuildReportsPage()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("mpo-scroll-page");
            scroll.contentContainer.AddToClassList("mpo-scroll-content");
            scroll.Add(BuildPageHeader("Reports", "Export a clear report and compare progress across repeated scans."));
            AddStatusBanners(scroll);

            MPOScanSnapshotData current = GetLiveSnapshot();
            if (current == null)
            {
                scroll.Add(MPOUI.EmptyState("No report data yet", "Complete a scan before exporting or comparing project health."));
                return scroll;
            }

            scroll.Add(MPOUI.SectionHeader("Export current scan", "Reports include metadata, category scores, findings, recommendations, impact estimates and fix safety."));
            var exportRow = new VisualElement();
            exportRow.AddToClassList("mpo-card-row");
            exportRow.Add(BuildExportCard("HTML REPORT", "Human-readable shareable report", "Export HTML", () => ExportReport("html", current)));
            exportRow.Add(BuildExportCard("JSON DATA", "Structured data for tools and CI", "Export JSON", () => ExportReport("json", current)));
            exportRow.Add(BuildExportCard("CSV DATA", "Spreadsheet-friendly issue export", "Export CSV", () => ExportReport("csv", current)));
            scroll.Add(exportRow);

            if (!string.IsNullOrWhiteSpace(_lastExportPath))
            {
                var last = MPOUI.Card();
                last.Add(MPOUI.Text("LAST EXPORT", "mpo-stat-kicker"));
                last.Add(MPOUI.Text(_lastExportPath, "mpo-detail-path"));
                last.Add(MPOUI.ActionButton("Reveal in Finder", () => EditorUtility.RevealInFinder(_lastExportPath)));
                scroll.Add(last);
            }

            scroll.Add(MPOUI.SectionHeader("Before / after", "Comparison uses the previous completed scan with the same platform, device tier and scan scope."));
            scroll.Add(BuildComparisonCard(current));

            scroll.Add(MPOUI.SectionHeader("Recent scan history", "Up to 25 completed scans are stored locally under the project Library folder."));
            scroll.Add(BuildHistoryCard(current));
            return scroll;
        }

        private VisualElement BuildExportCard(string title, string copy, string buttonText, Action action)
        {
            var card = MPOUI.Card("mpo-report-card");
            card.Add(MPOUI.Text(title, "mpo-stat-kicker"));
            card.Add(MPOUI.Text(copy, "mpo-next-title"));
            var button = MPOUI.ActionButton(buttonText, action);
            button.AddToClassList("mpo-primary");
            button.SetEnabled(!_needsRescan);
            button.style.marginTop = 12f;
            card.Add(button);
            if (_needsRescan)
                card.Add(MPOUI.Text("Re-scan required before export", "mpo-stat-copy"));
            return card;
        }

        private void ExportReport(string type, MPOScanSnapshotData current)
        {
            bool success = false;
            string path = string.Empty;
            if (string.Equals(type, "html", StringComparison.OrdinalIgnoreCase))
                success = MPOReportExporter.ExportHtml(current, out path);
            else if (string.Equals(type, "json", StringComparison.OrdinalIgnoreCase))
                success = MPOReportExporter.ExportJson(current, out path);
            else if (string.Equals(type, "csv", StringComparison.OrdinalIgnoreCase))
                success = MPOReportExporter.ExportCsv(current, out path);

            if (success)
                OnReportExported(path);
        }

        private VisualElement BuildComparisonCard(MPOScanSnapshotData current)
        {
            var card = MPOUI.Card("mpo-no-right-margin");
            MPOScanSnapshotData previous = MPOScanHistoryStore.GetPreviousComparable(current);
            if (previous == null)
            {
                card.Add(MPOUI.Text("Run another scan after making changes to unlock comparison.", "mpo-copy"));
                return card;
            }

            MPOScanComparison comparison = MPOScanComparison.Create(previous, current);
            if (comparison == null)
                return card;

            var row = new VisualElement();
            row.AddToClassList("mpo-card-row");
            row.Add(BuildStatCard("BEFORE", previous.overallScore, FormatSnapshotDate(previous)));
            row.Add(BuildStatCard("CURRENT", current.overallScore, FormatSnapshotDate(current)));
            row.Add(BuildStatCard("SCORE CHANGE", comparison.ScoreDelta, "Higher is better"));
            row.Add(BuildStatCard("RESOLVED", comparison.ResolvedIssueCount, "No longer active"));
            row.Add(BuildStatCard("NEW", comparison.NewIssueCount, "New active findings"));
            card.Add(row);
            card.Add(MPOUI.Text("Critical change " + Signed(comparison.CriticalDelta) + "  •  Warning change " + Signed(comparison.WarningDelta) + "  •  Suggestion change " + Signed(comparison.SuggestionDelta), "mpo-small"));
            return card;
        }

        private VisualElement BuildHistoryCard(MPOScanSnapshotData current)
        {
            var card = MPOUI.Card("mpo-no-right-margin");
            var header = new VisualElement();
            header.AddToClassList("mpo-row");
            header.Add(MPOUI.Text(MPOScanHistoryStore.Count + " saved scan(s)", "mpo-bold"));
            var spacer = new VisualElement();
            spacer.AddToClassList("mpo-flex");
            header.Add(spacer);
            var clear = MPOUI.ActionButton("Clear History", ClearHistory, "mpo-button");
            clear.SetEnabled(MPOScanHistoryStore.Count > 0);
            header.Add(clear);
            card.Add(header);

            List<MPOScanSnapshotData> recent = MPOScanHistoryStore.GetRecent(10);
            if (recent.Count == 0)
            {
                card.Add(MPOUI.Text("No completed scans are stored yet.", "mpo-copy"));
                return card;
            }

            foreach (MPOScanSnapshotData scan in recent)
            {
                var row = new VisualElement();
                row.AddToClassList("mpo-history-row");
                bool isCurrent = !string.IsNullOrWhiteSpace(current.id) && current.id == scan.id;
                row.Add(MPOUI.Badge(isCurrent ? "CURRENT" : "SCAN", isCurrent ? "mpo-impact-low" : "mpo-impact-none"));
                row.Add(MPOUI.Text(FormatSnapshotDate(scan), "mpo-history-date"));
                row.Add(MPOUI.Text(scan.targetPlatformName + " / " + scan.deviceTierName + "  •  " + (string.IsNullOrWhiteSpace(scan.scanScopeName) ? "Full Project" : scan.scanScopeName), "mpo-history-target"));
                row.Add(MPOUI.Text(scan.overallScore + "/100", "mpo-history-score"));
                row.Add(MPOUI.Text("C " + scan.criticalCount + "   W " + scan.warningCount + "   S " + scan.suggestionCount, "mpo-history-issues"));
                card.Add(row);
            }
            return card;
        }

        private void RunScan()
        {
            if (MPOScanRunner.IsRunning || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            try
            {
                MPOIgnoreStore.ClearOnce();
                InvalidateIssueCache();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Mobile Performance Optimizer] Could not clear Ignore Once state. The scan will continue.\n" + exception);
            }

            MPOProfile profile;
            try
            {
                profile = MPOProfile.Create(_targetPlatform, _deviceTier);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Mobile Performance Optimizer", "The selected scan profile could not be created.\n\n" + exception.Message, "OK");
                return;
            }

            if (!MPOScanScope.TryCreate(_scanScopeMode, _selectedFolderPath, out MPOScanScope scope, out string scopeError))
            {
                EditorUtility.DisplayDialog("Mobile Performance Optimizer", scopeError, "OK");
                return;
            }

            _scanScope = scope;
            _needsRescan = _scanResult != null;
            _currentHistoryId = string.Empty;
            bool started = MPOScanRunner.Start(profile, scope, OnScanCompleted, UpdateScanUi);
            if (!started)
            {
                EditorUtility.DisplayDialog("Mobile Performance Optimizer", "A scan is already running or the scan profile is invalid.", "OK");
                return;
            }

            _page = Page.Overview;
            UpdateNavState();
            RenderCurrentPage();
            UpdateScanUi();
        }

        private void UpdateScanUi()
        {
            if (_scanBanner == null)
                return;

            bool running = MPOScanRunner.IsRunning;
            _scanBanner.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
            if (_platformField != null) _platformField.SetEnabled(!running);
            if (_tierField != null) _tierField.SetEnabled(!running);
            foreach (Button button in _platformTabs.Values) button?.SetEnabled(!running);
            foreach (Button button in _tierTabs.Values) button?.SetEnabled(!running);
            foreach (Button button in _scopeTabs.Values) button?.SetEnabled(!running);
            if (_folderField != null) _folderField.SetEnabled(!running);
            if (_scanButton != null)
            {
                _scanButton.SetEnabled(!running && !EditorApplication.isPlayingOrWillChangePlaymode);
                _scanButton.text = running ? "Scanning…" : (_scanResult == null ? "Start Scan" : "Start New Scan");
            }

            if (!running)
                return;

            float percent = Mathf.Clamp01(MPOScanRunner.Progress) * 100f;
            _scanProgress.value = percent;
            _scanProgress.title = Mathf.RoundToInt(percent) + "%";
            _scanTitle.text = "Scanning  " + MPOScanRunner.CurrentAnalyzerNumber + "/" + MPOScanRunner.AnalyzerCount + "  •  " + MPOScanRunner.CurrentAnalyzerName;
            _scanStatus.text = MPOScanRunner.StatusMessage;
        }

        private void OnScanCompleted(MPOScanResult completed)
        {
            if (completed == null)
                return;

            _scanResult = completed;
            _scanPlatform = _targetPlatform;
            _scanTier = _deviceTier;
            _needsRescan = false;
            _page = Page.Overview;
            _selectedIssue = null;
            _selectedIssueGroup = null;
            _selectedFixIssue = null;
            _groupFilterKey = string.Empty;
            InvalidateIssueCache();

            try
            {
                MPOScanSnapshotData snapshot = MPOReportSnapshotBuilder.Build(_scanResult, _scanPlatform, _scanTier, null, _scanScope);
                _currentHistoryId = snapshot != null ? snapshot.id : string.Empty;
                if (!_scanResult.WasCancelled && snapshot != null)
                    MPOScanHistoryStore.Add(snapshot);
            }
            catch (Exception exception)
            {
                _scanResult.AddRecoverableWarning("Reports / History", "Post-scan snapshot", exception);
                Debug.LogWarning("[Mobile Performance Optimizer] Scan completed, but history/report snapshot creation failed. Results remain available.\n" + exception);
            }

            RefreshAllData();
        }

        private void RefreshAllData()
        {
            InvalidateIssueCache();
            EnsureActiveIssueCache();
            RefreshSidebar();
            UpdateScanUi();
            RenderCurrentPage();
        }

        private List<MPOIssue> GetVisibleIssuesCached()
        {
            EnsureActiveIssueCache();
            string key = _cacheVersion + "|" + _showCritical + "|" + _showWarnings + "|" + _showSuggestions + "|" +
                         _showOnlyFixable + "|" + _showOnlyHighImpact + "|" + _categoryFilterIndex + "|" +
                         (int)_sortMode + "|" + (_search ?? string.Empty) + "|" + (_groupFilterKey ?? string.Empty);

            if (string.Equals(key, _visibleIssueCacheKey, StringComparison.Ordinal))
                return _visibleIssueCache;

            IEnumerable<MPOIssue> filtered = _activeIssueCache
                .Where(IsSeverityVisible)
                .Where(IsCategoryVisible)
                .Where(IsSearchMatch)
                .Where(x => string.IsNullOrWhiteSpace(_groupFilterKey) || string.Equals(MPOIssueGroup.GetKey(x), _groupFilterKey, StringComparison.Ordinal))
                .Where(x => !_showOnlyFixable || x.CanFix)
                .Where(x => !_showOnlyHighImpact || x.HighestImpact == MPOImpactLevel.High);

            switch (_sortMode)
            {
                case IssueSortMode.EstimatedImpact:
                    filtered = filtered.OrderByDescending(x => x.ImpactScore).ThenByDescending(x => x.HighestImpact).ThenByDescending(x => x.Severity).ThenByDescending(x => x.Penalty).ThenBy(x => x.Title);
                    break;
                case IssueSortMode.Category:
                    filtered = filtered.OrderBy(x => x.Category).ThenByDescending(x => x.Severity).ThenByDescending(x => x.ImpactScore).ThenBy(x => x.Title);
                    break;
                case IssueSortMode.AssetName:
                    filtered = filtered.OrderBy(x => string.IsNullOrWhiteSpace(x.AssetPath) ? "~" : x.AssetPath).ThenBy(x => x.Title).ThenByDescending(x => x.Severity);
                    break;
                default:
                    filtered = filtered.OrderByDescending(x => x.Severity).ThenByDescending(x => x.ImpactScore).ThenByDescending(x => x.Penalty).ThenBy(x => x.Category).ThenBy(x => x.Title);
                    break;
            }

            _visibleIssueCache = filtered.ToList();
            _visibleIssueCacheKey = key;
            return _visibleIssueCache;
        }

        private List<MPOIssueGroup> GetVisibleGroupsCached()
        {
            List<MPOIssue> visible = GetVisibleIssuesCached();
            string key = _visibleIssueCacheKey + "|groups|" + (int)_sortMode;
            if (string.Equals(key, _visibleGroupCacheKey, StringComparison.Ordinal))
                return _visibleGroupCache;

            IEnumerable<MPOIssueGroup> groups = MPOIssueGroup.Build(visible);
            switch (_sortMode)
            {
                case IssueSortMode.EstimatedImpact:
                    groups = groups.OrderByDescending(x => x.HighestImpact)
                        .ThenByDescending(x => x.PriorityScore)
                        .ThenByDescending(x => x.Count)
                        .ThenBy(x => x.Title);
                    break;
                case IssueSortMode.Category:
                    groups = groups.OrderBy(x => x.Category)
                        .ThenByDescending(x => x.Severity)
                        .ThenByDescending(x => x.PriorityScore)
                        .ThenBy(x => x.Title);
                    break;
                case IssueSortMode.AssetName:
                    groups = groups.OrderBy(x => x.Title)
                        .ThenByDescending(x => x.Severity)
                        .ThenByDescending(x => x.Count);
                    break;
                default:
                    groups = groups.OrderByDescending(x => x.PriorityScore)
                        .ThenByDescending(x => x.Severity)
                        .ThenByDescending(x => x.HighestImpact)
                        .ThenByDescending(x => x.Count)
                        .ThenBy(x => x.Title);
                    break;
            }

            _visibleGroupCache = groups.ToList();
            _visibleGroupCacheKey = key;
            return _visibleGroupCache;
        }

        private List<MPOIssue> GetFixableIssuesCached()
        {
            EnsureActiveIssueCache();
            if (_fixableCacheVersion == _cacheVersion)
                return _fixableIssueCache;

            _fixableIssueCache = _activeIssueCache
                .Where(x => x.CanFix)
                .OrderByDescending(x => x.FixSafety)
                .ThenByDescending(x => x.Severity)
                .ThenByDescending(x => x.ImpactScore)
                .ThenBy(x => x.Title)
                .ToList();
            _fixableCacheVersion = _cacheVersion;
            return _fixableIssueCache;
        }

        private void EnsureActiveIssueCache()
        {
            if (!_activeIssueCacheDirty)
                return;

            _activeIssueCache.Clear();
            _activeCategoryCounts.Clear();
            if (_scanResult != null)
            {
                foreach (MPOIssue issue in _scanResult.AllIssues)
                {
                    if (issue == null || MPOIgnoreStore.IsIgnored(issue))
                        continue;
                    _activeIssueCache.Add(issue);
                    _activeCategoryCounts.TryGetValue(issue.Category, out int count);
                    _activeCategoryCounts[issue.Category] = count + 1;
                }

                _cachedOverallScore = MPOScoreCalculator.CalculateOverall(_scanResult);
                _cachedCriticalCount = _activeIssueCache.Count(x => x.Severity == MPOSeverity.Critical);
                _cachedWarningCount = _activeIssueCache.Count(x => x.Severity == MPOSeverity.Warning);
                _cachedSuggestionCount = _activeIssueCache.Count(x => x.Severity == MPOSeverity.Suggestion);
                _cachedFixableCount = _activeIssueCache.Count(x => x.CanFix);
                _cachedIgnoredCount = Math.Max(0, _scanResult.AllIssues.Count() - _activeIssueCache.Count);
                _cachedSafeFixCount = _activeIssueCache.Count(x => x.CanFix && x.FixSafety == MPOFixSafety.Safe);
                _cachedReviewFixCount = _activeIssueCache.Count(x => x.CanFix && x.FixSafety == MPOFixSafety.ReviewRequired);
                _cachedManualCount = _activeIssueCache.Count(x => !x.CanFix);
            }
            else
            {
                _cachedOverallScore = 100;
                _cachedCriticalCount = _cachedWarningCount = _cachedSuggestionCount = 0;
                _cachedFixableCount = _cachedIgnoredCount = 0;
                _cachedSafeFixCount = _cachedReviewFixCount = _cachedManualCount = 0;
            }

            _activeIssueCacheDirty = false;
            _cacheVersion++;
            _visibleIssueCacheKey = string.Empty;
            _visibleGroupCacheKey = string.Empty;
            _fixableCacheVersion = -1;
        }

        private void InvalidateIssueCache()
        {
            _activeIssueCacheDirty = true;
            _visibleIssueCacheKey = string.Empty;
            _visibleGroupCacheKey = string.Empty;
            _fixableCacheVersion = -1;
            _liveSnapshotCache = null;
            _liveSnapshotCacheVersion = -1;
        }

        private MPOScanSnapshotData GetLiveSnapshot()
        {
            if (_scanResult == null)
                return null;
            EnsureActiveIssueCache();
            if (_liveSnapshotCache != null && _liveSnapshotCacheVersion == _cacheVersion)
                return _liveSnapshotCache;

            _liveSnapshotCache = MPOReportSnapshotBuilder.Build(_scanResult, _scanPlatform, _scanTier, _currentHistoryId, _scanScope);
            _liveSnapshotCacheVersion = _cacheVersion;
            return _liveSnapshotCache;
        }

        private void PreviewAllSafeFixes()
        {
            FixAllSafeIssues();
        }

        private void FixAllSafeIssues()
        {
            EnsureActiveIssueCache();
            FixSafeIssues(_activeIssueCache);
        }

        private void FixSafeIssues(IEnumerable<MPOIssue> source)
        {
            if (source == null)
                return;

            var unique = new Dictionary<string, MPOIssue>();
            foreach (MPOIssue issue in source)
            {
                if (issue == null || !issue.CanFix || issue.FixSafety != MPOFixSafety.Safe)
                    continue;
                string key = MPOFixEngine.GetActionKey(issue);
                if (!unique.ContainsKey(key))
                    unique.Add(key, issue);
            }

            if (unique.Count == 0)
            {
                EditorUtility.DisplayDialog("Mobile Performance Optimizer", "No safe automatic fixes are available in the current scan.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Fix All Safe Issues",
                    "Apply " + unique.Count + " safe and revertible fix action(s)?\n\nOnly items classified SAFE are included. Review-required and manual findings will not be changed.",
                    "Fix All Safe",
                    "Cancel"))
                return;

            int applied = 0;
            var failures = new List<string>();
            foreach (MPOIssue issue in unique.Values)
            {
                try
                {
                    if (MPOFixEngine.Apply(issue, out string message))
                        applied++;
                    else
                        failures.Add(issue.Title + ": " + message);
                }
                catch (Exception exception)
                {
                    failures.Add(issue.Title + ": " + exception.Message);
                }
            }

            _needsRescan = applied > 0 || _needsRescan;
            string summary = "Applied " + applied + " safe fix action(s).";
            if (failures.Count > 0)
                summary += "\n\n" + failures.Count + " action(s) could not be applied. The rest were preserved.\n" + string.Join("\n", failures.Take(8));
            if (applied > 0)
                summary += "\n\nRe-scan to refresh the score and problem list. You can revert the session from the Fixes page.";
            EditorUtility.DisplayDialog("Safe Fixes Complete", summary, "OK");
            RefreshAllData();
        }

        private void OpenAutomaticFixes(IEnumerable<MPOIssue> source)
        {
            if (source == null)
                return;

            List<MPOIssue> fixes = source
                .Where(issue => issue != null && issue.CanFix && issue.FixSafety != MPOFixSafety.Manual)
                .ToList();
            if (fixes.Count == 0)
            {
                EditorUtility.DisplayDialog("Mobile Performance Optimizer", "No automatic fixes are available for this selection.", "OK");
                return;
            }

            MPOBatchFixWindow.ShowFor(fixes, () =>
            {
                _needsRescan = true;
                RefreshAllData();
            });
        }

        private void ReviewRecommendedFixes(IEnumerable<MPOIssue> source)
        {
            if (source == null)
                return;
            List<MPOIssue> review = source
                .Where(issue => issue != null && issue.CanFix && issue.FixSafety == MPOFixSafety.ReviewRequired)
                .ToList();
            if (review.Count == 0)
            {
                EditorUtility.DisplayDialog("Mobile Performance Optimizer", "No review-required fixes are available in the current scan.", "OK");
                return;
            }

            MPOBatchFixWindow.ShowFor(review, () =>
            {
                _needsRescan = true;
                RefreshAllData();
            });
        }

        private void OpenFixPreview(MPOIssue issue)
        {
            MPOFixPreviewWindow.ShowFor(issue, () =>
            {
                _needsRescan = true;
                RefreshAllData();
            });
        }

        private static void PingIssue(MPOIssue issue)
        {
            if (issue == null)
                return;
            UnityEngine.Object target = issue.ContextObject;
            if (target == null && !string.IsNullOrEmpty(issue.AssetPath))
                target = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);
            if (target == null)
                return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private void ShowIgnoreMenu(MPOIssue issue)
        {
            if (issue == null)
                return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Ignore once"), false, () =>
            {
                MPOIgnoreStore.IgnoreOnce(issue);
                RefreshAllData();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Ignore this rule"), false, () =>
            {
                MPOIgnoreStore.IgnoreRule(issue.RuleId);
                RefreshAllData();
            });

            if (!string.IsNullOrWhiteSpace(issue.AssetPath))
            {
                menu.AddItem(new GUIContent("Ignore this asset"), false, () =>
                {
                    MPOIgnoreStore.IgnoreAsset(issue.AssetPath);
                    RefreshAllData();
                });
                menu.AddItem(new GUIContent("Ignore this folder"), false, () =>
                {
                    MPOIgnoreStore.IgnoreFolder(issue.AssetPath);
                    RefreshAllData();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Ignore this asset"));
                menu.AddDisabledItem(new GUIContent("Ignore this folder"));
            }
            menu.ShowAsContext();
        }

        private void RevertLastSession()
        {
            if (!MPOFixSession.HasRevertableSession)
                return;
            if (!EditorUtility.DisplayDialog("Revert Fix Session", "Restore all settings/import values captured before the current fix session?", "Revert", "Cancel"))
                return;

            MPOFixSession.RevertLastSession(out string message);
            EditorUtility.DisplayDialog("Mobile Performance Optimizer", message, "OK");
            _needsRescan = true;
            RefreshAllData();
        }

        private void ClearAllIgnored()
        {
            if (MPOIgnoreStore.TotalCount == 0)
                return;
            if (!EditorUtility.DisplayDialog("Clear Ignored Items", "Show all previously ignored rules, assets, folders and one-time findings again?", "Clear", "Cancel"))
                return;
            MPOIgnoreStore.ClearAll();
            RefreshAllData();
        }

        private void ClearHistory()
        {
            if (MPOScanHistoryStore.Count == 0)
                return;
            if (!EditorUtility.DisplayDialog("Clear Scan History", "Delete the stored Mobile Performance Optimizer scan history for this project?", "Clear", "Cancel"))
                return;
            MPOScanHistoryStore.Clear();
            RenderCurrentPage();
        }

        private void OnReportExported(string path)
        {
            _lastExportPath = path ?? string.Empty;
            EditorUtility.DisplayDialog("Report Exported", "Report saved successfully.\n\n" + _lastExportPath, "OK");
            RenderCurrentPage();
        }

        private bool IsSeverityVisible(MPOIssue issue)
        {
            switch (issue.Severity)
            {
                case MPOSeverity.Critical: return _showCritical;
                case MPOSeverity.Warning: return _showWarnings;
                default: return _showSuggestions;
            }
        }

        private bool IsCategoryVisible(MPOIssue issue)
        {
            if (_categoryFilterIndex <= 0)
                return true;
            return (int)issue.Category == _categoryFilterIndex - 1;
        }

        private bool IsSearchMatch(MPOIssue issue)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;
            string query = _search.Trim();
            return Contains(issue.Title, query) || Contains(issue.Description, query) || Contains(issue.Recommendation, query) ||
                   Contains(issue.AssetPath, query) || Contains(issue.Category.ToString(), query) || Contains(issue.RuleId, query);
        }

        private static bool Contains(string source, string query)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string[] BuildCategoryOptions()
        {
            var options = new List<string> { "All categories" };
            foreach (MPOCategory category in Enum.GetValues(typeof(MPOCategory)))
                options.Add(category.ToString());
            return options.ToArray();
        }

        private static string SeverityLabel(MPOSeverity severity)
        {
            switch (severity)
            {
                case MPOSeverity.Critical: return "CRITICAL";
                case MPOSeverity.Warning: return "WARNING";
                default: return "SUGGESTION";
            }
        }

        private static string FixLabel(MPOIssue issue)
        {
            if (issue == null || !issue.CanFix) return "MANUAL";
            return issue.FixSafety == MPOFixSafety.Safe ? "SAFE FIX" : "REVIEW";
        }

        private static string ShortAssetName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }

        private static string CompactPath(string path, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(path) || maxCharacters < 16 || path.Length <= maxCharacters)
                return path ?? string.Empty;

            int keep = Math.Max(6, (maxCharacters - 3) / 2);
            return path.Substring(0, keep) + "..." + path.Substring(path.Length - keep);
        }

        private static string FriendlyTier(MPODeviceTier tier)
        {
            switch (tier)
            {
                case MPODeviceTier.LowEnd: return "Low End";
                case MPODeviceTier.HighEnd: return "High End";
                default: return "Mid Range";
            }
        }

        private static Color SeverityColor(MPOSeverity severity)
        {
            switch (severity)
            {
                case MPOSeverity.Critical: return new Color(0.95f, 0.32f, 0.40f);
                case MPOSeverity.Warning: return new Color(0.91f, 0.70f, 0.28f);
                default: return new Color(0.30f, 0.67f, 0.80f);
            }
        }

        private static Color ScoreColor(int score)
        {
            if (score >= 85) return new Color(0.32f, 0.75f, 0.58f);
            if (score >= 70) return new Color(0.96f, 0.62f, 0.10f);
            if (score >= 50) return new Color(0.91f, 0.69f, 0.28f);
            return new Color(0.94f, 0.32f, 0.40f);
        }

        private static string FormatUtcDate(string utc)
        {
            if (string.IsNullOrWhiteSpace(utc))
                return "unknown time";
            if (DateTime.TryParse(utc, out DateTime parsed))
                return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return utc;
        }

        private static string FormatSnapshotDate(MPOScanSnapshotData snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.completedAtUtc))
                return "Unknown date";
            if (DateTime.TryParse(snapshot.completedAtUtc, out DateTime parsed))
                return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return snapshot.completedAtUtc;
        }

        private static string Signed(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }
    }
}
