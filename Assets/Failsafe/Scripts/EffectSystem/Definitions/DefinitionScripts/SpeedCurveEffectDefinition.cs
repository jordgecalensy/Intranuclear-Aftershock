using System;
using Failsafe.PlayerMovements.Controllers;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "SpeedCurveEffectDefinition",
        menuName = "Failsafe/Effects/Movement/Speed Curve")]
    public class SpeedCurveEffectDefinition : EffectDefinition
    {
        [Header("Speed")]
        [Tooltip("Длительность изменения скорости.")]
        [SerializeField] private float _duration = 3f;

        [Tooltip("Множитель скорости в начале эффекта. 0.5 = скорость в 2 раза ниже.")]
        [SerializeField, Range(0.01f, 3f)] private float _startMultiplier = 0.5f;

        [Tooltip("Множитель скорости в конце эффекта. 1 = нормальная скорость.")]
        [SerializeField, Range(0.01f, 3f)] private float _endMultiplier = 1f;

        [Tooltip("Кривая восстановления. X = время 0..1, Y = сила перехода 0..1.")]
        [SerializeField] private AnimationCurve _curve = new(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));

        [Header("Stacking")]
        [Tooltip("Если true, эффект уникальный и при повторном применении обновляет таймер.")]
        [SerializeField] private bool _unique = true;

        [Tooltip("Стабильный ID модификатора скорости. Если 0, будет использован InstanceID asset'а.")]
        [SerializeField] private int _modifierIdOverride = 0;

        [Header("Debug")]
        [SerializeField] private bool _logResolveErrors = true;

        public override bool CanApply(EffectContext context)
        {
            return ResolveMovementController(context) != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            PlayerMovementController controller = ResolveMovementController(context);

            if (controller == null)
                return null;

            int modifierId = _modifierIdOverride != 0
                ? _modifierIdOverride
                : GetInstanceID();

            return new SpeedCurveEffect(
                controller,
                _duration,
                _startMultiplier,
                _endMultiplier,
                _curve,
                modifierId,
                _unique);
        }

        public override string GetStackKey(EffectContext context)
        {
            int targetId = ResolveTargetId(context);

            if (targetId != 0)
                return $"movement.speed-curve.{GetInstanceID()}.target.{targetId}";

            return $"movement.speed-curve.{GetInstanceID()}";
        }

        private PlayerMovementController ResolveMovementController(EffectContext context)
        {
            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target == null && context.HitCollider != null)
                target = context.HitCollider.transform.root.gameObject;

            if (target == null && context.TargetObject != null)
                target = context.TargetObject;

            if (target == null)
            {
                if (_logResolveErrors)
                    Debug.LogWarning("[SpeedCurveEffectDefinition] Target object not found.");

                return null;
            }

            LifetimeScope scope = ResolveLifetimeScope(target);

            if (scope == null)
            {
                if (_logResolveErrors)
                {
                    Debug.LogWarning(
                        $"[SpeedCurveEffectDefinition] LifetimeScope not found near target {target.name}. " +
                        "Для игрока нужен PlayerLifetimeScope на root или в родителях/детях.",
                        target);
                }

                return null;
            }

            if (scope.Container == null)
            {
                if (_logResolveErrors)
                {
                    Debug.LogWarning(
                        $"[SpeedCurveEffectDefinition] LifetimeScope container is null on {scope.name}.",
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
                        $"[SpeedCurveEffectDefinition] Cannot resolve PlayerMovementController from scope {scope.name}. {e.Message}",
                        scope);
                }

                return null;
            }
        }

        private static LifetimeScope ResolveLifetimeScope(GameObject target)
        {
            if (target == null)
                return null;

            LifetimeScope scope =
                target.GetComponent<LifetimeScope>() ??
                target.GetComponentInParent<LifetimeScope>() ??
                target.GetComponentInChildren<LifetimeScope>(true);

            return scope;
        }

        private static int ResolveTargetId(EffectContext context)
        {
            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target != null)
                return target.GetInstanceID();

            if (context.HitCollider != null)
                return context.HitCollider.GetInstanceID();

            if (context.TargetObject != null)
                return context.TargetObject.GetInstanceID();

            return 0;
        }
    }
}