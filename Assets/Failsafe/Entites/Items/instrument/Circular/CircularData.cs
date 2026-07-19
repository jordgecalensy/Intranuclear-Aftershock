using UnityEngine;

[CreateAssetMenu(fileName = "CircularData", menuName = "ScriptableObjects/Entities/Items/CircularData")]
public class CircularData : ScriptableObject
{
    public float StartUseDelay;
    /// <summary>
    /// Кулдаун изпользования предмета
    /// </summary>
    public float UseDelay;
    public int Damage;
    public float MaxDistance;
}
