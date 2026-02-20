using UnityEngine;

public class EmpGrandeObject : GranadeObject
{
    private Vector3 _direction = new Vector3(0, 0, 0);
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
        base.DamagebleExplosionEffect(hitInfo);
        Debug.Log("on enemy effect");
        var OnEnemyEffect = Instantiate(Data.OnEnemyEffect, hitInfo.transform);
        Destroy(OnEnemyEffect, Data.LifeTimeOnEnemyEffect);
        if (hitInfo.GetComponent<Enemy>() != null)
            hitInfo.GetComponent<Enemy>().StunnedState(_direction, Data.LifeTimeOnEnemyEffect);
    }
    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy) { }
}
