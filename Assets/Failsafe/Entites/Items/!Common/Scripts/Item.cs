using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Item : Prop
{
    public ItemData ItemData;
    public List<ActionsGroup> ActionsGroups;

    private Rigidbody _rigidbody;
    private Collider _collider;
    private Transform _handlePoint;

    private int _energyAmountCurrent;
    private bool _runtimeStateInitialized;

    public Transform HandlePoint => _handlePoint;

    public int EnergyAmountCurrent => _energyAmountCurrent;

    public int EnergyAmountMax
    {
        get
        {
            if (!HasEnergySystem())
                return 0;

            return Mathf.Max(0, ItemData.EnergyAmountMax);
        }
    }

    private void Awake()
    {
        if (!GetComponentInChildren<Collider>())
            Debug.LogError("No colliders on item " + name, this);

        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponentInChildren<Collider>();
        _handlePoint = transform.Find("HandlePoint");

        InitializeRuntimeState();
    }

    private void InitializeRuntimeState()
    {
        if (_runtimeStateInitialized)
            return;

        _runtimeStateInitialized = true;

        if (HasEnergySystem())
            _energyAmountCurrent = Mathf.Max(0, ItemData.EnergyAmountMax);
        else
            _energyAmountCurrent = 0;
    }

    public bool HasEnergySystem()
    {
        return ItemData != null && ItemData.UsesEnergy;
    }

    public bool TryRestoreEnergy(int energyAmount, out string error)
    {
        InitializeRuntimeState();

        if (!HasEnergySystem())
        {
            if (energyAmount != 0)
            {
                error =
                    $"Item '{name}' has no energy system, but the save " +
                    $"contains energy value {energyAmount}.";

                return false;
            }

            _energyAmountCurrent = 0;
            error = null;
            return true;
        }

        int maximum = Mathf.Max(0, ItemData.EnergyAmountMax);

        if (energyAmount < 0 || energyAmount > maximum)
        {
            error =
                $"Saved energy for item '{name}' must be between 0 " +
                $"and {maximum}.";

            return false;
        }

        _energyAmountCurrent = energyAmount;
        error = null;
        return true;
    }

    public bool IsEnergyEmpty()
    {
        if (!HasEnergySystem())
            return false;

        return _energyAmountCurrent <= 0;
    }

    public bool IsEnergyFull()
    {
        if (!HasEnergySystem())
            return true;

        return _energyAmountCurrent >= Mathf.Max(0, ItemData.EnergyAmountMax);
    }

    public bool CanUseEnergy()
    {
        if (!HasEnergySystem())
            return true;

        int cost = Mathf.Max(1, ItemData.EnergyCostPerUse);
        return _energyAmountCurrent >= cost;
    }

    public bool TryUseEnergy()
    {
        if (!HasEnergySystem())
            return true;

        int cost = Mathf.Max(1, ItemData.EnergyCostPerUse);

        if (_energyAmountCurrent < cost)
            return false;

        _energyAmountCurrent -= cost;
        _energyAmountCurrent = Mathf.Clamp(_energyAmountCurrent, 0, Mathf.Max(0, ItemData.EnergyAmountMax));

        return true;
    }

    public void ReloadEnergy(int amount)
    {
        if (!HasEnergySystem())
            return;

        _energyAmountCurrent += Mathf.Max(0, amount);
        _energyAmountCurrent = Mathf.Clamp(_energyAmountCurrent, 0, Mathf.Max(0, ItemData.EnergyAmountMax));

    }

    public void FillEnergy()
    {
        if (!HasEnergySystem())
            return;

        _energyAmountCurrent = Mathf.Max(0, ItemData.EnergyAmountMax);

    }

    public int GetEnergyAmountForMax()
    {
        if (!HasEnergySystem())
            return 0;

        return Mathf.Max(0, ItemData.EnergyAmountMax - _energyAmountCurrent);
    }

    /// <summary>
    /// Предмет можно использовать через старый ActionsGroup.
    /// Legacy.
    /// </summary>
    public bool IsUsable()
    {
        var playerUseActionId = System.Guid.Parse("316f217b-db19-4ab3-992d-f06d0052d966");

        return ActionsGroups != null &&
               ActionsGroups.Where(x => x.Actions.Any(z => z.action.id == playerUseActionId)).Any();
    }

    /// <summary>
    /// Использовать предмет через старый ActionsGroup.
    /// Legacy.
    /// </summary>
    public void Use()
    {
        var playerUseActionId = System.Guid.Parse("316f217b-db19-4ab3-992d-f06d0052d966");

        if (ActionsGroups == null)
            return;

        foreach (var action in ActionsGroups.Where(x => x.Actions.Any(z => z.action.id == playerUseActionId)))
            action.Invoke();
    }

    /// <summary>
    /// Состояние в инвентаре/руке/ящике.
    /// </summary>
    public void ToInventoryState()
    {
        if (_rigidbody != null)
        {
#if UNITY_6000_0_OR_NEWER
            _rigidbody.linearVelocity = Vector3.zero;
#else
            _rigidbody.velocity = Vector3.zero;
#endif
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }

        if (_collider != null)
            _collider.enabled = false;
    }

    /// <summary>
    /// Состояние в игровом мире.
    /// </summary>
    public void ToWorldState()
    {
        if (_rigidbody != null)
            _rigidbody.isKinematic = false;

        if (_collider != null)
            _collider.enabled = true;
    }

    public void SetKinematic(bool value)
    {
        if (_rigidbody != null)
            _rigidbody.isKinematic = value;
    }
}
