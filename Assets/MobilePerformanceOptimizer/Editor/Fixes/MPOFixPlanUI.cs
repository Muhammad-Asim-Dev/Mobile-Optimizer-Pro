using System;
using System.Collections.Generic;
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
            if (plan == null)
                return host;

            var imgui = new IMGUIContainer(() => Draw(plan));
            imgui.style.flexShrink = 0f;
            host.Add(imgui);
            return host;
        }

        public static VisualElement BuildBulk(MPOBulkFixPreset preset, Action changed = null)
        {
            var host = new VisualElement();
            host.AddToClassList("mpo-bulk-editor");
            if (preset == null || !preset.HasEditableSettings)
                return host;

            var mode = new DropdownField("Category Mode");
            mode.AddToClassList("mpo-bulk-mode");
            mode.choices = new List<string> { "Recommended", "Custom for this category" };
            mode.index = preset.Custom ? 1 : 0;
            host.Add(mode);

            // Category presets can expose several editable settings. Bound that dynamic area
            // inside its own scroll view so it never pushes/overlaps the Problems results UI.
            var bodyScroll = new ScrollView(ScrollViewMode.Vertical);
            bodyScroll.AddToClassList("mpo-bulk-scroll");
            var body = new VisualElement();
            body.AddToClassList("mpo-bulk-body");
            bodyScroll.Add(body);
            host.Add(bodyScroll);

            Action rebuild = null;
            rebuild = () =>
            {
                body.Clear();
                if (!preset.Custom)
                {
                    var note = new Label("Recommended values will be used for every compatible fix in this category.");
                    note.AddToClassList("mpo-bulk-help");
                    body.Add(note);
                    return;
                }

                var help = new Label("Set each value once. It will be reused for compatible assets only; unsupported assets keep their valid recommended value.");
                help.AddToClassList("mpo-bulk-help");
                body.Add(help);

                foreach (MPOBulkSettingChoice setting in preset.Settings)
                    body.Add(BuildBulkSettingRow(setting, changed));
            };

            mode.RegisterValueChangedCallback(evt =>
            {
                bool custom = mode.index == 1;
                if (custom != preset.Custom)
                    preset.SetCustom(custom);
                rebuild();
                changed?.Invoke();
            });

            rebuild();
            return host;
        }

        private static VisualElement BuildBulkSettingRow(MPOBulkSettingChoice setting, Action changed)
        {
            var row = new VisualElement();
            row.AddToClassList("mpo-bulk-setting-row");

            var copy = new VisualElement();
            copy.AddToClassList("mpo-bulk-setting-copy");
            var enabled = new Toggle { text = setting.DisplayName, value = setting.Enabled };
            enabled.AddToClassList("mpo-bulk-setting-name");
            copy.Add(enabled);
            var recommended = new Label("Recommended: " + Format(setting.Recommended));
            recommended.AddToClassList("mpo-bulk-setting-recommended");
            copy.Add(recommended);
            row.Add(copy);

            VisualElement valueControl = BuildBulkValueControl(setting, changed);
            valueControl.AddToClassList("mpo-bulk-setting-control");
            valueControl.SetEnabled(setting.Enabled);
            row.Add(valueControl);

            enabled.RegisterValueChangedCallback(evt =>
            {
                setting.Enabled = evt.newValue;
                valueControl.SetEnabled(evt.newValue);
                changed?.Invoke();
            });

            return row;
        }

        private static VisualElement BuildBulkValueControl(MPOBulkSettingChoice setting, Action changed)
        {
            if (setting.Choices != null && setting.Choices.Length > 0)
            {
                var labels = setting.Choices.Select(Format).ToList();
                int currentIndex = Array.IndexOf(setting.Choices, setting.Selected);
                if (currentIndex < 0) currentIndex = 0;
                var field = new DropdownField("Use", labels, currentIndex);
                field.RegisterValueChangedCallback(evt =>
                {
                    int index = field.index;
                    if (index >= 0 && index < setting.Choices.Length)
                        setting.Selected = setting.Choices[index];
                    changed?.Invoke();
                });
                return field;
            }

            if (setting.Selected is bool boolValue)
            {
                var field = new Toggle("Use") { value = boolValue };
                field.RegisterValueChangedCallback(evt => { setting.Selected = evt.newValue; changed?.Invoke(); });
                return field;
            }

            if (setting.Selected is int intValue)
            {
                var field = new IntegerField("Use") { value = intValue };
                field.RegisterValueChangedCallback(evt => { setting.Selected = evt.newValue; changed?.Invoke(); });
                return field;
            }

            if (setting.Selected is float floatValue)
            {
                var field = new FloatField("Use") { value = floatValue };
                field.RegisterValueChangedCallback(evt => { setting.Selected = evt.newValue; changed?.Invoke(); });
                return field;
            }

            var fallback = new Label(Format(setting.Selected));
            fallback.AddToClassList("mpo-muted");
            return fallback;
        }

        private static void Draw(MPOFixPlan plan)
        {
            if (plan.Error != null)
            {
                EditorGUILayout.HelpBox(plan.Error, MessageType.Warning);
                return;
            }

            bool supportsUsefulCustomValue = plan.Settings.Any(IsEditableSetting);
            if (supportsUsefulCustomValue)
            {
                bool custom = EditorGUILayout.Popup(
                    "Apply Mode",
                    plan.Custom ? 1 : 0,
                    new[] { "Recommended", "Custom Value" }) == 1;

                if (custom != plan.Custom)
                    plan.SetCustom(custom);
            }
            else if (plan.Custom)
            {
                plan.SetCustom(false);
            }

            plan.UpdateLinkedSettings();

            EditorGUILayout.HelpBox(
                plan.Custom
                    ? "Enable only the values you want to override. Unity validates, saves/reimports and verifies every selected value."
                    : "Recommended values come from the selected platform and device profile.",
                MessageType.Info);

            var visible = plan.Custom ? plan.Settings : plan.Settings.Where(setting => setting.Enabled);
            foreach (MPOSettingChoice setting in visible)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (plan.Custom)
                        setting.Enabled = EditorGUILayout.ToggleLeft("Change " + setting.DisplayName, setting.Enabled, EditorStyles.boldLabel);
                    else
                        EditorGUILayout.LabelField(setting.DisplayName, EditorStyles.boldLabel);

                    EditorGUILayout.LabelField("Current: " + Format(setting.Current), EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("Recommended: " + Format(setting.Recommended), EditorStyles.wordWrappedLabel);

                    if (!plan.Custom || !setting.Enabled)
                        continue;

                    setting.Selected = DrawValue("Selected", setting.Selected, setting.Choices);
                }
            }
        }

        private static bool IsEditableSetting(MPOSettingChoice setting)
        {
            if (setting == null || setting.Current == null)
                return false;

            Type type = setting.Current.GetType();
            return setting.Choices != null || type == typeof(bool) || type == typeof(int) || type == typeof(float);
        }

        private static object DrawValue(string label, object value, object[] choices)
        {
            if (value == null)
                return value;

            if (choices != null && choices.Length > 0)
            {
                int index = Array.IndexOf(choices, value);
                string[] labels = choices.Select(Format).ToArray();
                int next = EditorGUILayout.Popup(label, Math.Max(0, index), labels);
                return next >= 0 && next < choices.Length ? choices[next] : value;
            }

            if (value is bool boolValue)
                return EditorGUILayout.Toggle(label, boolValue);
            if (value is int intValue)
                return EditorGUILayout.IntField(label, intValue);
            if (value is float floatValue)
                return EditorGUILayout.FloatField(label, floatValue);

            EditorGUILayout.LabelField(label, Format(value));
            return value;
        }

        private static string Format(object value)
        {
            return value == null ? "—" : value.ToString();
        }
    }
}
