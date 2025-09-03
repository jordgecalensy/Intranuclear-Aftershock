using UnityEngine;

public class ElevatorInteractionButtonDown : Interactable
{
    [SerializeField] private ElevatorController _elevator;
    protected override void Interact()
    {
        base.Interact();
        _elevator.OnButtonDownPress();
    }
}
