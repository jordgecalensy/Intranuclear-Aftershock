using Failsafe.Scripts.Health;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public sealed class InstantHealEffect : Effect
    {
        private readonly IHealth _health;
        private readonly float _amount;
        private readonly bool _log;

        public InstantHealEffect(
            IHealth health,
            float amount,
            bool log = false)
        {
            _health = health;
            _amount = Mathf.Max(0f, amount);
            _log = log;

            _duration = 0f;
            IsUniqueEffect = false;
        }

        public override void ApplyEffect()
        {
            if (_health == null || _amount <= 0f)
                return;

            float before = _health.CurrentHealth;
            _health.AddHealth(_amount);

            if (_log)
            {
                Debug.Log(
                    $"[InstantHealEffect] Heal {_amount:0.##}. Health: {before:0.##} -> {_health.CurrentHealth:0.##}/{_health.MaxHealth:0.##}");
            }
        }

        public override void ClearEffect()
        {
        }
    }
}
