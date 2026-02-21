using UnityEngine;

public class TMPDropdownCustomHeaderInteractable : Interactable
{
    [SerializeField] private TMPDropdownRoot _dropdown;

    private void Reset()
    {
        if (_dropdown == null) _dropdown = GetComponentInParent<TMPDropdownRoot>();
    }

    public void Bind(TMPDropdownRoot dropdown)
    {
        _dropdown = dropdown;
    }

    protected override void Interact()
    {
        if (_dropdown == null) return;
        _dropdown.Toggle();
    }
}
