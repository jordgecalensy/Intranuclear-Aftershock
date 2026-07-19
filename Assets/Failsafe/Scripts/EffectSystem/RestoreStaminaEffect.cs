using Failsafe.Player.Model;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public sealed class RestoreStaminaEffect : Effect
    {
        private readonly IStamina _stamina;
        private readonly float _amount;
        private readonly bool _log;

        public RestoreStaminaEffect(
            IStamina stamina,
            float amount,
            bool log = false)
        {
            _stamina = stamina;
            _amount = Mathf.Max(0f, amount);
            _log = log;

            _duration = 0f;
            IsUniqueEffect = false;
        }

        public override void ApplyEffect()
        {
            if (_stamina == null || _amount <= 0f)
                return;

            float before = _stamina.CurrentStamina;
            _stamina.RestoreStamina(_amount);

            if (_log)
            {
                Debug.Log(
                    $"[RestoreStaminaEffect] Restore {_amount:0.##}. Stamina: {before:0.##} -> {_stamina.CurrentStamina:0.##}/{_stamina.MaxStamina:0.##}");
            }
        }

        public override void ClearEffect()
        {
        }
    }
}
