using System;
using Failsafe.Scripts.Modifiebles;
using UnityEngine;

namespace Failsafe.Player.Model
{
    /// <summary>
    /// Выносливость персонажа
    /// </summary>
    public interface IStamina
    {
        public event Action<float> OnStaminaSpended;
        public event Action<float> OnStaminaRestored;

        public bool IsEmpty { get; }
        /// <summary>
        /// Максимальное значение выносливости
        /// </summary>
        public float MaxStamina { get; }
        /// <summary>
        /// Текущее значение выносливости
        /// </summary>
        public float CurrentStamina { get; }
        /// <summary>
        /// Потратить выносливость
        /// </summary>
        /// <param name="amount">Кол-во потраченной выносливости</param>
        public void SpendStamina(float amount);
        /// <summary>
        /// Восполнить выносливость
        /// </summary>
        /// <param name="amount">Кол-во восстановленной выносливости</param>
        public void RestoreStamina(float amount);
    }

    public interface IRestorableStamina : IStamina
    {
        public event Action<float> OnStateRestored;

        public void RestoreState(float stamina);
    }

    /// <summary>
    /// Выносливость персонажа
    /// </summary>
    public class PlayerStamina : IRestorableStamina
    {

        public event Action<float> OnStaminaSpended;
        public event Action<float> OnStaminaRestored;
        public event Action<float> OnStateRestored;

        public bool IsEmpty => _currentStamina <= 0;

        public float MaxStamina => Mathf.Max(1f, _maxStamina);

        public float CurrentStamina => Mathf.Max(0, _currentStamina);
        private readonly ModifiableField<float> _maxStamina;
        private float _currentStamina;


        public PlayerStamina(PlayerRuntimeParameters runtimeParameters)
        {
            _maxStamina = runtimeParameters.MaxStamina;
            _currentStamina = MaxStamina;
        }

        public void AddMaxStaminaModifier(IModificator<float> modificator)
        {
            ChangeMaxStamina(modificator, true);
        }

        public void RemoveMaxStaminaModifier(IModificator<float> modificator)
        {
            ChangeMaxStamina(modificator, false);
        }

        private void ChangeMaxStamina(IModificator<float> modificator, bool add)
        {
            if (modificator == null)
                return;

            float previousMaxStamina = Mathf.Max(0.0001f, MaxStamina);
            float staminaRatio = Mathf.Clamp01(CurrentStamina / previousMaxStamina);

            if (add)
                _maxStamina.AddModificator(modificator);
            else
                _maxStamina.RemoveModificator(modificator);

            _currentStamina = Mathf.Clamp(MaxStamina * staminaRatio, 0f, MaxStamina);
        }

        public void RestoreStamina(float amount)
        {
            _currentStamina = Mathf.Min(_currentStamina + amount, MaxStamina);
            OnStaminaRestored?.Invoke(amount);
        }

        public void SpendStamina(float amount)
        {
            // Значение может быть отрицательнмы, чтобы не абузить затратные действия при низкой выносливости
            _currentStamina -= amount;
            OnStaminaSpended?.Invoke(amount);
        }
        public void RestoreState(float stamina)
        {
            _currentStamina = Mathf.Clamp(stamina, 0f, MaxStamina);
            OnStateRestored?.Invoke(_currentStamina);
        }
    }
}
