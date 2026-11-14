using Failsafe.Inventory.Presentation;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InventoryRobotPresentationController))]
[CanEditMultipleObjects]
public sealed class InventoryRobotPresentationControllerEditor : Editor
{
    private bool _showAdvanced;
    private bool _showTesting;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Объекты Руви", EditorStyles.boldLabel);
        Draw("_animator", "Анимация вылета и возврата");
        Draw("_visualRoot", "Видимая часть робота");
        Draw("_bodyAnimator", "Анимация корпуса и крышки");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Скорость", EditorStyles.boldLabel);
        Draw("_bodyOpenSpeed", "Открытие крышки");
        Draw("_bodyCloseSpeed", "Закрытие крышки");

        EditorGUILayout.Space();
        _showTesting = EditorGUILayout.Foldout(_showTesting, "Проверка анимаций", true);
        if (_showTesting)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                Draw("_reactionOverride", "Вариант открытия");
                EditorGUILayout.HelpBox("Random выбирает вариант случайно. Остальные значения фиксируют анимацию для проверки.",
                    MessageType.Info);
                if (Application.isPlaying && targets.Length == 1)
                {
                    var controller = (InventoryRobotPresentationController)target;
                    EditorGUILayout.LabelField("Состояние", controller.State.ToString());
                    EditorGUILayout.LabelField("Текущий вариант", controller.CurrentReaction.ToString());
                }
            }
        }
        SerializedProperty reaction = serializedObject.FindProperty("_reactionOverride");
        if (!reaction.hasMultipleDifferentValues && reaction.enumValueIndex != (int)InventoryRobotReaction.Random)
            EditorGUILayout.HelpBox("Вариант открытия зафиксирован. Для обычной игры выберите Random в разделе проверки.",
                MessageType.Warning);

        _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Дополнительные настройки", true);
        if (_showAdvanced)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                Draw("_startHidden", "Скрывать при запуске");
                EditorGUILayout.LabelField("Состояния Animator", EditorStyles.boldLabel);
                Draw("_bodyOpenState", "Open1");
                Draw("_bodyOpen2State", "Open2");
                Draw("_bodyOpen3State", "Open3");
                Draw("_bodyRefusalState", "Отказ");
                Draw("_bodyCloseSourceState", "Источник закрытия");
                Draw("_returnState", "Возврат за спину");
                Draw("_openTriggerName", "Команда вылета");
                Draw("_closeTriggerName", "Команда возврата");
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Ограничения ожидания, секунды", EditorStyles.boldLabel);
                Draw("_bodyOpeningTimeout", "Корпус: открытие");
                Draw("_bodyClosingTimeout", "Корпус: закрытие");
                Draw("_closingTimeout", "Возврат за спину");
                Draw("_useFallbackTimeout", "Страховка событий анимации");
                if (serializedObject.FindProperty("_useFallbackTimeout").boolValue)
                    Draw("_openingTimeout", "Вылет: ожидание события");
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string propertyName, string label) =>
        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), new GUIContent(label));
}
