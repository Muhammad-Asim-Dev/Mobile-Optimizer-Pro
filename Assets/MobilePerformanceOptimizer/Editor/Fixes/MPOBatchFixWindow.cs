using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MobilePerformanceOptimizer
{
    public sealed class MPOBatchFixWindow : EditorWindow
    {
        private sealed class BatchFixItem
        {
            public MPOIssue Issue;
            public bool Selected = true;
        }

        private readonly List<BatchFixItem> _items = new List<BatchFixItem>();
        private Action _onApplied;
        private ListView _list;
        private Label _selectionLabel;
        private Button _applyButton;
        private bool _containsReviewFixes;
        private readonly List<BatchFixItem> _applyQueue = new List<BatchFixItem>();
        private readonly List<string> _applyFailures = new List<string>();
        private int _applyIndex;
        private int _appliedCount;
        private bool _isApplying;
        private ProgressBar _applyProgress;
        private Label _applyStatus;

        public static void ShowFor(IEnumerable<MPOIssue> issues, Action onApplied)
        {
            if (issues == null)
                return;

            var unique = new Dictionary<string, MPOIssue>();
            foreach (MPOIssue issue in issues)
            {
                if (issue == null || !issue.CanFix)
                    continue;
                string key = MPOFixEngine.GetActionKey(issue);
                if (!unique.ContainsKey(key))
                    unique.Add(key, issue);
            }

            if (unique.Count == 0)
                return;

            MPOBatchFixWindow window = CreateInstance<MPOBatchFixWindow>();
            window._onApplied = onApplied;
            foreach (MPOIssue issue in unique.Values.OrderBy(item => item.FixSafety).ThenByDescending(item => item.Severity).ThenBy(item => item.Title))
            {
                window._items.Add(new BatchFixItem { Issue = issue, Selected = true });
                if (issue.FixSafety == MPOFixSafety.ReviewRequired)
                    window._containsReviewFixes = true;
            }

            window.titleContent = new GUIContent(window._containsReviewFixes ? "Review Recommended Fixes" : "Safe Fixes");
            window.minSize = new Vector2(720f, 500f);
            window.maxSize = new Vector2(1100f, 850f);
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            MPOUI.ApplyTheme(root);

            var head = new VisualElement();
            head.AddToClassList("mpo-page");
            head.style.flexGrow = 0f;
            head.style.paddingBottom = 8f;
            head.Add(MPOUI.Text(_containsReviewFixes ? "Review Recommended Fixes" : "Safe Fixes", "mpo-page-title"));
            head.Add(MPOUI.Text(_containsReviewFixes
                ? "Review each proposed change, uncheck anything you do not want, then apply the selected fixes. Original values are saved for Revert."
                : "These changes are classified safe and revertible. Uncheck anything you want to leave unchanged.", "mpo-page-subtitle"));

            var toolbar = new VisualElement();
            toolbar.AddToClassList("mpo-toolbar");
            toolbar.style.marginTop = 14f;
            toolbar.Add(MPOUI.ActionButton("Select All", () => SetAll(true)));
            toolbar.Add(MPOUI.ActionButton("Select None", () => SetAll(false)));
            var spacer = new VisualElement();
            spacer.AddToClassList("mpo-flex");
            toolbar.Add(spacer);
            _selectionLabel = MPOUI.Text(string.Empty, "mpo-bold");
            toolbar.Add(_selectionLabel);
            head.Add(toolbar);
            root.Add(head);

            _list = new ListView();
            _list.AddToClassList("mpo-list");
            _list.style.marginLeft = 20f;
            _list.style.marginRight = 20f;
            _list.style.flexGrow = 1f;
            _list.selectionType = SelectionType.None;
            _list.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _list.fixedItemHeight = 82f;
            _list.itemsSource = _items;
            _list.makeItem = MakeItem;
            _list.bindItem = BindItem;
            _list.unbindItem = (element, index) => { };
            root.Add(_list);

            var footer = new VisualElement();
            footer.AddToClassList("mpo-toolbar");
            footer.style.marginLeft = 20f;
            footer.style.marginRight = 20f;
            footer.style.marginTop = 10f;
            footer.style.marginBottom = 16f;
            footer.Add(MPOUI.ActionButton("Cancel", Close));
            var footerSpacer = new VisualElement();
            footerSpacer.AddToClassList("mpo-flex");
            footer.Add(footerSpacer);
            _applyButton = MPOUI.ActionButton(_containsReviewFixes ? "Apply Selected Fixes" : "Apply Selected Safe Fixes", ApplySelected);
            _applyButton.AddToClassList("mpo-primary");
            footer.Add(_applyButton);
            root.Add(footer);

            _applyProgress = new ProgressBar { title = "Ready", value = 0f };
            _applyProgress.style.marginLeft = 20f;
            _applyProgress.style.marginRight = 20f;
            _applyProgress.style.marginBottom = 4f;
            _applyProgress.style.display = DisplayStyle.None;
            root.Add(_applyProgress);
            _applyStatus = MPOUI.Text(string.Empty, "mpo-small");
            _applyStatus.style.marginLeft = 20f;
            _applyStatus.style.marginRight = 20f;
            _applyStatus.style.marginBottom = 14f;
            _applyStatus.style.display = DisplayStyle.None;
            root.Add(_applyStatus);

            RefreshSelectionSummary();
        }

        private VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("mpo-issue-row");
            row.style.height = 82f;

            var toggle = new Toggle { name = "toggle" };
            toggle.style.width = 30f;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (row.userData is BatchFixItem item)
                {
                    item.Selected = evt.newValue;
                    RefreshSelectionSummary();
                }
            });
            row.Add(toggle);

            var main = new VisualElement();
            main.AddToClassList("mpo-issue-row-main");
            var title = MPOUI.Text(string.Empty, "mpo-issue-row-title");
            title.name = "title";
            main.Add(title);
            var meta = MPOUI.Text(string.Empty, "mpo-issue-row-meta");
            meta.name = "meta";
            main.Add(meta);
            var preview = MPOUI.Text(string.Empty, "mpo-issue-row-impact");
            preview.name = "preview";
            main.Add(preview);
            row.Add(main);
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _items.Count)
                return;

            BatchFixItem item = _items[index];
            MPOIssue issue = item.Issue;
            element.userData = item;
            element.Q<Toggle>("toggle").SetValueWithoutNotify(item.Selected);
            element.Q<Label>("title").text = issue.Title;
            element.Q<Label>("meta").text = (issue.FixSafety == MPOFixSafety.Safe ? "SAFE" : "REVIEW") + "  •  " + issue.Category + "  •  " + issue.Severity;
            string preview = string.IsNullOrWhiteSpace(issue.FixPreview) ? issue.Recommendation : issue.FixPreview;
            element.Q<Label>("preview").text = string.IsNullOrWhiteSpace(preview) ? "No preview text" : preview.Replace("\n", " ");
        }

        private int SelectedCount => _items.Count(item => item.Selected);

        private void SetAll(bool value)
        {
            foreach (BatchFixItem item in _items)
                item.Selected = value;
            _list?.RefreshItems();
            RefreshSelectionSummary();
        }

        private void RefreshSelectionSummary()
        {
            if (_selectionLabel != null)
                _selectionLabel.text = SelectedCount + " / " + _items.Count + " selected";
            if (_applyButton != null)
                _applyButton.SetEnabled(SelectedCount > 0);
        }

        private void ApplySelected()
        {
            if (_isApplying)
                return;

            List<BatchFixItem> selected = _items.Where(item => item.Selected).ToList();
            if (selected.Count == 0)
                return;

            if (!EditorUtility.DisplayDialog(
                    _containsReviewFixes ? "Apply Selected Fixes" : "Apply Selected Safe Fixes",
                    "Apply " + selected.Count + " selected fix action(s)?\n\nChanges are processed one at a time so the Editor remains responsive between asset reimports. Original values are available through Revert Last Fix Session.",
                    "Apply",
                    "Cancel"))
                return;

            _applyQueue.Clear();
            _applyQueue.AddRange(selected);
            _applyFailures.Clear();
            _applyIndex = 0;
            _appliedCount = 0;
            _isApplying = true;

            if (_list != null) _list.SetEnabled(false);
            if (_applyButton != null) _applyButton.SetEnabled(false);
            if (_applyProgress != null)
            {
                _applyProgress.style.display = DisplayStyle.Flex;
                _applyProgress.value = 0f;
                _applyProgress.title = "Starting fixes...";
            }
            if (_applyStatus != null)
            {
                _applyStatus.style.display = DisplayStyle.Flex;
                _applyStatus.text = "Preparing " + _applyQueue.Count + " fix action(s)...";
            }

            EditorApplication.update -= ApplyNextQueuedFix;
            EditorApplication.update += ApplyNextQueuedFix;
        }

        private void ApplyNextQueuedFix()
        {
            if (!_isApplying)
            {
                EditorApplication.update -= ApplyNextQueuedFix;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (_applyStatus != null)
                    _applyStatus.text = "Waiting for Unity to finish compiling/importing...";
                return;
            }

            if (_applyIndex >= _applyQueue.Count)
            {
                FinishQueuedApply();
                return;
            }

            BatchFixItem item = _applyQueue[_applyIndex];
            MPOIssue issue = item.Issue;
            if (_applyProgress != null)
            {
                _applyProgress.value = _applyQueue.Count == 0 ? 100f : (_applyIndex / (float)_applyQueue.Count) * 100f;
                _applyProgress.title = (_applyIndex + 1) + " / " + _applyQueue.Count + "  " + issue.Title;
            }
            if (_applyStatus != null)
                _applyStatus.text = string.IsNullOrWhiteSpace(issue.AssetPath) ? issue.Title : issue.AssetPath;

            try
            {
                if (MPOFixEngine.Apply(issue, out string message))
                    _appliedCount++;
                else
                    _applyFailures.Add(issue.Title + ": " + message);
            }
            catch (Exception exception)
            {
                _applyFailures.Add(issue.Title + ": " + exception.Message);
            }

            _applyIndex++;
            Repaint();
        }

        private void FinishQueuedApply()
        {
            EditorApplication.update -= ApplyNextQueuedFix;
            _isApplying = false;
            AssetDatabase.SaveAssets();

            if (_applyProgress != null)
            {
                _applyProgress.value = 100f;
                _applyProgress.title = "Complete";
            }
            if (_applyStatus != null)
                _applyStatus.text = "Applied " + _appliedCount + " of " + _applyQueue.Count + " action(s).";

            string summary = "Applied " + _appliedCount + " fix action(s).";
            if (_applyFailures.Count > 0)
                summary += "\n\nCould not apply " + _applyFailures.Count + " action(s):\n" + string.Join("\n", _applyFailures.Take(12).ToArray());
            summary += "\n\nRe-scan the project to refresh results. You can revert the session from the Fixes page.";

            EditorUtility.DisplayDialog("Mobile Performance Optimizer", summary, "OK");
            if (_appliedCount > 0)
                _onApplied?.Invoke();
            Close();
        }

        private void OnDisable()
        {
            EditorApplication.update -= ApplyNextQueuedFix;
        }
    }
}
