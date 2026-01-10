using UnityEngine;
using TMPro;

public class TMPDropdownItemInteractable : Interactable
{
    private TMP_Dropdown _dropdown;
    private int _index;
    private TMPDropdownRaycastBridge _bridge;

    public void Init(TMP_Dropdown dropdown, int index, TMPDropdownRaycastBridge bridge)
    {
        _dropdown = dropdown;
        _index = index;
        _bridge = bridge;
    }

    protected override void Interact()
    {
        if (_dropdown == null) return;

        _dropdown.value = _index;
        _dropdown.RefreshShownValue();
        _dropdown.Hide();

        if (_bridge != null)
            _bridge.ForceClose(); // важно: убрать reserved space
    }
}
