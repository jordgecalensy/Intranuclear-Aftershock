using UnityEngine;

public class ChargeButton : Interactable
{
    [SerializeField] private ChargeStation _station;
    protected override void Interact()
    {
        base.Interact();
        _station.OnButtonPress();
    }
}
