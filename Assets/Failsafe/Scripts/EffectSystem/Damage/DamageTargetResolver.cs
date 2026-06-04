using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.Health;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.Damage
{
    public readonly struct DamageTarget
    {
        private readonly IDamageable _damageable;
        private readonly IHealth _health;

        public readonly GameObject GameObject;

        public bool IsValid => _damageable != null || _health != null;

        public DamageTarget(
            IDamageable damageable,
            IHealth health,
            GameObject gameObject)
        {
            _damageable = damageable;
            _health = health;
            GameObject = gameObject;
        }

        public void TakeDamage(DamageInfo damage)
        {
            if (!IsValid)
                return;

            if (_health != null)
            {
                if (_health.IsDead)
                    return;

                if (damage.Amount <= 0f)
                    return;

                _health.AddHealth(-damage.Amount);
                return;
            }

            _damageable?.TakeDamage(damage);
        }
    }

    public static class DamageTargetResolver
    {
        public static bool TryResolve(EffectContext context, out DamageTarget target)
        {
            target = default;

            if (context.HitCollider == null)
                return false;

            return TryResolve(context.HitCollider, out target);
        }

        public static bool TryResolve(Collider collider, out DamageTarget target)
        {
            target = default;

            if (collider == null)
                return false;

            if (TryResolveHealthFromScope(collider, out target))
                return true;

            if (TryResolveDamageable(collider, out target))
                return true;

            return false;
        }

        private static bool TryResolveHealthFromScope(Collider collider, out DamageTarget target)
        {
            target = default;

            LifetimeScope scope = FindClosestScope(collider);

            if (scope == null || scope.Container == null)
                return false;

            try
            {
                IHealth health = scope.Container.Resolve<IHealth>();

                if (health == null)
                    return false;

                target = new DamageTarget(
                    null,
                    health,
                    GetTargetObject(collider));

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveDamageable(Collider collider, out DamageTarget target)
        {
            target = default;

            IDamageable damageable = collider.GetComponentInParent<IDamageable>();

            if (damageable == null && collider.attachedRigidbody != null)
                damageable = collider.attachedRigidbody.GetComponentInChildren<IDamageable>();

            if (damageable == null)
                damageable = collider.transform.root.GetComponentInChildren<IDamageable>();

            if (damageable == null)
                return false;

            target = new DamageTarget(
                damageable,
                null,
                GetTargetObject(collider));

            return true;
        }

        private static LifetimeScope FindClosestScope(Collider collider)
        {
            LifetimeScope scope = collider.GetComponentInParent<LifetimeScope>();

            if (scope != null)
                return scope;

            if (collider.attachedRigidbody != null)
            {
                scope = collider.attachedRigidbody.GetComponentInParent<LifetimeScope>();

                if (scope != null)
                    return scope;
            }

            return collider.transform.root.GetComponentInChildren<LifetimeScope>();
        }

        private static GameObject GetTargetObject(Collider collider)
        {
            if (collider.attachedRigidbody != null)
                return collider.attachedRigidbody.gameObject;

            return collider.gameObject;
        }
    }
}