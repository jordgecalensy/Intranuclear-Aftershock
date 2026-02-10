using UnityEngine;

public class ExtinquisherShield : MonoBehaviour, IEnterable
{
    [Header("InsertTrigger")]
    [SerializeField] private Transform _holdPoint;
    [SerializeField] private Collider _triggerCollider;

    private void Awake()
    {
        InsertTrigger.GetOrCreate(_triggerCollider.gameObject, this, _holdPoint);
    }

    public void OnEntered()
    {
    }

    public void OnExited()
    {
    }

    public bool IsRightType(Component candidate)
    {
        return candidate.GetComponent<ExtinguisherCarryable>() != null;
    }
}
