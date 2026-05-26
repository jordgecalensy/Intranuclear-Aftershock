using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;

public class ScanGrenadeObject : BaseGrеnadeObject
{
    [SerializeField] private Material _materialScan;
    protected override void DamagebleExplosionEffect(Collider hitInfo)
    {
        if (hitInfo.gameObject.transform.parent == null) return;
        Scaneble scaneble = hitInfo.gameObject.transform.parent.gameObject.AddComponent<Scaneble>();
        if (DamagedObjects.Contains(scaneble.gameObject)) return;
        if (scaneble != null && _materialScan != null)
            scaneble.ScanHit(Data.DurationOnEnemyEffect, _materialScan);
        DamagedObjects.Add(scaneble.gameObject);
    }
    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy)
    {
        
    }
}
