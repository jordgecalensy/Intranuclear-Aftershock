using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public sealed class ElectroBattaryInsertable : MonoBehaviour, IInsertable
{
    private PhysicsController _physicsController;
    private bool _inInsertTrigger = false;
    private Transform _holderTransform;
    private IEnterable _charger;

    public bool IsInserted => _physicsController.IsInserted;
    public bool IsGrabbed => _physicsController.IsGrabbed;
    public bool IsInInsertTrigger => _inInsertTrigger;

    private void Awake()
    {
        _physicsController = PhysicsController.Create(gameObject);
    }

    private void FixedUpdate()
    {
        if (_inInsertTrigger && !IsGrabbed && !IsInserted)
        {
            _physicsController.Insert(_holderTransform, 2f);
            _charger.OnEntered();
        }
        else if (IsGrabbed && IsInserted)
        {
            _physicsController.Eject();
            _charger.OnExited();
            _charger = null;
            Debug.Log("Exited");
        }
    }

    public void OnInserted(Transform holderTransform, IEnterable charger)
    {
        _inInsertTrigger = true;
        _holderTransform = holderTransform;
        _charger = charger;
    }

    public void OnEjected()
    {
        _inInsertTrigger = false;
    }
}