using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public sealed class ElectroShieldInsertTrigger : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    private ElectroShieldInsertable _currentInsertable;

    private void OnTriggerEnter(Collider other)
    {
        _currentInsertable = other.GetComponent<ElectroShieldInsertable>();
        if (_currentInsertable == null) return;
        if (_currentInsertable.IsInInsertTrigger) return;
        _currentInsertable.OnInserted(holdPoint);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_currentInsertable != null && other.GetComponent<ElectroShieldInsertable>() == _currentInsertable)
        {
            _currentInsertable.OnEjected();
            _currentInsertable = null;
        }
    }
}