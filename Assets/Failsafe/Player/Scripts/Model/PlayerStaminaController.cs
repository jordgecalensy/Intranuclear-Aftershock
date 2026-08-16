using System;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Player.Model
{
    /// <summary>
    /// Контроллер выносливости персонажа: регенерация, трата на действия 
    /// </summary>
    public class PlayerStaminaController : IFixedTickable, IDisposable
    {
        private readonly IStamina _stamina;
        private readonly PlayerModelParameters _playerModelParameters;
        private readonly PlayerRuntimeParameters _runtimeParameters;
        private float _spentAt = -1;

        public PlayerStaminaController(
            IStamina stamina,
            PlayerModelParameters playerModelParameters,
            PlayerRuntimeParameters runtimeParameters)
        {
            _stamina = stamina;
            _playerModelParameters = playerModelParameters;
            _runtimeParameters = runtimeParameters;

            _stamina.OnStaminaSpended += OnSpend;
        }

        public void Dispose()
        {
            _stamina.OnStaminaSpended -= OnSpend;
        }

        public void FixedTick()
        {
            PassiveRestore();
        }

        /// <summary>
        /// Тратить вынослиаость на бег
        /// </summary>
        /// <remarks>
        /// Выполнять в Update или FixedUpdate
        /// </remarks>
        public void SpendOnRunning()
        {
            var amount = _playerModelParameters.StaminaForRunPerSecond * Time.deltaTime;
            _stamina.SpendStamina(amount);
        }

        /// <summary>
        /// Потратить выносливость на прыжок
        /// </summary>
        public void SpendOnJump()
        {
            _stamina.SpendStamina(_playerModelParameters.StaminaForJump);
        }

        private void PassiveRestore()
        {
            if (_spentAt + _playerModelParameters.RegenerationDelay > Time.time)
                return;
            if (_stamina.CurrentStamina >= _stamina.MaxStamina)
                return;

            float regenerationPerSecond = Mathf.Max(
                0f,
                _runtimeParameters.StaminaRegenerationPerSecond);

            if (regenerationPerSecond <= 0f)
                return;

            float regenerateAmount = regenerationPerSecond * Time.fixedDeltaTime;
            _stamina.RestoreStamina(regenerateAmount);
        }

        private void OnSpend(float ammount)
        {
            _spentAt = Time.time;
        }
    }
}
