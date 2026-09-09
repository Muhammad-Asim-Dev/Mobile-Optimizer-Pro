using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MobilePerformanceOptimizer
{
    public sealed class MPODiagnosticsWindow : EditorWindow
    {
        private sealed class DiagnosticRow
        {
            public string Type;
            public string Message;
        }

        private readonly List<DiagnosticRow> _rows = new List<DiagnosticRow>();
        private MPOScanResult _result;
        private ListView _list;

        public static void ShowFor(MPOScanResult result)
        {
            var window = GetWindow<MPODiagnosticsWindow>(true, "MPO Scan Diagnostics", true);
            window.minSize = new Vector2(760f, 480f);
            window._result = result;
            window.RebuildRows();
            window.BuildUi();
            window.Show();
        }

        public void CreateGUI()
        {
            MPOUI.ApplyTheme(rootVisualElement);
            rootVisualElement.AddToClassList("mpo-diagnostics-root");
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
            copy.Add(MPOUI.Text("Scan diagnostics", "mpo-page-title"));
            copy.Add(MPOUI.Text("Unreadable or broken project items are skipped so the remaining analyzers can finish.", "mpo-page-subtitle"));
            header.Add(copy);
            var close = MPOUI.ActionButton("Close", Close);
            header.Add(close);
            root.Add(header);

            if (_result == null)
            {
                root.Add(MPOUI.EmptyState("No diagnostics available", "Run a scan and reopen diagnostics from the optimizer window."));
                return;
            }

            var summary = new VisualElement();
            summary.AddToClassList("mpo-card-row");
            summary.Add(BuildStat("FAILED ANALYZERS", _result.FailedAnalyzerCount, "Analyzer-level failures"));
            summary.Add(BuildStat("SKIPPED ITEMS", _result.RecoverableErrorCount, "Recovered item-level failures"));
            summary.Add(BuildStat("COMPLETED", _result.CompletedAnalyzerCount, "of " + _result.TotalAnalyzerCount + " analyzers"));
            root.Add(summary);

            RebuildRows();
            _list = new ListView();
            _list.AddToClassList("mpo-list");
            _list.AddToClassList("mpo-diagnostics-list");
            _list.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            _list.itemsSource = _rows;
            _list.makeItem = MakeRow;
            _list.bindItem = BindRow;
            root.Add(_list);

            if (_result.SuppressedRecoverableWarningCount > 0)
            {
                Label note = MPOUI.Text(
                    _result.SuppressedRecoverableWarningCount + " additional skipped item(s) were counted but not retained in memory. Exported reports preserve the diagnostic totals.",
                    "mpo-banner");
                note.AddToClassList("mpo-banner-info");
                root.Add(note);
            }
        }

        private static VisualElement BuildStat(string title, int value, string copy)
        {
            var card = MPOUI.Card("mpo-stat-card");
            card.Add(MPOUI.Text(title, "mpo-stat-kicker"));
            card.Add(MPOUI.Text(value.ToString("N0"), "mpo-stat-value"));
            card.Add(MPOUI.Text(copy, "mpo-stat-copy"));
            return card;
        }

        private void RebuildRows()
        {
            _rows.Clear();
            if (_result == null)
                return;

            foreach (string error in _result.AnalyzerErrors)
                _rows.Add(new DiagnosticRow { Type = "ANALYZER FAILURE", Message = error ?? string.Empty });
            foreach (string warning in _result.RecoverableWarnings)
                _rows.Add(new DiagnosticRow { Type = "SKIPPED ITEM", Message = warning ?? string.Empty });
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("mpo-diagnostic-row");
            var badge = MPOUI.Text(string.Empty, "mpo-diagnostic-type");
            badge.name = "type";
            var message = MPOUI.Text(string.Empty, "mpo-diagnostic-message");
            message.name = "message";
            row.Add(badge);
            row.Add(message);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count)
                return;
            DiagnosticRow row = _rows[index];
            element.Q<Label>("type").text = row.Type;
            element.Q<Label>("message").text = row.Message;
            element.tooltip = row.Message;
        }
    }
}
