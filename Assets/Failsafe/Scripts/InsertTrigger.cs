using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public sealed class InsertTrigger : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private GameObject charger;
    private Insertable _currentInsertable;
    private IEnterable _charger;

    private void Awake()
    {
        _charger = charger.GetComponent<IEnterable>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_charger.IsRightType(other)) return;
        _currentInsertable = other.GetComponent<Insertable>();
        if (_currentInsertable == null) return;
        if (_currentInsertable.IsInInsertTrigger) return;
        _currentInsertable.EnterTrigger(holdPoint, _charger);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_currentInsertable != null && other.GetComponent<Insertable>() == _currentInsertable)
        {
            _currentInsertable.ExitTrigger();
            _currentInsertable = null;
        }
    }
}