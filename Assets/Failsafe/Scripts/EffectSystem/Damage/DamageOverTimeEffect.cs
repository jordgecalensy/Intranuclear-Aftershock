using Failsafe.Scripts.Damage;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Effects
{
    public sealed class DamageOverTimeEffect : Effect, IReapplicableEffect
    {
        private readonly IDamageable _target;
        private readonly DamageType _damageType;
        private readonly GameObject _source;
        private readonly bool _scaleByPower;

        private float _damagePerTick;
        private float _tickInterval;
        private float _power;
        private float _timer;

        public DamageOverTimeEffect(
            IDamageable target,
            DamageType damageType,
            float duration,
            float damagePerTick,
            float tickInterval,
            GameObject source,
            float power = 1f,
            bool scaleByPower = false)
        {
            _target = target;
            _damageType = damageType;
            _source = source;
            _scaleByPower = scaleByPower;

            _duration = Mathf.Max(0f, duration);
            _damagePerTick = Mathf.Max(0f, damagePerTick);
            _tickInterval = Mathf.Max(0.01f, tickInterval);
            _power = Mathf.Max(0f, power);

            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            _timer = 0f;
        }

        public override void Update()
        {
            _timer -= Time.deltaTime;

            if (_timer > 0f)
                return;

            _timer = _tickInterval;

            float amount = _scaleByPower
                ? _damagePerTick * _power
                : _damagePerTick;

            _target?.TakeDamage(new DamageInfo(
                amount,
                _damageType,
                DamageApplicationKind.DotTick,
                _source,
                power: _power));
        }

        public override void ClearEffect()
        {
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not DamageOverTimeEffect reapplied)
                return;

            float currentExpireAt = ElapsedAt;
            float reappliedExpireAt = Time.time + reapplied._duration;
            float newExpireAt = Mathf.Max(currentExpireAt, reappliedExpireAt);

            _duration = Mathf.Max(0f, newExpireAt - StarteAt);
            _damagePerTick = Mathf.Max(_damagePerTick, reapplied._damagePerTick);
            _power = Mathf.Max(_power, reapplied._power);
        }
    }
}