using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public sealed class InsertTrigger : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    private ExtinguisherInsertable _currentInsertable;

    private void OnTriggerEnter(Collider other)
    {
        _currentInsertable = other.GetComponent<ExtinguisherInsertable>();
        if (_currentInsertable == null) return;
        if (_currentInsertable.IsInInsertTrigger) return;
        _currentInsertable.OnInserted(holdPoint);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_currentInsertable != null && other.GetComponent<ExtinguisherInsertable>() == _currentInsertable)
        {
            _currentInsertable.OnEjected();
            _currentInsertable = null;
        }
    }
}