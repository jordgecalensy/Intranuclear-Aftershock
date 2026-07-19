using System;
using Failsafe.PlayerMovements.Controllers;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "StagedSpeedModifierEffectDefinition",
        menuName = "Failsafe/Effects/Movement/Staged Speed Modifier")]
    public class StagedSpeedModifierEffectDefinition : EffectDefinition
    {
        [Header("Observed Status")]
        [SerializeField] private StatusEffectType _observedStatus = StatusEffectType.Cold;

        [Tooltip("Длительность эффекта. Обычно ставь такую же, как у staged status.")]
        [SerializeField] private float _duration = 5f;

        [Tooltip("Если true, эффект сам выключится, когда observed status исчезнет.")]
        [SerializeField] private bool _clearWhenStatusMissing = true;

        [Header("Stages")]
        [SerializeField] private StagedSpeedModifierStage[] _stageModifiers =
        {
            new StagedSpeedModifierStage(),
            new StagedSpeedModifierStage(),
            new StagedSpeedModifierStage()
        };

        [Header("Stacking")]
        [SerializeField] private bool _unique = true;

        [Tooltip("Стабильный ID модификатора скорости. Если 0, будет использован InstanceID asset'а.")]
        [SerializeField] private int _modifierIdOverride = 0;

        [Header("Debug")]
        [SerializeField] private bool _logResolveErrors = true;

        public override bool CanApply(EffectContext context)
        {
            if (!TryResolveStatusState(context, out StatusEffectState state))
                return false;

            if (state == null)
                return false;

            if (_clearWhenStatusMissing && !state.HasStatus(_observedStatus))
                return false;

            return ResolveMovementController(context) != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!TryResolveStatusState(context, out StatusEffectState state))
                return null;

            PlayerMovementController controller = ResolveMovementController(context);

            if (state == null || controller == null)
                return null;

            int modifierId = _modifierIdOverride != 0
                ? _modifierIdOverride
                : GetInstanceID();

            return new StagedSpeedModifierEffect(
                state,
                controller,
                _observedStatus,
                _duration,
                _stageModifiers,
                modifierId,
                _unique,
                _clearWhenStatusMissing);
        }

        public override string GetStackKey(EffectContext context)
        {
            if (TryResolveStatusState(context, out StatusEffectState state) && state != null)
                return $"movement.staged-speed.{_observedStatus}.{GetInstanceID()}.{state.GetInstanceID()}";

            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target != null)
                return $"movement.staged-speed.{_observedStatus}.{GetInstanceID()}.target.{target.GetInstanceID()}";

            if (context.HitCollider != null)
                return $"movement.staged-speed.{_observedStatus}.{GetInstanceID()}.collider.{context.HitCollider.GetInstanceID()}";

            return $"movement.staged-speed.{_observedStatus}.{GetInstanceID()}";
        }

        private static bool TryResolveStatusState(
            EffectContext context,
            out StatusEffectState state)
        {
            return StatusEffectStateResolver.TryResolve(
                context,
                autoAdd: false,
                out state);
        }

        private PlayerMovementController ResolveMovementController(EffectContext context)
        {
            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target == null && context.HitCollider != null)
                target = context.HitCollider.transform.root.gameObject;

            if (target == null)
            {
                if (_logResolveErrors)
                    Debug.LogWarning("[StagedSpeedModifierEffectDefinition] Target object not found.");

                return null;
            }

            LifetimeScope scope = ResolveLifetimeScope(target);

            if (scope == null)
            {
                if (_logResolveErrors)
                {
                    Debug.LogWarning(
                        $"[StagedSpeedModifierEffectDefinition] LifetimeScope not found near target {target.name}.",
                        target);
                }

                return null;
            }

            if (scope.Container == null)
            {
                if (_logResolveErrors)
                {
                    Debug.LogWarning(
                        $"[StagedSpeedModifierEffectDefinition] LifetimeScope container is null on {scope.name}.",
                        scope);
                }

                return null;
            }

            try
            {
                return scope.Container.Resolve<PlayerMovementController>();
            }
            catch (Exception e)
            {
                if (_logResolveErrors)
                {
                    Debug.LogWarning(
                        $"[StagedSpeedModifierEffectDefinition] Cannot resolve PlayerMovementController from scope {scope.name}. {e.Message}",
                        scope);
                }

                return null;
            }
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