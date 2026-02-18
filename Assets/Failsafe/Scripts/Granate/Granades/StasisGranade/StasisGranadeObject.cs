using UnityEngine;

public class StasisGranadeObject : GranadeObject
{
    [SerializeField] private bool _ifDefaultStasisMode;
    protected override void DamagebleExplosionEffect(Collider hitInfo)
    {

    }
    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy)
    {
        if (hitInfo.GetComponent<Rigidbody>() == null) return;
        Debug.Log($"Rigidbody: {hitInfo.name}");
        if (hitInfo.GetComponentInParent<Stasisable>() != null)
        {
            hitInfo.GetComponentInParent<Stasisable>().StasisHit(Data.LifeTimeOnEnemyEffect, _ifDefaultStasisMode);
        }
    }
}
