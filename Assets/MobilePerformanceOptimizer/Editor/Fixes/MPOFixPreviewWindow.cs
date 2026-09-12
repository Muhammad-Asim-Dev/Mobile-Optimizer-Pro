using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MobilePerformanceOptimizer
{
    public sealed class MPOFixPreviewWindow : EditorWindow
    {
        private MPOIssue _issue;
        private MPOFixPlan _plan;
        private bool _reviewOnly;
        private Action _onApplied;

        public static void ShowFor(MPOIssue issue, Action onApplied)
        {
            if (issue == null || !issue.CanFix)
                return;

            MPOFixPreviewWindow window = CreateInstance<MPOFixPreviewWindow>();
            window._issue = issue;
            window._plan = issue.FixKind == MPOFixKind.DisableDevelopmentBuildFlags ? null : MPOFixPlans.Create(issue);
            window._onApplied = onApplied;
            window.titleContent = new GUIContent("Preview Fix");
            window.minSize = new Vector2(560f, 430f);
            window.maxSize = new Vector2(840f, 760f);
            window.ShowUtility();
        }

        internal static void ReviewPlan(MPOFixPlan plan, Action onReviewed)
        {
            var window = CreateInstance<MPOFixPreviewWindow>();
            window._issue = plan.Issue;
            window._plan = plan.CopyForReview();
            window._reviewOnly = true;
            window._onApplied = () => {
                plan.SetCustom(window._plan.Custom);
                for (int i = 0; i < plan.Settings.Count; i++)
                {
                    plan.Settings[i].Selected = window._plan.Settings[i].Selected;
                    plan.Settings[i].Enabled = window._plan.Settings[i].Enabled;
                }
                onReviewed?.Invoke();
            };
            window.titleContent = new GUIContent("Configure Selected Fix");
            window.minSize = new Vector2(560, 430);
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            if (_issue == null)
            {
                Close();
                return;
            }

            VisualElement root = rootVisualElement;
            root.Clear();
            MPOUI.ApplyTheme(root);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("mpo-scroll-page");
            scroll.contentContainer.AddToClassList("mpo-scroll-content");
            scroll.Add(BuildHeader());
            scroll.Add(BuildPreviewCard());
            root.Add(scroll);

            var footer = new VisualElement();
            footer.AddToClassList("mpo-toolbar");
            footer.style.marginLeft = 16f;
            footer.style.marginRight = 16f;
            footer.style.marginBottom = 14f;

            footer.Add(MPOUI.ActionButton("Cancel", Close));
            var spacer = new VisualElement();
            spacer.AddToClassList("mpo-flex");
            footer.Add(spacer);
            var apply = MPOUI.ActionButton(_reviewOnly ? "Use These Settings" : _issue.FixSafety == MPOFixSafety.Safe ? "Apply Safe Fix" : "Apply Reviewed Fix", ApplyFix);
            apply.AddToClassList("mpo-primary");
            footer.Add(apply);
            root.Add(footer);
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("mpo-page-head");
            var main = new VisualElement();
            main.AddToClassList("mpo-page-head-main");
            main.Add(MPOUI.Text("Preview Fix", "mpo-page-title"));
            main.Add(MPOUI.Text("Review exactly what will change before anything is modified.", "mpo-page-subtitle"));
            header.Add(main);
            return header;
        }

        private VisualElement BuildPreviewCard()
        {
            var card = MPOUI.Card("mpo-no-right-margin");

            var badges = new VisualElement();
            badges.AddToClassList("mpo-row");
            badges.Add(MPOUI.Badge(_issue.FixSafety == MPOFixSafety.Safe ? "SAFE FIX" : "REVIEW REQUIRED", _issue.FixSafety == MPOFixSafety.Safe ? "mpo-impact-low" : "mpo-impact-medium"));
            badges.Add(MPOUI.Badge(_issue.Category.ToString().ToUpperInvariant(), "mpo-impact-none"));
            card.Add(badges);

            card.Add(MPOUI.Text(_issue.Title, "mpo-detail-title"));
            if (!string.IsNullOrWhiteSpace(_issue.AssetPath))
                card.Add(MPOUI.Text(_issue.AssetPath, "mpo-detail-path"));
            else if (_issue.ContextObject != null)
                card.Add(MPOUI.Text(_issue.ContextObject.name + (_issue.ContextObject is Component component ? " — " + component.gameObject.scene.path : ""), "mpo-detail-path"));

            var impacts = new VisualElement();
            impacts.AddToClassList("mpo-action-row");
            MPOUI.AddImpactChip(impacts, "CPU", _issue.CpuImpact);
            MPOUI.AddImpactChip(impacts, "GPU", _issue.GpuImpact);
            MPOUI.AddImpactChip(impacts, "MEM", _issue.MemoryImpact);
            MPOUI.AddImpactChip(impacts, "BUILD", _issue.BuildSizeImpact);
            MPOUI.AddImpactChip(impacts, "THERMAL", _issue.ThermalImpact);
            card.Add(impacts);

            card.Add(MPOFixPlanUI.Build(_plan));
            card.Add(BuildSection(_plan == null ? "WHAT WILL CHANGE" : "RECOMMENDATION CONTEXT", string.IsNullOrWhiteSpace(_issue.FixPreview) ? _issue.Recommendation : _issue.FixPreview));
            card.Add(BuildSection("WHY THIS IS " + (_issue.FixSafety == MPOFixSafety.Safe ? "SAFE" : "REVIEW-REQUIRED"),
                _issue.FixSafety == MPOFixSafety.Safe
                    ? "This changes an editor/import setting that is straightforward to restore. The original value is captured in the current optimization session before the change is applied."
                    : "This setting can affect runtime behavior or visual/audio quality. Apply only after confirming the project does not rely on the current value. The original value is still captured for revert."));

            return card;
        }

        private static VisualElement BuildSection(string title, string copy)
        {
            var section = new VisualElement();
            section.AddToClassList("mpo-detail-section");
            section.Add(MPOUI.Text(title, "mpo-simple-section-title"));
            section.Add(MPOUI.Text(copy, "mpo-simple-section-copy"));
            return section;
        }

        private void ApplyFix()
        {
            if (_issue == null)
                return;

            if (_reviewOnly) { _onApplied?.Invoke(); Close(); return; }
            string message;
            bool applied = _plan == null ? MPOFixEngine.Apply(_issue, out message) :
                MPOFixPlans.Apply(_plan, out message) == MPOApplyStatus.Applied;
            if (applied)
            {
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Mobile Performance Optimizer", message, "OK");
                _onApplied?.Invoke();
                Close();
            }
            else
            {
                EditorUtility.DisplayDialog("Fix Could Not Be Applied", message, "OK");
            }
        }
    }
}
