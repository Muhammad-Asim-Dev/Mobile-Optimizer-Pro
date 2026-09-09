using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MobilePerformanceOptimizer
{
    internal static class MPOUI
    {
        public const string ThemePath = "Assets/MobilePerformanceOptimizer/Editor/UI/MobilePerformanceOptimizerTheme.uss";

        public static void ApplyTheme(VisualElement root)
        {
            if (root == null)
                return;

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath);
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);

            root.AddToClassList("mpo-root");
            root.RemoveFromClassList("mpo-light");
            root.AddToClassList("mpo-dark");
            root.AddToClassList("mpo-midnight-amber");
        }

        public static VisualElement Card(string extraClass = null)
        {
            var card = new VisualElement();
            card.AddToClassList("mpo-card");
            if (!string.IsNullOrWhiteSpace(extraClass))
                card.AddToClassList(extraClass);
            return card;
        }

        public static Label Text(string text, string className = null)
        {
            var label = new Label(text ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(className))
                label.AddToClassList(className);
            return label;
        }

        public static Button ActionButton(string text, Action clicked, string className = "mpo-button")
        {
            var button = new Button(clicked) { text = text ?? string.Empty };
            if (!string.IsNullOrWhiteSpace(className))
                button.AddToClassList(className);
            return button;
        }

        public static VisualElement Badge(string text, string className = null)
        {
            var badge = new VisualElement();
            badge.AddToClassList("mpo-badge");
            if (!string.IsNullOrWhiteSpace(className))
                badge.AddToClassList(className);
            badge.Add(new Label(text ?? string.Empty));
            return badge;
        }

        public static string SeverityClass(MPOSeverity severity)
        {
            switch (severity)
            {
                case MPOSeverity.Critical: return "mpo-critical";
                case MPOSeverity.Warning: return "mpo-warning";
                default: return "mpo-suggestion";
            }
        }

        public static string ImpactClass(MPOImpactLevel impact)
        {
            switch (impact)
            {
                case MPOImpactLevel.High: return "mpo-impact-high";
                case MPOImpactLevel.Medium: return "mpo-impact-medium";
                case MPOImpactLevel.Low: return "mpo-impact-low";
                default: return "mpo-impact-none";
            }
        }

        public static void AddImpactChip(VisualElement parent, string label, MPOImpactLevel impact)
        {
            if (parent == null)
                return;

            var chip = Badge(label + "  " + MPOImpactUtility.Label(impact).ToUpperInvariant(), ImpactClass(impact));
            chip.tooltip = label + " impact is a static estimate used for prioritization, not a profiler measurement.";
            parent.Add(chip);
        }

        public static VisualElement SectionHeader(string title, string subtitle = null)
        {
            var header = new VisualElement();
            header.AddToClassList("mpo-section-header");
            header.Add(Text(title, "mpo-section-title"));
            if (!string.IsNullOrWhiteSpace(subtitle))
                header.Add(Text(subtitle, "mpo-section-subtitle"));
            return header;
        }

        public static VisualElement EmptyState(string title, string message)
        {
            var empty = new VisualElement();
            empty.AddToClassList("mpo-empty");
            empty.Add(Text(title, "mpo-empty-title"));
            empty.Add(Text(message, "mpo-empty-copy"));
            return empty;
        }
    }
}
