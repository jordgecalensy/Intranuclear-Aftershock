using UnityEngine;

public class CallElevatorButton : Interactable
{
    [SerializeField] private ElevatorController _elevator;
    [SerializeField] private int _elevatorFloor;
    protected override void Interact()
    {
        base.Interact();
        _elevator.CallElevatorButton(_elevatorFloor - 1);
    }
}
