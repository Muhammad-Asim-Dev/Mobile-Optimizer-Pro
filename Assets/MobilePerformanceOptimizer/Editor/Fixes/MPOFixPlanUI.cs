using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MobilePerformanceOptimizer
{
    internal static class MPOFixPlanUI
    {
        public static VisualElement Build(MPOFixPlan plan)
        {
            var host = new VisualElement();
            if (plan == null) return host;
            host.Add(new IMGUIContainer(() => Draw(plan)));
            return host;
        }

        private static void Draw(MPOFixPlan plan)
        {
            if (plan.Error != null) { EditorGUILayout.HelpBox(plan.Error, MessageType.Warning); return; }
            bool custom = EditorGUILayout.Popup("Optimization Mode", plan.Custom ? 1 : 0,
                new[] { "Recommended / Automatic", "Custom / Manual" }) == 1;
            if (custom != plan.Custom) plan.SetCustom(custom);
            plan.UpdateLinkedSettings();
            EditorGUILayout.HelpBox("Select settings to change. Custom values may leave valid findings on the next scan. Revert restores changed settings and reports conflicts with later edits.", MessageType.Info);
            bool linkedEmission = plan.Settings.Any(s => s.Key == "Emission" && s.Enabled && Equals(s.Selected, false));
            if (linkedEmission) EditorGUILayout.HelpBox("Disabling emission also sets its color to black and marks it non-emissive for GI. These linked changes are shown below and restored together.", MessageType.Info);
            foreach (var s in plan.Settings)
            {
                bool linked = linkedEmission && (s.Key == "Emission Color" || s.Key == "Emission GI Flags");
                if (!plan.Custom && !s.Enabled) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUI.DisabledScope(!plan.Custom || linked)) s.Enabled = EditorGUILayout.ToggleLeft(s.DisplayName, s.Enabled);
                    EditorGUILayout.LabelField("Current: " + s.Current + "    Recommended: " + s.Recommended, EditorStyles.wordWrappedLabel);
                    using (new EditorGUI.DisabledScope(!plan.Custom || !s.Enabled || linked))
                    {
                        if (s.Choices != null)
                        {
                            int index = Array.IndexOf(s.Choices, s.Selected);
                            int next = EditorGUILayout.Popup("Selected", index, s.Choices.Select(v => v.ToString()).ToArray());
                            if (next >= 0 && next < s.Choices.Length) s.Selected = s.Choices[next];
                        }
                        else if (s.Selected is bool b) s.Selected = EditorGUILayout.Toggle("Selected", b);
                        else if (s.Selected is int i) s.Selected = EditorGUILayout.IntField("Selected", i);
                        else if (s.Selected is Color color) s.Selected = EditorGUILayout.ColorField("Selected", color);
                        else if (s.Selected is float f) s.Selected = EditorGUILayout.FloatField("Selected", f);
                    }
                }
            }
        }
    }
}
