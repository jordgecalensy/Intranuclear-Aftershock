// Начальная точка
using UnityEngine;

public class PowerSource : PowerNode
{
    private bool _isEnable = true;
    [SerializeField]
    private ElectricalPanelScript _electricalPanel;

    private void Start()
    {
        // Например, питание запускается автоматически или после починки
        if (_electricalPanel != null) return;
        StartPower();
    }
    public void SetEnable(bool isEnable)
    {
        SetEnabledState(isEnable);

        var manager = FindFirstObjectByType<PowerNetworkManager>();
        if (manager != null)
        {
            manager.RefreshPower();
        }
        else
        {
            Debug.LogWarning(
                "[POWER-NET] PowerNetworkManager not found in scene.");
        }
    }

    internal void RestoreEnabledState(bool isEnable)
    {
        SetEnabledState(isEnable);
    }

    public override void StartPower()
    {
        if (!_isEnable) return;
        base.StartPower();
    }

    private void SetEnabledState(bool isEnable)
    {
        _isEnable = isEnable;
    }
}
