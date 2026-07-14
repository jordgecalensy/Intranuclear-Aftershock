using Failsafe.Scripts.Damage;
using Failsafe.Scripts.EffectSystem;

namespace Failsafe.Scripts.EffectSystem.Effects
{
    public sealed class InstantDamageEffect : Effect
    {
        private readonly DamageTarget _target;
        private readonly DamageInfo _damage;
        private readonly bool _ignoreResistance;
        private readonly bool _logResistance;

        public InstantDamageEffect(
            DamageTarget target,
            DamageInfo damage,
            bool ignoreResistance = false,
            bool logResistance = false)
        {
            _target = target;
            _damage = damage;
            _ignoreResistance = ignoreResistance;
            _logResistance = logResistance;

            _duration = 0f;
            IsUniqueEffect = false;
        }

        public override void ApplyEffect()
        {
            DamageResistanceUtility.ApplyDamage(
                _target,
                _damage,
                _ignoreResistance,
                _logResistance);
        }

        public override void ClearEffect()
        {
        }
    }
}