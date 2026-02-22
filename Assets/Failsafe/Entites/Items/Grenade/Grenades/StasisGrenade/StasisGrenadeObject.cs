using UnityEngine;

public class StasisGrenadeObject : GrеnadeObject
{
    [SerializeField] private bool _ifDefaultStasisMode;
    protected override void DamagebleExplosionEffect(Collider hitInfo)
    {

    }
    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy)
    {
        Debug.Log($"Rigidbody: {hitInfo.name}");
        if (hitInfo.GetComponentInParent<Stasisable>() != null)
        {
            hitInfo.GetComponentInParent<Stasisable>().StasisHit(Data.DurationOnEnemyEffect, _ifDefaultStasisMode);
        }
    }
}
