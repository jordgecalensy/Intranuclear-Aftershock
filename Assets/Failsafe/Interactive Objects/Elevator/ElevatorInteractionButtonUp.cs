using UnityEngine;

public class ElevatorInteractionButtonUp : Interactable
{
    [SerializeField] private ElevatorController _elevator;
    protected override void Interact()
    {
        base.Interact();
        _elevator.OnButtonUpPress();
    }
}
