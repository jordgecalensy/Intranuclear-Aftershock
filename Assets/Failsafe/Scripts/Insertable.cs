using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public class Insertable : MonoBehaviour
{
    private PhysicsController _physicsController;
    private bool _inInsertTrigger = false;
    private Transform _holderTransform;
    private IEnterable _charger;
    private IInsertable _owner;

    public bool IsInserted => _physicsController.IsInserted;
    public bool IsGrabbed => _physicsController.IsGrabbed;
    public bool IsInInsertTrigger => _inInsertTrigger;

    private void Awake()
    {
        _physicsController = PhysicsController.Create(gameObject);
        _owner = GetComponent<IInsertable>();
    }

    private void FixedUpdate()
    {
        if (_inInsertTrigger && !IsGrabbed && !IsInserted)
        {
            _physicsController.Insert(_holderTransform, 2f);
            _charger.OnEntered();
            _owner?.OnInserted();
        }
        else if (IsGrabbed && IsInserted)
        {
            _physicsController.Eject();
            _charger.OnExited();
            _owner?.OnEjected();
            _owner = null;
            _charger = null;
        }
    }

    public void EnterTrigger(Transform holderTransform, IEnterable charger)
    {
        _inInsertTrigger = true;
        _holderTransform = holderTransform;
        _charger = charger;
    }

    public void ExitTrigger()
    {
        _inInsertTrigger = false;
    }
}
