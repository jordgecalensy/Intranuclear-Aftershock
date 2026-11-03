using Failsafe.Scripts.Damage;
using UnityEditor;

[CustomEditor(typeof(WeakPoint))]
public sealed class WeakPointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var weakPoint = (WeakPoint)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Accumulated Damage", weakPoint.AccumulatedDamage);
            EditorGUILayout.Toggle("Threshold Reached", weakPoint.IsThresholdReached);
        }
    }

    public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;
}
