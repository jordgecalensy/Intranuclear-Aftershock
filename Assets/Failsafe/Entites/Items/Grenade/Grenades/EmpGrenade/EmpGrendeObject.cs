using UnityEngine;

public class EmpGrendeObject : BaseGrеnadeObject
{
    private Vector3 _direction = new Vector3(0, 0, 0);
    protected override void DamagebleExplosionEffect(Collider hitInfo)
    {
        base.DamagebleExplosionEffect(hitInfo);
        if (Data.OnEnemyEffect != null)
        {
            var OnEnemyEffect = Instantiate(Data.OnEnemyEffect, hitInfo.transform);
            Destroy(OnEnemyEffect, Data.DurationOnEnemyEffect);
        }
        if (hitInfo.GetComponent<Enemy>() != null)
            hitInfo.GetComponent<Enemy>().StunnedState(_direction, Data.DurationOnEnemyEffect);
    }
    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy) { }
}
