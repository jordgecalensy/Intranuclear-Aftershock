using Failsafe.Scripts.Damage;

namespace Failsafe.Scripts.EffectSystem.Effects
{
    public sealed class InstantDamageEffect : Effect
    {
        private readonly DamageTarget _target;
        private readonly DamageInfo _damage;

        public InstantDamageEffect(
            DamageTarget target,
            DamageInfo damage)
        {
            _target = target;
            _damage = damage;

            _duration = 0f;
            IsUniqueEffect = false;
        }

        public override void ApplyEffect()
        {
            _target.TakeDamage(_damage);
        }

        public override void ClearEffect()
        {
        }
    }
}