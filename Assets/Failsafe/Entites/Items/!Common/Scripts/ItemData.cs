using Failsafe.Scripts.EffectSystem;
using FMODUnity;
using UnityEngine;

public enum ItemType
{
    /// <summary>
    /// Расходник
    /// </summary>
    Consumable,

    /// <summary>
    /// Старый тип пистолета / legacy.
    /// Для игрока оружие лучше делать через конкретный IUsable.
    /// </summary>
    Gun,

    /// <summary>
    /// Стазис-пушка как обычный предмет через IUsable.
    /// </summary>
    StasisGun,

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
    [Header("Base")]
    public string Description;
    public ItemType Type;

    [Header("Use Timings")]
    public float StartUseDelay = 0f;
    public float UseDelay = 0.2f;

    [Header("Energy / Charges")]
    [Tooltip("Если false, поля энергии игнорируются.")]
    public bool UsesEnergy = false;

    [Tooltip("Максимальный заряд конкретного экземпляра предмета.")]
    public int EnergyAmountMax = 0;

    [Tooltip("Сколько заряда тратится за одно использование.")]
    public int EnergyCostPerUse = 1;

    [Header("Raycast Use")]
    public float UseRange = 100f;
    public LayerMask UseMask = ~0;

    [Header("Effects")]
    public EffectBundle DefaultModeEffects;
    public EffectBundle AlternativeModeEffects;

    [Header("SFX")]
    public EventReference UseSFX;
    public EventReference EmptyUseSFX;
    public EventReference ModeSwitchSFX;
}