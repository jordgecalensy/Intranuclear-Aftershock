using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public sealed class InsertTrigger : MonoBehaviour
{
    private Transform _holdPoint;
    private IEnterable _charger;
    private Insertable _currentInsertable;

    public static InsertTrigger GetOrCreate(GameObject parent, IEnterable charger, Transform holdPoint)
    {
        var trigger = parent.GetComponent<InsertTrigger>();
        if (trigger != null) return trigger;
        trigger = parent.AddComponent<InsertTrigger>();
        trigger._charger = charger;
        trigger._holdPoint = holdPoint;
        return trigger;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_charger.IsRightType(other)) return;
        _currentInsertable = other.GetComponent<Insertable>();
        if (_currentInsertable == null) return;
        if (_currentInsertable.IsInInsertTrigger) return;
        _currentInsertable.EnterTrigger(_holdPoint, _charger);
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