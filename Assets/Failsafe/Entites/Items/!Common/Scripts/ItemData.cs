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

    [Header("Inventory")]
    [Tooltip("Stable item definition ID. Generate it once in the ItemData inspector and do not change it after saves start using it.")]
    public string InventoryDefinitionId;

    [Tooltip("Name shown in the inventory UI. If empty, the asset name can be used as a visual fallback.")]
    public string DisplayName;

    [Tooltip("Optional fallback used when a 3D inventory model is unavailable.")]
    public Sprite InventoryIcon;

    [Tooltip("Prefab used when the item must be created in the game world, for example after dropping or loading.")]
    public Item WorldItemPrefab;

    [Tooltip("Render-only prefab used as the 3D representation inside the inventory.")]
    public GameObject InventoryModelPrefab;

    [Tooltip("One-time orientation correction that puts the model into its canonical inventory pose.")]
    public Vector3 InventoryBaseEulerAngles;

    [Tooltip("Artistic offset after automatic centering, measured in inventory cells.")]
    public Vector3 InventoryModelOffsetInCells;

    [Min(0.01f)]
    [Tooltip("Additional multiplier applied after the model is automatically fitted into its cells.")]
    public float InventoryModelScaleMultiplier = 1f;

    [Range(0f, 0.45f)]
    [Tooltip("Empty border kept around the fitted model, expressed as a ratio of its available space.")]
    public float InventoryModelFitPadding = 0.08f;

    [Min(0.01f)]
    [Tooltip("Maximum distance the model may rise above the inventory plane, measured in cells.")]
    public float InventoryModelMaxDepthInCells = 0.75f;

    [Min(1)]
    public int InventoryWidth = 1;

    [Min(1)]
    public int InventoryHeight = 1;

    [Min(1)]
    public int InventoryMaxStack = 1;

    public bool CanRotateInInventory = true;
    public bool CanAssignQuickSlot = true;

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
