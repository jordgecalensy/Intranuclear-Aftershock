using System;
using VContainer.Unity;
using Failsafe.Player.Model;
using Failsafe.Player.UI;
using Failsafe.PlayerMovements;
using Failsafe.Scripts.Health;

namespace Failsafe.Player.Scripts
{
    public class PlayerUIPresenter : IInitializable, ITickable
    {
        private readonly PlayerUIController _view;
        private readonly IStamina _stamina;
        private readonly IHealth _health;
        private readonly PlayerModelParameters _modelParameters;
        private readonly PlayerNoiseParameters _noiseParameters;
        private readonly PlayerController _playerController;

        public PlayerUIPresenter(
            PlayerUIController view,
            IStamina stamina,
            IHealth health,
            PlayerModelParameters modelParameters,
            PlayerNoiseParameters noiseParameters,
            PlayerController playerController)
        {
            _view = view;
            _stamina = stamina;
            _health = health;
            _modelParameters = modelParameters;
            _noiseParameters = noiseParameters;
            _playerController = playerController;
        }

        public void Initialize() => UpdateView();

        public void Tick() => UpdateView();

        private void UpdateView()
        {
            _view.UpdateStaminaUI(
                _stamina.CurrentStamina,
                _stamina.MaxStamina,
                _modelParameters.MaxStamina);

            _view.UpdateHealthUI(_health.CurrentHealth, _health.MaxHealth);

            _view.UpdateNoiseUI(
                _playerController.CurrentNoiseStrength,
                _noiseParameters.ReducedStrength,
                _noiseParameters.DefaultStrength,
                _noiseParameters.IncreasedStrength);
        }
    }
}
