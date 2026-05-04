using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class EarthquakeMovementSlowEffect : Effect, IReapplicableEffect
    {
        private readonly PlayerMovementController _movementController;
        private readonly int _modifierId;

        // Конечный множитель скорости, до которого должен дойти эффект.
        private float _targetSpeedMultiplier;
        // Время плавного входа в замедление.
        private float _fadeInDuration;
        // Время плавного выхода из замедления.
        private float _fadeOutDuration;

        public EarthquakeMovementSlowEffect(
            PlayerMovementController movementController,
            float speedMultiplier,
            float duration,
            float fadeInDuration = 0f,
            float fadeOutDuration = 0f)
        {
            _movementController = movementController;
            _targetSpeedMultiplier = Mathf.Max(0.0001f, speedMultiplier);
            _duration = duration;
            _fadeInDuration = Mathf.Max(0f, fadeInDuration);
            _fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

            IsUniqueEffect = true; // В менеджере может жить только один earthquake slow effect.
            _modifierId = GetType().GetHashCode(); // Ключ модификатора скорости в PlayerMovementController.
        }

        public override void ApplyEffect()
        {
            // При старте выставляем текущее значение замедления с учётом fade-in.
            _movementController.SetSpeedModifier(_modifierId, EvaluateCurrentMultiplier());
        }

        public override void Update()
        {
            // Каждый тик обновляем множитель, чтобы замедление плавно менялось по времени.
            _movementController.SetSpeedModifier(_modifierId, EvaluateCurrentMultiplier());
        }

        public override void ClearEffect()
        {
            // После завершения полностью убираем модификатор скорости.
            _movementController.RemoveSpeedModifier(_modifierId);
        }

        public void OnReapply(Effect other)
        {
            if (!(other is EarthquakeMovementSlowEffect reapplied))
                return;

            _targetSpeedMultiplier = reapplied._targetSpeedMultiplier;
            _fadeInDuration = reapplied._fadeInDuration;
            _fadeOutDuration = reapplied._fadeOutDuration;
            // Продлеваем эффект, сохраняя его привязку к исходному времени старта.
            _duration = reapplied._duration + (Time.time - StarteAt);

            _movementController.SetSpeedModifier(_modifierId, EvaluateCurrentMultiplier());
        }

        private float EvaluateCurrentMultiplier()
        {
            float elapsed = Mathf.Clamp(Time.time - StarteAt, 0f, _duration);
            float fadeMultiplier = EvaluateFadeMultiplier(elapsed, _duration, _fadeInDuration, _fadeOutDuration);
            // Линейно идём от нормальной скорости 1.0 к целевому множителю замедления.
            return Mathf.Lerp(1f, _targetSpeedMultiplier, fadeMultiplier);
        }

        private static float EvaluateFadeMultiplier(
            float currentTime,
            float totalDuration,
            float fadeInDuration,
            float fadeOutDuration)
        {
            float multiplier = 1f;

            // Плавный вход: сила замедления растёт от 0 до 1.
            if (fadeInDuration > 0f)
                multiplier = Mathf.Min(multiplier, Mathf.Clamp01(currentTime / fadeInDuration));

            if (fadeOutDuration > 0f)
            {
                // Плавный выход: к концу эффект постепенно отпускает игрока.
                float remainingTime = Mathf.Max(0f, totalDuration - currentTime);
                multiplier = Mathf.Min(multiplier, Mathf.Clamp01(remainingTime / fadeOutDuration));
            }

            return multiplier;
        }
    }
}
