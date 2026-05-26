using UnityEngine;

public class ExtinquisherShield : MonoBehaviour, IEnterable
{
    [Header("InsertTrigger")]
    [SerializeField] private Transform _holdPoint;
    [SerializeField] private Collider _triggerCollider;
    private bool _isEmpty = true;

    private void Awake()
    {
        InsertTrigger.GetOrCreate(_triggerCollider.gameObject, this, _holdPoint);
    }

    public void OnEntered()
    {
        _isEmpty = false;
    }

    public void OnExited()
    {
        _isEmpty = true;
    }

    public bool IsRightType(Component candidate)
    {
        return candidate.GetComponent<ExtinguisherCarryable>() != null;
    }

    public bool IsEmpty()
    {
        return _isEmpty;
    }
}
