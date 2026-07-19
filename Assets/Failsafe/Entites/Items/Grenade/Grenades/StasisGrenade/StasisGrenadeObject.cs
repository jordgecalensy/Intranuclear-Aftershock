using Failsafe.Scripts.EffectSystem;
using UnityEngine;

public class StasisGrenadeObject : BaseGrеnadeObject
{
    [SerializeField] private EffectBundle _explosionEffects;

    protected override EffectBundle ResolveExplosionEffects()
    {
        return _explosionEffects != null
            ? _explosionEffects
            : base.ResolveExplosionEffects();
    }

    protected override void DamagebleExplosionEffect(Collider hitInfo)
    {
    }

    protected override void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy)
    {
    }
}
