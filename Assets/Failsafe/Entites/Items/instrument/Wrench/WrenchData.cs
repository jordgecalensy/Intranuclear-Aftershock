using UnityEngine;

[CreateAssetMenu(fileName = "WrenchData", menuName = "ScriptableObjects/Entities/Items/Wrench")]
public class WrenchData : ScriptableObject
{
    public float StartUseDelay;
    /// <summary>
    /// Кулдаун изпользования предмета
    /// </summary>
    public float UseDelay;
    public int Damage;
    public float MaxDistance;
}
