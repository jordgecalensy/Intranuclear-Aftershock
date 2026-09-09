using UnityEditor;
using UnityEngine;

namespace Failsafe.Scripts.Modifiebles
{
    [CustomPropertyDrawer(typeof(ModifiableField<float>))]
    public class ModifiableFieldFloatDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty baseValue = property.FindPropertyRelative(nameof(ModifiableField<float>.BaseValue));

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.PropertyField(position, baseValue, label);
            EditorGUI.EndProperty();
        }
    }
}
