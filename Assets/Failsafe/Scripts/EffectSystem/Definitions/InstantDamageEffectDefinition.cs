using Failsafe.Scripts.Damage;
using Failsafe.Scripts.EffectSystem.Effects;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Definitions
{
    [CreateAssetMenu(menuName = "Effects/Damage/Instant Damage")]
    public sealed class InstantDamageEffectDefinition : EffectDefinition
    {
        [SerializeField] private float _amount = 10f;
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [SerializeField] private DamageApplicationKind _applicationKind = DamageApplicationKind.Instant;
        [SerializeField] private bool _scaleByContextPower = false;

        public override bool CanApply(EffectContext context)
        {
            return DamageTargetResolver.TryResolve(context, out _);
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!DamageTargetResolver.TryResolve(context, out DamageTarget target))
                return null;

            float finalAmount = _scaleByContextPower
                ? _amount * context.Power
                : _amount;

            var damage = new DamageInfo(
                finalAmount,
                _damageType,
                _applicationKind,
                context.Source,
                context.Point,
                context.Direction,
                context.Power);

            return new InstantDamageEffect(target, damage);
        }
    }
}