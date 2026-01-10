#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

[CustomEditor(typeof(TMPDropdownOwnerId))]
public class TMPDropdownOwnerIdSyncEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var owner = (TMPDropdownOwnerId)target;
        if (owner == null) return;

        // Если этот OwnerId стоит на объекте с TMP_Dropdown — синкаем Template автоматически
        var dropdown = owner.GetComponent<TMP_Dropdown>();
        if (dropdown == null || dropdown.template == null)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("TMP Dropdown ID Sync", EditorStyles.boldLabel);

        if (GUILayout.Button("Sync ID to Template"))
        {
            Sync(owner, dropdown);
        }

        // Автосинк на каждом инспектор-рисовании (без кнопки тоже будет держать в актуале)
        Sync(owner, dropdown);
    }

    private void Sync(TMPDropdownOwnerId rootOwner, TMP_Dropdown dropdown)
    {
        if (rootOwner == null || dropdown == null || dropdown.template == null)
            return;

        var templateGO = dropdown.template.gameObject;
        var templateOwner = templateGO.GetComponent<TMPDropdownOwnerId>();
        if (templateOwner == null)
        {
            templateOwner = templateGO.AddComponent<TMPDropdownOwnerId>();
            EditorUtility.SetDirty(templateGO);
        }

        if (templateOwner.Id != rootOwner.Id)
        {
            Undo.RecordObject(templateOwner, "Sync TMP Dropdown Owner Id");
            templateOwner.SetId(rootOwner.Id);
            EditorUtility.SetDirty(templateOwner);
        }
    }
}
#endif
