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
        if (hitInfo.gameObject.transform.parent == null) return;
        Scaneble scaneble = hitInfo.gameObject.transform.parent.gameObject.AddComponent<Scaneble>();
        if (scaneble != null && _materialScan != null) 
            scaneble.ScanHit(Data.DurationOnEnemyEffect, _materialScan);
    }
    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy)
    {
        
    }
}
