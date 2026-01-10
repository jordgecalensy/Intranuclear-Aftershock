using UnityEngine;
using TMPro;

public class TMPDropdownInteractable : Interactable
{
    [SerializeField] private TMP_Dropdown _dropdown;
    [SerializeField] private TMPDropdownRaycastBridge _bridge;

    private void Reset()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
        _bridge = GetComponent<TMPDropdownRaycastBridge>();
    }

    protected override void Interact()
    {
        if (_dropdown == null || _bridge == null) return;

        if (_bridge.IsOpen())
        {
            _bridge.ForceClose();
            return;
        }

        _dropdown.Show();
        _bridge.OnShowCalled();
    }
}
