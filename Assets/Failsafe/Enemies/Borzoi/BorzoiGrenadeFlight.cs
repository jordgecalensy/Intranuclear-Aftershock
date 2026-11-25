using System.Collections.Generic;
using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

public sealed class BorzoiGrenadeFlight : MonoBehaviour
{
    private static readonly HashSet<BorzoiGrenadeFlight> Active = new();

    private Rigidbody _body;
    private PhysicsController _carry;
    private Collider[] _ownerColliders;
    private Collider[] _grenadeColliders;
    private bool _settleOnLanding;

    internal static IReadOnlyCollection<BorzoiGrenadeFlight> ActiveGrenades => Active;
    internal Rigidbody Body => _body;
    internal bool CanBeCaught => _body != null && _carry != null && !_carry.IsGrabbed;

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    public void Launch(Rigidbody body, Collider[] ownerColliders, Vector3 velocity)
    {
        _body = body;
        _ownerColliders = ownerColliders;
        _grenadeColliders = GetComponentsInChildren<Collider>();
        _carry = PhysicsController.GetOrCreate(gameObject);
        _carry.Grabbed += OnGrabbed;
        _settleOnLanding = true;
        SetIgnoreOwner(true);
        _body.linearVelocity = velocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_settleOnLanding || collision.contactCount == 0 || collision.GetContact(0).normal.y < 0.5f)
            return;

        _settleOnLanding = false;
        _body.linearVelocity = Vector3.zero;
        _body.angularVelocity = Vector3.zero;
    }

    private void OnGrabbed()
    {
        _settleOnLanding = false;
        _body.linearVelocity = Vector3.zero;
        _body.WakeUp();
        SetIgnoreOwner(false);
        _carry.Grabbed -= OnGrabbed;
    }

    private void OnDestroy()
    {
        if (_carry != null)
            _carry.Grabbed -= OnGrabbed;
    }

    private void SetIgnoreOwner(bool ignore)
    {
        foreach (var grenadeCollider in _grenadeColliders)
        {
            foreach (var ownerCollider in _ownerColliders)
            {
                if (ownerCollider != null && ownerCollider.enabled && ownerCollider.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(grenadeCollider, ownerCollider, ignore);
            }
        }
    }
}
