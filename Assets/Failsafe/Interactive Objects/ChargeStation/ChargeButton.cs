using UnityEngine;

public class ChargeButton : Interactable
{
    [SerializeField] private ChargeStation _station;

    protected override void Interact(PlayerInteractionContext context)
    {
        if (_station == null)
        {
            Debug.LogError("[ChargeButton] ChargeStation не назначена.", this);
            return;
        }

        _station.OnButtonPress(context);
    }
}