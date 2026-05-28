using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.EffectSystem.Effects;
using Failsafe.Scripts.EffectSystem.Targets;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem.Definitions
{
    [CreateAssetMenu(menuName = "Effects/Movement/Speed Multiplier")]
    public sealed class SpeedMultiplierEffectDefinition : EffectDefinition
    {
        [SerializeField] private float _duration = 3f;

        [SerializeField, Range(0.01f, 10f)]
        private float _multiplier = 0.5f;

        [SerializeField]
        private SpeedStackPolicy _stackPolicy = SpeedStackPolicy.Strongest;

        public override bool CanApply(EffectContext context)
        {
            return TryResolveTarget(context, out _);
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!TryResolveTarget(context, out var target))
                return null;

            return new SpeedMultiplierEffect(
                target,
                _duration,
                _multiplier,
                _stackPolicy);
        }

        public override string GetStackKey(EffectContext context)
        {
            return "movement.speed_multiplier";
        }

        private static bool TryResolveTarget(
            EffectContext context,
            out IMovementSpeedModifierTarget target)
        {
            target = null;

            if (context.TryGet<IMovementSpeedModifierTarget>(out var componentTarget))
            {
                target = componentTarget;
                return true;
            }

            if (context.HitCollider == null)
                return false;

            LifetimeScope scope = context.HitCollider.GetComponentInParent<LifetimeScope>();

            if (scope == null)
                return false;

            if (scope.Container == null)
                return false;

            try
            {
                target = scope.Container.Resolve<PlayerMovementController>();
                return target != null;
            }
            catch
            {
                return false;
            }
        }
    }
}