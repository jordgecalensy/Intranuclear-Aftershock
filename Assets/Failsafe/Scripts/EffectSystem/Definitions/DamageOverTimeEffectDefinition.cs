using Failsafe.Scripts.Damage;
using Failsafe.Scripts.EffectSystem.Effects;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Definitions
{
    [CreateAssetMenu(menuName = "Effects/Damage/Damage Over Time")]
    public sealed class DamageOverTimeEffectDefinition : EffectDefinition
    {
        [SerializeField] private DamageType _damageType = DamageType.Fire;
        [SerializeField] private float _duration = 4f;
        [SerializeField] private float _damagePerTick = 2f;
        [SerializeField] private float _tickInterval = 1f;
        [SerializeField] private bool _scaleByContextPower = false;

        public override bool CanApply(EffectContext context)
        {
            return DamageTargetResolver.TryResolve(context, out _);
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!DamageTargetResolver.TryResolve(context, out DamageTarget target))
                return null;

            return new DamageOverTimeEffect(
                target,
                _damageType,
                _duration,
                _damagePerTick,
                _tickInterval,
                context.Source,
                context.Power,
                _scaleByContextPower);
        }

        public override string GetStackKey(EffectContext context)
        {
            return $"damage.dot.{_damageType}";
        }
    }
}