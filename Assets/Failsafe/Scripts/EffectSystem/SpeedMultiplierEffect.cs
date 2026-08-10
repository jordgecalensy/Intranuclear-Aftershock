using Failsafe.Scripts.EffectSystem.Targets;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Effects
{
    public enum SpeedStackPolicy
    {
        Strongest,
        Weakest,
        IncomingOverrides
    }

    public sealed class SpeedMultiplierEffect : Effect, IReapplicableEffect
    {
        private readonly IMovementSpeedModifierTarget _target;
        private readonly int _modifierId;
        private readonly SpeedStackPolicy _stackPolicy;

        private float _multiplier;

        public SpeedMultiplierEffect(
            IMovementSpeedModifierTarget target,
            float duration,
            float multiplier,
            SpeedStackPolicy stackPolicy = SpeedStackPolicy.Strongest)
        {
            _target = target;
            _duration = Mathf.Max(0f, duration);
            _multiplier = Mathf.Clamp(multiplier, 0.01f, 10f);
            _stackPolicy = stackPolicy;

            IsUniqueEffect = true;
            _modifierId = GetHashCode();
        }

        public override void ApplyEffect()
        {
            _target?.SetSpeedModifier(_modifierId, _multiplier);
        }

        public override void ClearEffect()
        {
            _target?.RemoveSpeedModifier(_modifierId);
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not SpeedMultiplierEffect reapplied)
                return;

            _multiplier = ResolveMultiplier(_multiplier, reapplied._multiplier, _stackPolicy);
            _duration += reapplied._duration;

            _target?.SetSpeedModifier(_modifierId, _multiplier);
        }

        private static float ResolveMultiplier(float current, float incoming, SpeedStackPolicy policy)
        {
            switch (policy)
            {
                case SpeedStackPolicy.IncomingOverrides:
                    return incoming;

                case SpeedStackPolicy.Weakest:
                    return Mathf.Max(current, incoming);

                case SpeedStackPolicy.Strongest:
                default:
                    if (current < 1f || incoming < 1f)
                        return Mathf.Min(current, incoming);

                    return Mathf.Max(current, incoming);
            }
        }
    }
}
