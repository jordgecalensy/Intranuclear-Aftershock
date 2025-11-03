#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SymbolDictionary.Entry))]
public class SymbolDictionaryEntryDrawer : PropertyDrawer
{
    private const float Cell = 18f;
    private const float Pad  = 4f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // key + grid + buttons + description + mask label + небольшой отступ
        float h = EditorGUIUtility.singleLineHeight;                 // key
        h += (Cell * 3f + Pad * 2f) + Pad;                           // grid
        h += EditorGUIUtility.singleLineHeight + Pad;                // buttons
        h += EditorGUIUtility.singleLineHeight + Pad;                // mask label
        h += EditorGUIUtility.singleLineHeight * 3f + Pad;           // description (примерно 3 строки)
        h += Pad * 2f;
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var r = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        // fields
        var keyProp   = property.FindPropertyRelative("key");
        var cellsProp = property.FindPropertyRelative("cells");
        var descProp  = property.FindPropertyRelative("description");
        var maskProp  = property.FindPropertyRelative("mask9");

        // key
        EditorGUI.PropertyField(r, keyProp, new GUIContent("Symbol"));
        r.y += r.height + Pad;

        // ensure size 9
        if (cellsProp.arraySize != 9) cellsProp.arraySize = 9;

        // grid 3x3
        var gridRect = new Rect(r.x, r.y, Cell * 3f + Pad * 2f, Cell * 3f + Pad * 2f);
        DrawGrid(gridRect, cellsProp);
        r.y += gridRect.height + Pad;

        // buttons: Clear / Fill / Invert
        var btnW = (position.width - Pad * 2f) / 3f;
        var br = new Rect(position.x, r.y, btnW, EditorGUIUtility.singleLineHeight);
        if (GUI.Button(br, "Clear")) SetAll(cellsProp, false);
        br.x += btnW + Pad;
        if (GUI.Button(br, "Fill")) SetAll(cellsProp, true);
        br.x += btnW + Pad;
        if (GUI.Button(br, "Invert")) Invert(cellsProp);
        r.y += EditorGUIUtility.singleLineHeight + Pad;

        // current mask label (read-only)
        ushort mask = BuildMask(cellsProp);
        EditorGUI.LabelField(r, "Mask 9 (bits):", SymbolDictionary.Mask9ToString(mask));
        r.y += EditorGUIUtility.singleLineHeight + Pad;

        // description
        EditorGUI.PropertyField(r, descProp, new GUIContent("Description"));
        r.y += EditorGUI.GetPropertyHeight(descProp, true) + Pad;

        // push back mask9 runtime field
        maskProp.intValue = mask;

        EditorGUI.EndProperty();
    }

    private static void DrawGrid(Rect rect, SerializedProperty cellsProp)
    {
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int idx = row * 3 + col;
                var cell = cellsProp.GetArrayElementAtIndex(idx);

                var cRect = new Rect(
                    rect.x + col * (Cell + Pad),
                    rect.y + row * (Cell + Pad),
                    Cell, Cell);

                bool v = cell.boolValue;
                v = GUI.Toggle(cRect, v, GUIContent.none, "Button");
                cell.boolValue = v;
            }
        }
    }

    private static ushort BuildMask(SerializedProperty cellsProp)
    {
        ushort m = 0;
        for (int i = 0; i < 9; i++)
        {
            var c = cellsProp.GetArrayElementAtIndex(i);
            if (c.boolValue) m |= (ushort)(1 << i);
        }
        return m;
    }

    private static void SetAll(SerializedProperty cellsProp, bool v)
    {
        for (int i = 0; i < 9; i++)
            cellsProp.GetArrayElementAtIndex(i).boolValue = v;
    }

    private static void Invert(SerializedProperty cellsProp)
    {
        for (int i = 0; i < 9; i++)
        {
            var p = cellsProp.GetArrayElementAtIndex(i);
            p.boolValue = !p.boolValue;
        }
    }
}
#endif