using UnityEngine;

[CreateAssetMenu(menuName = "Failsafe/Code Pattern", fileName = "CodePattern")]
public class CodePatternSO : ScriptableObject
{
    public string patternName = "Default";
    [TextArea(1, 8)] public string code = "ABC";
}