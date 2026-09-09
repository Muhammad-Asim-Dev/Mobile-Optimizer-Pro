using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MobilePerformanceOptimizer
{
    public sealed class MPOIgnoredItemsWindow : EditorWindow
    {
        private sealed class IgnoreRow
        {
            public string Type;
            public string Value;
        }

        private readonly List<IgnoreRow> _rows = new List<IgnoreRow>();
        private Action _onChanged;
        private ListView _list;

        public static void ShowFor(Action onChanged)
        {
            var window = GetWindow<MPOIgnoredItemsWindow>();
            window.titleContent = new GUIContent("MPO Ignored Items");
            window.minSize = new Vector2(720f, 460f);
            window._onChanged = onChanged;
            window.BuildUi();
            window.Show();
        }

        public void CreateGUI()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            MPOUI.ApplyTheme(root);
            root.AddToClassList("mpo-diagnostics-root");

            var header = new VisualElement();
            header.AddToClassList("mpo-diagnostics-header");
            var copy = new VisualElement();
            copy.AddToClassList("mpo-flex");
            copy.Add(MPOUI.Text("Ignored items", "mpo-page-title"));
            copy.Add(MPOUI.Text("Restore individual ignored rules, assets or folders without clearing everything.", "mpo-page-subtitle"));
            header.Add(copy);
            var clear = MPOUI.ActionButton("Clear All", ClearAll);
            clear.AddToClassList("mpo-danger");
            clear.SetEnabled(MPOIgnoreStore.PersistentCount > 0);
            header.Add(clear);
            root.Add(header);

            RebuildRows();
            if (_rows.Count == 0)
            {
                root.Add(MPOUI.EmptyState("No persistent ignored items", "Use Ignore on a finding to exclude a rule, asset or folder from future scores."));
                return;
            }

            _list = new ListView();
            _list.AddToClassList("mpo-list");
            _list.AddToClassList("mpo-diagnostics-list");
            _list.fixedItemHeight = 58f;
            _list.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _list.itemsSource = _rows;
            _list.makeItem = MakeRow;
            _list.bindItem = BindRow;
            root.Add(_list);
        }

        private void RebuildRows()
        {
            _rows.Clear();
            foreach (string rule in MPOIgnoreStore.GetRules())
                _rows.Add(new IgnoreRow { Type = "RULE", Value = rule });
            foreach (string asset in MPOIgnoreStore.GetAssets())
                _rows.Add(new IgnoreRow { Type = "ASSET", Value = asset });
            foreach (string folder in MPOIgnoreStore.GetFolders())
                _rows.Add(new IgnoreRow { Type = "FOLDER", Value = folder });
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("mpo-ignore-manager-row");

            var type = MPOUI.Badge(string.Empty, "mpo-impact-none");
            type.name = "typeBadge";
            row.Add(type);

            var value = MPOUI.Text(string.Empty, "mpo-ignore-manager-value");
            value.name = "value";
            row.Add(value);

            var restore = MPOUI.ActionButton("Restore", null);
            restore.name = "restore";
            restore.clicked += () => RestoreRow(restore.userData as IgnoreRow);
            row.Add(restore);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count)
                return;

            IgnoreRow row = _rows[index];
            VisualElement badge = element.Q<VisualElement>("typeBadge");
            Label badgeLabel = badge != null ? badge.Q<Label>() : null;
            if (badgeLabel != null)
                badgeLabel.text = row.Type;

            Label value = element.Q<Label>("value");
            if (value != null)
            {
                value.text = Compact(row.Value, 100);
                value.tooltip = row.Value;
            }

            Button restore = element.Q<Button>("restore");
            if (restore != null)
                restore.userData = row;
        }

        private void RestoreRow(IgnoreRow row)
        {
            if (row == null)
                return;

            bool changed = false;
            if (string.Equals(row.Type, "RULE", StringComparison.Ordinal))
                changed = MPOIgnoreStore.RemoveRule(row.Value);
            else if (string.Equals(row.Type, "ASSET", StringComparison.Ordinal))
                changed = MPOIgnoreStore.RemoveAsset(row.Value);
            else if (string.Equals(row.Type, "FOLDER", StringComparison.Ordinal))
                changed = MPOIgnoreStore.RemoveFolder(row.Value);

            if (!changed)
                return;

            _onChanged?.Invoke();
            BuildUi();
        }

        private void ClearAll()
        {
            if (!EditorUtility.DisplayDialog("Clear Ignored Items", "Restore every persistent ignored rule, asset and folder?", "Clear All", "Cancel"))
                return;
            MPOIgnoreStore.ClearAll();
            _onChanged?.Invoke();
            BuildUi();
        }

        private static string Compact(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= max)
                return value ?? string.Empty;
            int keep = Math.Max(8, (max - 3) / 2);
            return value.Substring(0, keep) + "..." + value.Substring(value.Length - keep);
        }
    }
}
