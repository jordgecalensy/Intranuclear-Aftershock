using System;
using Failsafe.Player.Model;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "RestoreStaminaEffectDefinition",
        menuName = "Failsafe/Effects/Positive/Restore Stamina")]
    public sealed class RestoreStaminaEffectDefinition : EffectDefinition
    {
        [Header("Stamina")]
        [SerializeField] private float _amount = 25f;
        [SerializeField] private bool _scaleByContextPower = false;

        [Header("Debug")]
        [SerializeField] private bool _logResolveErrors = false;
        [SerializeField] private bool _logApply = false;

        public override bool CanApply(EffectContext context)
        {
            return GetFinalAmount(context) > 0f &&
                   ResolveStamina(context) != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            IStamina stamina = ResolveStamina(context);

            if (stamina == null)
                return null;

            return new RestoreStaminaEffect(
                stamina,
                GetFinalAmount(context),
                _logApply);
        }

        public override string GetStackKey(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target != null)
                return $"positive.restore-stamina.{GetInstanceID()}.target.{target.GetInstanceID()}";

            return $"positive.restore-stamina.{GetInstanceID()}";
        }

        private float GetFinalAmount(EffectContext context)
        {
            return Mathf.Max(
                0f,
                _scaleByContextPower ? _amount * context.Power : _amount);
        }

        private IStamina ResolveStamina(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target == null)
            {
                if (_logResolveErrors)
                    Debug.LogWarning("[RestoreStaminaEffectDefinition] Target not found.");

                return null;
            }

            LifetimeScope scope = ResolveLifetimeScope(target);

            if (scope == null)
            {
                if (_logResolveErrors)
                {
                    Debug.LogWarning(
                        $"[RestoreStaminaEffectDefinition] LifetimeScope not found near target {target.name}.",
                        target);
                }

                return null;
            }

            if (scope.Container == null)
            {
                if (_logResolveErrors)
                {
                    Debug.LogWarning(
                        $"[RestoreStaminaEffectDefinition] LifetimeScope container is null on {scope.name}.",
                        scope);
                }

                return null;
            }

            try
            {
                return scope.Container.Resolve<IStamina>();
            }
            catch (Exception e)
            {
                if (_logResolveErrors)
                {
                    Debug.LogWarning(
                        $"[RestoreStaminaEffectDefinition] Cannot resolve IStamina from scope {scope.name}. {e.Message}",
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
