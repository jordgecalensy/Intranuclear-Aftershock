using UnityEngine;

public class EmpGrendeObject : BaseGrеnadeObject
{
    private Vector3 _direction = new Vector3(0, 0, 0);

    protected override void OnBundleExplosionEffectApplied(Collider hitInfo, Vector3 directionToEnemy)
    {
        CreateOnEnemyEffect(hitInfo);
    }

    protected override void DamagebleExplosionEffect(Collider hitInfo)
    {
        base.DamagebleExplosionEffect(hitInfo);
        CreateOnEnemyEffect(hitInfo);

        if (hitInfo.GetComponent<Enemy>() != null)
            hitInfo.GetComponent<Enemy>().StunnedState(_direction, Data.DurationOnEnemyEffect);
    }

    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy) { }

    private void CreateOnEnemyEffect(Collider hitInfo)
    {
        if (Data.OnEnemyEffect == null)
            return;

        var onEnemyEffect = Instantiate(Data.OnEnemyEffect, hitInfo.transform);
        Destroy(onEnemyEffect, Data.DurationOnEnemyEffect);
    }
}
