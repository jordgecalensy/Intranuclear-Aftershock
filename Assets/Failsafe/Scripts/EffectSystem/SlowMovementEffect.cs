using System;
using UnityEngine;
using Failsafe.PlayerMovements.Controllers;

namespace Failsafe.Scripts.EffectSystem.Effects
{
    /// <summary>
    /// Эффект замедления перемещения игрока: умножает скорость на multiplier (0.0..1.0).
    /// </summary>
    public class SlowMovementEffect : Effect, IReapplicableEffect
    {
        private readonly PlayerMovementController _controller;

        /// <summary>
        /// Множитель скорости: 1.0 = без эффекта; 0.5 = в 2 раза медленнее; 0.2 = очень сильный слоу.
        /// </summary>
        private float _multiplier;

        /// <summary>
        /// Идентификатор модификатора скорости в контроллере.
        /// </summary>
        private int _modifierId;

        /// <param name="controller">Контроллер перемещения</param>
        /// <param name="duration">Длительность эффекта (сек)</param>
        /// <param name="multiplier">Множитель (0..1]</param>
        /// <param name="unique">Является ли эффект уникальным</param>
        public SlowMovementEffect(PlayerMovementController controller, float duration, float multiplier, bool unique = true)
        {
            _controller = controller;
            _duration = Mathf.Max(0f, duration);
            _multiplier = Mathf.Clamp(multiplier, 0.01f, 1f);
            IsUniqueEffect = unique;

            // генерируем стабильный id на время жизни эффекта
            _modifierId = GetHashCode();
        }

        public override void ApplyEffect()
        {
            if (_controller == null)
            {
                Debug.LogError("[SlowMovementEffect] PlayerMovementController == null. Проверь DI/резолв.");
                return;
            }
            _controller.SetSpeedModifier(_modifierId, _multiplier);
        }
        public override void ClearEffect()
        {
            // Убираем только свой модификатор, не трогая другие.
            _controller.RemoveSpeedModifier(_modifierId);
        }

        /// <summary>
        /// Продлить/усилить эффект при повторном применении.
        /// </summary>
        public void OnReapply(Effect newEffect)
        {
            if (newEffect is SlowMovementEffect slow)
            {
                // Берём более «сильное» замедление (меньший множитель)
                _multiplier = Mathf.Min(_multiplier, slow._multiplier);

                // продлеваем длительность от текущего момента
                _duration = Mathf.Max(ElapsedAt - Time.time, 0f) + slow._duration;

                // сразу обновляем множитель в контроллере
                _controller.SetSpeedModifier(_modifierId, _multiplier);
            }
        }
    }
}