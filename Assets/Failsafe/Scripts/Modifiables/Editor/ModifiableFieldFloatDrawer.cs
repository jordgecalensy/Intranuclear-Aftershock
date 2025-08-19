using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Failsafe.Scripts.Modifiebles
{
    public class ModifiableFieldFloatDrawer : OdinValueDrawer<ModifiableField<float>>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            if (label != null)
            {
                rect = EditorGUI.PrefixLabel(rect, label);
            }

            var value = this.ValueEntry.SmartValue;
            var baseValue = value.BaseValue;
            GUIHelper.PushLabelWidth(20);
            value.BaseValue = EditorGUI.FloatField(rect.AlignLeft(rect.width * 0.5f), value.BaseValue);
            if (baseValue != value.BaseValue)
            {
                value.Recalculate();
            }
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.FloatField(rect.AlignRight(rect.width * 0.5f), value.Value);
            EditorGUI.EndDisabledGroup();
            GUIHelper.PopLabelWidth();

            this.ValueEntry.SmartValue = value;
        }
    }
}