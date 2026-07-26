using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public sealed class InsertTrigger : MonoBehaviour
{
    private Transform _holdPoint;
    private IEnterable _charger;
    private Insertable _currentInsertable;

    internal Insertable CurrentInsertable => _currentInsertable;

    public static InsertTrigger GetOrCreate(GameObject parent, IEnterable charger, Transform holdPoint)
    {
        if (parent == null)
            throw new System.ArgumentNullException(nameof(parent));

        if (charger == null)
            throw new System.ArgumentNullException(nameof(charger));

        if (holdPoint == null)
            throw new System.ArgumentNullException(nameof(holdPoint));

        var trigger = parent.GetComponent<InsertTrigger>();
        if (trigger == null)
            trigger = parent.AddComponent<InsertTrigger>();

        trigger._charger = charger;
        trigger._holdPoint = holdPoint;
        return trigger;
    }

    internal void RestoreInserted(Insertable insertable)
    {
        if (insertable == null)
            throw new System.ArgumentNullException(nameof(insertable));

        if (_charger == null || _holdPoint == null)
        {
            throw new System.InvalidOperationException(
                $"Insert trigger '{name}' is not configured.");
        }

        if (_currentInsertable != null &&
            _currentInsertable != insertable)
        {
            throw new System.InvalidOperationException(
                $"Insert trigger '{name}' already contains another object.");
        }

        if (insertable.IsInInsertTrigger &&
            _currentInsertable != insertable)
        {
            throw new System.InvalidOperationException(
                $"Insertable '{insertable.name}' already belongs to another trigger.");
        }

        _currentInsertable = insertable;
        insertable.RestoreInserted(_holdPoint, _charger);
    }

    internal void RestoreEmpty()
    {
        if (_currentInsertable == null)
            return;

        _currentInsertable.RestoreOutsideTrigger();
        _currentInsertable = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_charger.IsRightType(other)) return;

        Insertable candidate = other.GetComponent<Insertable>();
        if (candidate == null || candidate.IsInInsertTrigger)
            return;

        if (_currentInsertable != null &&
            _currentInsertable != candidate)
        {
            return;
        }

        _currentInsertable = candidate;
        candidate.EnterTrigger(_holdPoint, _charger);
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
