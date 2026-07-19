using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;

public class StasisGrenadeObject : BaseGrеnadeObject
{
    [SerializeField] private EffectBundle _explosionEffects;

    [Inject] private IEffectApplicationService _effects;

    protected override void DamagebleExplosionEffect(Collider hitInfo)
    {
    }

    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy)
    {
        if (hitInfo == null)
            return;

        Vector3 direction = directionToEnemy.sqrMagnitude > 0.0001f
            ? directionToEnemy.normalized
            : (hitInfo.transform.position - transform.position).normalized;

        var context = new EffectContext(
            gameObject,
            hitInfo,
            hitInfo.ClosestPoint(transform.position),
            -direction,
            direction);

        _effects?.Apply(_explosionEffects, context);
    }
}