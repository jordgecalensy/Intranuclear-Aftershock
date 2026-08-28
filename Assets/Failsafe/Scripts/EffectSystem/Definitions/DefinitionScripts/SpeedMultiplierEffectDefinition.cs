using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.EffectSystem.Effects;
using Failsafe.Scripts.EffectSystem.Targets;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Definitions
{
    [CreateAssetMenu(menuName = "Effects/Movement/Speed Multiplier")]
    public sealed class SpeedMultiplierEffectDefinition : EffectDefinition
    {
        [SerializeField] private float _duration = 3f;

        [SerializeField, Range(0.01f, 10f)]
        private float _multiplier = 0.5f;

        [SerializeField]
        private bool _useContextPowerAsMultiplier;

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

            float duration = context.ResolveDuration(_duration);
            float multiplier = _useContextPowerAsMultiplier
                ? Mathf.Clamp(context.Power, 0.01f, 10f)
                : _multiplier;

            return new SpeedMultiplierEffect(
                target,
                duration,
                multiplier,
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

            return EffectTargetResolver.TryResolve(context, out target);
        }
    }
}
