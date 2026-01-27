using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public sealed class ExtinguisherInsertable : MonoBehaviour, IInsertable
{
    
    // [Header("Extinguisher")]
    private ExtinguisherCarryable extinguisherCarryable;
    private PhysicsController _physicsController;
    private bool _inInsertTrigger = false;
    private Transform _holderTransform;

    public bool IsInserted => _physicsController.IsInserted;
    public bool IsGrabbed => _physicsController.IsGrabbed;
    public bool IsInInsertTrigger => _inInsertTrigger;

    private void Awake()
    {
        _physicsController = PhysicsController.Create(gameObject);
        extinguisherCarryable = GetComponent<ExtinguisherCarryable>();
    }

    private void FixedUpdate()
    {
        if (_inInsertTrigger && !IsGrabbed && !IsInserted)
        {
            _physicsController.Insert(_holderTransform, 2f);
            Debug.Log("Inserted extinguisher into holder via FixedUpdate");
        }
        else if (IsGrabbed && IsInserted)
        {
            _physicsController.Eject();
        }
    }

    public void OnInserted(Transform holderTransform)
    {
        if (extinguisherCarryable != null)
        {
            extinguisherCarryable.OnUseStop();
        }
        _inInsertTrigger = true;
        _holderTransform = holderTransform;
    }

    public void OnEjected()
    {
        _inInsertTrigger = false;
    }
}