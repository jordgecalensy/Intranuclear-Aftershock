#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Failsafe.Inventory.Editor
{
    [CustomEditor(typeof(ItemDefinition))]
    public class ItemDefinitionEditor : UnityEditor.Editor
    {
        SerializedProperty worldPrefab, displayName;
        SerializedProperty wProp, hProp, footprint;
        SerializedProperty maxStack, isHeavy, canRotate;
        SerializedProperty fitMode, scaleMult;
        SerializedProperty poseMode, manualLocalScale, manualLocalEuler,
                          manualLocalPositionMeters, manualOffsetCellsXZ, manualOffsetY;

        void OnEnable()
        {
            worldPrefab = serializedObject.FindProperty("WorldPrefab");
            displayName = serializedObject.FindProperty("displayName");

            wProp       = serializedObject.FindProperty("shapeWidth");
            hProp       = serializedObject.FindProperty("shapeHeight");
            footprint   = serializedObject.FindProperty("footprint");

            maxStack    = serializedObject.FindProperty("maxStack");
            isHeavy     = serializedObject.FindProperty("isHeavy");
            canRotate   = serializedObject.FindProperty("canRotate");

            fitMode     = serializedObject.FindProperty("fitMode");
            scaleMult   = serializedObject.FindProperty("scaleMultiplier");

            poseMode                = serializedObject.FindProperty("poseMode");
            manualLocalScale        = serializedObject.FindProperty("manualLocalScale");
            manualLocalEuler        = serializedObject.FindProperty("manualLocalEuler");
            manualLocalPositionMeters = serializedObject.FindProperty("manualLocalPositionMeters");
            manualOffsetCellsXZ     = serializedObject.FindProperty("manualOffsetCellsXZ");
            manualOffsetY           = serializedObject.FindProperty("manualOffsetY");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // World / Meta
            EditorGUILayout.PropertyField(worldPrefab, new GUIContent("World Prefab"));
            EditorGUILayout.PropertyField(displayName, new GUIContent("Display Name"));
            EditorGUILayout.Space(6);

            // Grid Footprint
            EditorGUILayout.LabelField("Grid Footprint", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(wProp, new GUIContent("Width"));
            EditorGUILayout.PropertyField(hProp, new GUIContent("Height"));

            int w = Mathf.Max(1, wProp.intValue);
            int h = Mathf.Max(1, hProp.intValue);
            int need = w * h;
            if (footprint.arraySize != need) footprint.arraySize = need;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill Rectangle")) SetAll(footprint, true);
            if (GUILayout.Button("Clear All"))      SetAll(footprint, false);
            EditorGUILayout.EndHorizontal();

            const float cell = 20f;
            var on  = new Color(0.25f, 0.8f, 0.35f, 0.65f);
            var off = new Color(0.20f, 0.20f, 0.20f, 0.30f);
            for (int y = 0; y < h; y++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    bool v = footprint.GetArrayElementAtIndex(idx).boolValue;
                    var r = GUILayoutUtility.GetRect(cell, cell, GUILayout.ExpandWidth(false));
                    EditorGUI.DrawRect(r, v ? on : off);
                    if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                    {
                        footprint.GetArrayElementAtIndex(idx).boolValue = !v;
                        GUI.changed = true; Event.current.Use();
                    }
                    Handles.color = new Color(0,0,0,0.7f);
                    Handles.DrawAAPolyLine(2f, new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin));
                    Handles.DrawAAPolyLine(2f, new Vector3(r.xMax, r.yMin), new Vector3(r.xMax, r.yMax));
                    Handles.DrawAAPolyLine(2f, new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax));
                    Handles.DrawAAPolyLine(2f, new Vector3(r.xMin, r.yMax), new Vector3(r.xMin, r.yMin));
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);
            // Stack / Flags
            EditorGUILayout.PropertyField(maxStack);
            EditorGUILayout.PropertyField(isHeavy);
            EditorGUILayout.PropertyField(canRotate);

            EditorGUILayout.Space(6);
            // Visual Fit (авто)
            EditorGUILayout.LabelField("Visual Fit (Auto)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(fitMode);
            EditorGUILayout.PropertyField(scaleMult);

            EditorGUILayout.Space(6);
            // Inventory Pose Override (ручной)
            EditorGUILayout.LabelField("Inventory Pose Override", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(poseMode);

            var mode = (InventoryPoseMode)poseMode.enumValueIndex;
            if (mode == InventoryPoseMode.ManualMeters || mode == InventoryPoseMode.ManualCells)
            {
                EditorGUILayout.PropertyField(manualLocalScale, new GUIContent("Manual Local Scale"));
                EditorGUILayout.PropertyField(manualLocalEuler, new GUIContent("Manual Local Euler"));

                if (mode == InventoryPoseMode.ManualMeters)
                {
                    EditorGUILayout.PropertyField(manualLocalPositionMeters, new GUIContent("Manual Local Position (meters)"));
                }
                else
                {
                    EditorGUILayout.PropertyField(manualOffsetCellsXZ, new GUIContent("Manual Offset XZ (cells)"));
                    EditorGUILayout.PropertyField(manualOffsetY, new GUIContent("Manual Offset Y (meters)"));
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void SetAll(SerializedProperty arr, bool val)
        {
            for (int i = 0; i < arr.arraySize; i++)
                arr.GetArrayElementAtIndex(i).boolValue = val;
        }
    }
}
#endif
