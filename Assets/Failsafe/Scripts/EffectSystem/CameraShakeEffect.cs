using System.Collections.Generic;
using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class CameraShakeEffect : Effect, IReapplicableEffect
    {
        private readonly PlayerRotationController _rotation;

        private struct ShakeImpulse
        {
            public float Time;
            public float Duration;
            public float Intensity;
            public float Frequency;
            // Время плавного нарастания силы тряски.
            public float FadeInDuration;
            // Время плавного затухания тряски перед окончанием импульса.
            public float FadeOutDuration;
        }

        private readonly List<ShakeImpulse> _impulses = new();

        // Храним стартовые параметры, чтобы OnReapply мог корректно добавить новый импульс.
        private readonly float _initialIntensity;
        private readonly float _initialDuration;
        private readonly float _initialFrequency;
        // Параметры fade для конкретного импульса тряски.
        private readonly float _initialFadeInDuration;
        private readonly float _initialFadeOutDuration;

        public CameraShakeEffect(
            PlayerRotationController rotation,
            float intensity,
            float duration,
            float frequency,
            float fadeInDuration = 0f,
            float fadeOutDuration = 0f)
        {
            _rotation = rotation;
            _initialIntensity = intensity;
            _initialDuration = duration;
            _initialFrequency = frequency;
            _initialFadeInDuration = Mathf.Max(0f, fadeInDuration);
            _initialFadeOutDuration = Mathf.Max(0f, fadeOutDuration);

            _duration = duration; // Общее время жизни эффекта.
            IsUniqueEffect = true; // В менеджере одновременно существует только один CameraShakeEffect.
        }

        public override void ApplyEffect()
        {
            // Первый запуск эффекта: создаём стартовый shake-импульс.
            AddImpulse(
                _initialIntensity,
                _initialDuration,
                _initialFrequency,
                _initialFadeInDuration,
                _initialFadeOutDuration);
        }

        public override void ClearEffect()
        {
            _impulses.Clear();

            if (_rotation != null)
            {
                // После завершения возвращаем голову игрока в базовый локальный поворот.
                _rotation.HeadTransform.localRotation =
                    Quaternion.Euler(_rotation.HeadLocalRotation);
            }
        }

        public void OnReapply(Effect other)
        {
            if (!(other is CameraShakeEffect reapplied))
                return;

            RemoveExpiredImpulses();

            float strongestActiveIntensity = GetStrongestActiveIntensity();
            bool shouldRestartShake =
                _impulses.Count == 0 ||
                reapplied._initialIntensity >= strongestActiveIntensity;

            if (shouldRestartShake)
            {
                // Если прилетел такой же или более сильный shake, перезапускаем его с нуля.
                // Это особенно важно для периодического урона: игрок должен ощущать каждый новый удар отдельно.
                _impulses.Clear();
                AddImpulse(
                    reapplied._initialIntensity,
                    reapplied._initialDuration,
                    reapplied._initialFrequency,
                    reapplied._initialFadeInDuration,
                    reapplied._initialFadeOutDuration);

                _duration = (Time.time - StarteAt) + reapplied._initialDuration;
                return;
            }

            float currentEndTime = ElapsedAt;
            float newEndTime = Time.time + reapplied._initialDuration;
            float finalEndTime = Mathf.Max(currentEndTime, newEndTime);

            // Более слабые повторные shake не сбрасывают текущий сильный импульс, а мягко добавляются поверх него.
            _duration = finalEndTime - StarteAt;

            AddImpulse(
                reapplied._initialIntensity,
                reapplied._initialDuration,
                reapplied._initialFrequency,
                reapplied._initialFadeInDuration,
                reapplied._initialFadeOutDuration);
        }

        public override void Update()
        {
            if (_rotation == null)
                return;

            float x = 0f;
            float y = 0f;

            for (int i = _impulses.Count - 1; i >= 0; i--)
            {
                ShakeImpulse impulse = _impulses[i];
                impulse.Time += Time.deltaTime;

                if (impulse.Time > impulse.Duration)
                {
                    _impulses.RemoveAt(i);
                    continue;
                }

                float fadeMultiplier = EvaluateFadeMultiplier(
                    impulse.Time,
                    impulse.Duration,
                    impulse.FadeInDuration,
                    impulse.FadeOutDuration);

                // Fade-множитель делает вход в тряску плавным и мягко гасит её к концу.
                float shake = Mathf.Sin(impulse.Time * impulse.Frequency) * impulse.Intensity * fadeMultiplier;
                x += shake;
                y += shake * 0.5f;

                _impulses[i] = impulse;
            }

            _rotation.HeadTransform.localRotation =
                Quaternion.Euler(
                    _rotation.HeadLocalRotation.x + x,
                    _rotation.HeadLocalRotation.y + y,
                    _rotation.HeadLocalRotation.z);
        }

        private void AddImpulse(
            float intensity,
            float duration,
            float frequency,
            float fadeInDuration,
            float fadeOutDuration)
        {
            // Каждый повторный вызов эффекта добавляет новый независимый импульс тряски.
            _impulses.Add(new ShakeImpulse
            {
                Time = 0f,
                Duration = duration,
                Intensity = intensity,
                Frequency = frequency,
                FadeInDuration = fadeInDuration,
                FadeOutDuration = fadeOutDuration
            });
        }

        private void RemoveExpiredImpulses()
        {
            for (int i = _impulses.Count - 1; i >= 0; i--)
            {
                if (_impulses[i].Time > _impulses[i].Duration)
                    _impulses.RemoveAt(i);
            }
        }

        private float GetStrongestActiveIntensity()
        {
            float strongestIntensity = 0f;

            for (int i = 0; i < _impulses.Count; i++)
            {
                if (_impulses[i].Intensity > strongestIntensity)
                    strongestIntensity = _impulses[i].Intensity;
            }

            return strongestIntensity;
        }

        private static float EvaluateFadeMultiplier(
            float currentTime,
            float totalDuration,
            float fadeInDuration,
            float fadeOutDuration)
        {
            float multiplier = 1f;

            // На старте эффект растёт от 0 до 1 за указанное время fade-in.
            if (fadeInDuration > 0f)
                multiplier = Mathf.Min(multiplier, Mathf.Clamp01(currentTime / fadeInDuration));

            if (fadeOutDuration > 0f)
            {
                // Перед завершением эффект затухает от 1 до 0 за время fade-out.
                float remainingTime = Mathf.Max(0f, totalDuration - currentTime);
                multiplier = Mathf.Min(multiplier, Mathf.Clamp01(remainingTime / fadeOutDuration));
            }

            return multiplier;
        }
    }
}
