using Failsafe.Inventory.Presentation;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InventoryPanelBinding))]
public sealed class InventoryPanelBindingEditor : Editor
{
    private bool _showAdvanced;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Углы панели со стороны игрока", EditorStyles.boldLabel);
        Draw("BottomLeft", "Нижний левый");
        Draw("BottomRight", "Нижний правый");
        Draw("TopLeft", "Верхний левый");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Интерфейс", EditorStyles.boldLabel);
        Draw("TargetRoot", "Перемещаемый объект");
        Draw("ReferenceRect", "Область для совмещения");
        Draw("SurfaceOffset", "Отступ от поверхности");
        _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Дополнительные настройки", true);
        if (_showAdvanced)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                Draw("AdditionalRoots", "Дополнительные объекты");
                Draw("Padding", "Отступ от краёв");
                Draw("PreviewInEditor", "Совмещать в редакторе");
            }
        }
        serializedObject.ApplyModifiedProperties();
        var binding = (InventoryPanelBinding)target;
        bool valid = binding.TryGetFit(out _, out _, out _, out _, out _, out string error);
        EditorGUILayout.HelpBox(valid
            ? "Пропорции UI сохраняются. Жёлтая линия показывает сторону положительного отступа."
            : error, valid ? MessageType.Info : MessageType.Warning);
        using (new EditorGUI.DisabledScope(!valid))
            if (GUILayout.Button("Совместить с панелью"))
            {
                Undo.RecordObject(binding.TargetRoot, "Fit inventory panel");
                if (binding.AdditionalRoots != null)
                    foreach (Transform root in binding.AdditionalRoots)
                        Undo.RecordObject(root, "Fit inventory panel");
                binding.TryApply(out _);
                EditorUtility.SetDirty(binding.TargetRoot);
                PrefabUtility.RecordPrefabInstancePropertyModifications(binding.TargetRoot);
                if (binding.AdditionalRoots != null)
                    foreach (Transform root in binding.AdditionalRoots)
                    {
                        EditorUtility.SetDirty(root);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
                    }
                SceneView.RepaintAll();
            }
    }

    private void Draw(string propertyName, string label) =>
        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), new GUIContent(label), true);
}
