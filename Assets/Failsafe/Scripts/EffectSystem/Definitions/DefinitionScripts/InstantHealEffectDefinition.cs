using System;
using Failsafe.Scripts.Health;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "InstantHealEffectDefinition",
        menuName = "Failsafe/Effects/Positive/Instant Heal")]
    public sealed class InstantHealEffectDefinition : EffectDefinition
    {
        [Header("Heal")]
        [SerializeField] private float _amount = 25f;
        [SerializeField] private bool _scaleByContextPower = false;

        [Header("Debug")]
        [SerializeField] private bool _logResolveErrors = false;
        [SerializeField] private bool _logApply = false;

        public override bool CanApply(EffectContext context)
        {
            return GetFinalAmount(context) > 0f &&
                   ResolveHealth(context) != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            IHealth health = ResolveHealth(context);

            if (health == null)
                return null;

            return new InstantHealEffect(
                health,
                GetFinalAmount(context),
                _logApply);
        }

        public override string GetStackKey(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target != null)
                return $"positive.instant-heal.{GetInstanceID()}.target.{target.GetInstanceID()}";

            return $"positive.instant-heal.{GetInstanceID()}";
        }

        private float GetFinalAmount(EffectContext context)
        {
            return Mathf.Max(
                0f,
                _scaleByContextPower ? _amount * context.Power : _amount);
        }

        private IHealth ResolveHealth(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target == null)
            {
                if (_logResolveErrors)
                    EffectLog.Warning(EffectLog.Parameters, "[InstantHealEffectDefinition] Target not found.");

                return null;
            }

            LifetimeScope scope = ResolveLifetimeScope(target);

            if (scope == null)
            {
                if (_logResolveErrors)
                {
                    EffectLog.Warning(EffectLog.Parameters,
                        $"[InstantHealEffectDefinition] LifetimeScope not found near target {target.name}.",
                        target);
                }

                return null;
            }

            if (scope.Container == null)
            {
                if (_logResolveErrors)
                {
                    EffectLog.Warning(EffectLog.Parameters,
                        $"[InstantHealEffectDefinition] LifetimeScope container is null on {scope.name}.",
                        scope);
                }

                return null;
            }

            try
            {
                return scope.Container.Resolve<IHealth>();
            }
            catch (Exception e)
            {
                if (_logResolveErrors)
                {
                    EffectLog.Warning(EffectLog.Parameters,
                        $"[InstantHealEffectDefinition] Cannot resolve IHealth from scope {scope.name}. {e.Message}",
                        scope);
                }

                return null;
            }
        }

        private static GameObject ResolveTargetObject(EffectContext context)
        {
            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target != null)
                return target;

            if (context.TargetObject != null)
                return context.TargetObject;

            if (context.HitCollider != null)
                return context.HitCollider.transform.root.gameObject;

            return null;
        }

        private static LifetimeScope ResolveLifetimeScope(GameObject target)
        {
            if (target == null)
                return null;

            return target.GetComponent<LifetimeScope>() ??
                   target.GetComponentInParent<LifetimeScope>() ??
                   target.GetComponentInChildren<LifetimeScope>(true);
        }
    }
}
