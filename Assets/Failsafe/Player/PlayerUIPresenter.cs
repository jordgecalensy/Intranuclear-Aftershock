using System;
using VContainer.Unity;
using Failsafe.Player.Model;
using Failsafe.Player.UI;
using Failsafe.Scripts.Health;

namespace Failsafe.Player.Scripts
{
    public class PlayerUIPresenter : IInitializable, ITickable
    {
        private readonly PlayerUIController _view;
        private readonly IStamina _stamina; //
        private readonly IHealth _health; //

        public PlayerUIPresenter(PlayerUIController view, IStamina stamina, IHealth health)
        {
            _view = view;
            _stamina = stamina;
            _health = health;
        }

        public void Initialize() => _view.UpdateHealthUI(_health.CurrentHealth, _health.MaxHealth);

        public void Tick()
        {
            _view.UpdateStaminaUI(_stamina.CurrentStamina, _stamina.MaxStamina);
            _view.UpdateHealthUI(_health.CurrentHealth, _health.MaxHealth);
            // Если есть менеджер врагов, передавай сюда AlertnessValue
        }
    }
}