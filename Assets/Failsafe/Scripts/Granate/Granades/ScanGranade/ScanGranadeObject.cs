using UnityEngine;

public class ScanGranadeObject : GranadeObject
{
    [SerializeField] private Material _materialScan;
    protected override bool HitsChecking(RaycastHit hit, Collider hitInfo)
    {
        if (hitInfo.tag != "Enemy")
        {
            return true;
        }
        Debug.Log($"{hitInfo.gameObject.name}");
        return false;
    }
    protected override void DamagebleExplosionEffect(Collider hitInfo)
    {
        Scaneble scaneble = hitInfo.gameObject.transform.parent.gameObject.AddComponent<Scaneble>();
        if (scaneble != null ) 
            scaneble.ScanHit(Data.LifeTimeOnEnemyEffect, _materialScan);
    }
    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy)
    {
        
    }
}
