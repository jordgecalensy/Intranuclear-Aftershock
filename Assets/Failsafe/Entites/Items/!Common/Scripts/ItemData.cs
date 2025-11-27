using UnityEngine;

public enum ItemType
{
    /// <summary>
    /// Расходник
    /// </summary>
    Consumable,
    /// <summary>
    /// Пистолет
    /// </summary>
    Gun,
    /// <summary>
    /// Граната
    /// </summary>
    Grenade,
    /// <summary>
    /// Выбрасываемый на землю предмет
    /// </summary>
    GroundItem,
    /// <summary>
    /// Инструмент
    /// </summary>
    Tool
}

[CreateAssetMenu(fileName = "ItemData", menuName = "ScriptableObjects/Entities/ItemData")]
public class ItemData : ScriptableObject
{
    public string Description;

    public ItemType Type;
}