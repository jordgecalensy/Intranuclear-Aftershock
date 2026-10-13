using Failsafe.Inventory.Presentation;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RuviFaceController))]
public sealed class RuviFaceControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_presentation"), new GUIContent("Робот"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_screenRenderer"), new GUIContent("Экран лица"));
        SerializedProperty libraryProperty = serializedObject.FindProperty("_library");
        EditorGUILayout.PropertyField(libraryProperty, new GUIContent("Эмоции"));
        serializedObject.ApplyModifiedProperties();

        RuviEmotionLibrary library = libraryProperty.objectReferenceValue as RuviEmotionLibrary;
        if (library == null)
            return;
        EditorGUILayout.HelpBox("Шансы задаются в библиотеке эмоций. Кнопки проверки доступны в Play Mode, когда инвентарь открыт.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            RuviFaceController controller = (RuviFaceController)target;
            for (int i = 0; i < library.Emotions.Count; i++)
                if (library.Emotions[i] != null && GUILayout.Button(library.Emotions[i].Name))
                    controller.PlayEmotion(i);
            if (GUILayout.Button("Нейтральное лицо"))
                controller.ShowNeutral();
        }
    }
}
