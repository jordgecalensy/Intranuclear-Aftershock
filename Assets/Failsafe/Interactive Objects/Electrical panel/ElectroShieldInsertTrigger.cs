using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public sealed class ElectroShieldInsertTrigger : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private GameObject charger;
    private ElectroBattaryInsertable _currentInsertable;
    private IEnterable _charger;

    private void Awake()
    {
        _charger = charger.GetComponent<IEnterable>();
    }

    private void OnTriggerEnter(Collider other)
    {
        _currentInsertable = other.GetComponent<ElectroBattaryInsertable>();
        if (_currentInsertable == null) return;
        if (_currentInsertable.IsInInsertTrigger) return;
        _currentInsertable.OnInserted(holdPoint, _charger);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_currentInsertable != null && other.GetComponent<ElectroBattaryInsertable>() == _currentInsertable)
        {
            _currentInsertable.OnEjected();
            _currentInsertable = null;
        }
    }
}