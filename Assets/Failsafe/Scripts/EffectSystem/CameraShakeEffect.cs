using UnityEngine;
using Failsafe.PlayerMovements.Controllers;
using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;

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
        }

        private readonly List<ShakeImpulse> _impulses = new();

        // Храним параметры, чтобы OnReapply мог использовать их
        private readonly float _initialIntensity;
        private readonly float _initialDuration;
        private readonly float _initialFrequency;

        public CameraShakeEffect(
            PlayerRotationController rotation,
            float intensity,
            float duration,
            float frequency)
        {
            _rotation = rotation;
            _initialIntensity = intensity;
            _initialDuration  = duration;
            _initialFrequency = frequency;

            _duration = duration; // эффект бесконечный
            IsUniqueEffect = true;      // только один shake-эффект
        }

        public override void ApplyEffect()
        {
            // первый запуск → добавляем первый импульс
            AddImpulseDamage(_initialIntensity, _initialDuration, _initialFrequency);
        }

        public override void ClearEffect()
        {
            _impulses.Clear();

            if (_rotation != null)
            {
                _rotation.HeadTransform.localRotation =
                    Quaternion.Euler(_rotation.HeadLocalRotation);
            } 
        }

        /// <summary>
        /// Вызывается EffectManager, если эффект уже применяется.
        /// Продлеваем, добавляя новый импульс.
        /// </summary>
        public void OnReapply(Effect other)
        {
            if (other is CameraShakeEffect reapplied)
            {
                _duration = reapplied._duration + (Time.time - StarteAt);
                AddImpulseDamage(reapplied._initialIntensity, reapplied._initialDuration, reapplied._initialFrequency);
            }
        }

        /// <summary>
        /// Добавляет новый shake-импульс
        /// </summary>
        private void AddImpulseDamage(float intensity, float duration, float frequency)
        {
            _impulses.Add(new ShakeImpulse
            {
                Time = 0f,
                Duration = duration,
                Intensity = intensity,
                Frequency = frequency
            });
        }

        public override void Update()
        {
            if (_rotation == null)
                return;

            float x = 0;
            float y = 0;

            for (int i = _impulses.Count - 1; i >= 0; i--)
            {
                var imp = _impulses[i];
                imp.Time += Time.deltaTime;

                if (imp.Time > imp.Duration)
                {
                    _impulses.RemoveAt(i);
                    continue;
                }

                float shake = Mathf.Sin(imp.Time * imp.Frequency) * imp.Intensity;
                x += shake;
                y += shake * 0.5f;

                _impulses[i] = imp;
            }

            _rotation.HeadTransform.localRotation =
                Quaternion.Euler(
                    _rotation.HeadLocalRotation.x + x,
                    _rotation.HeadLocalRotation.y + y,
                    0f);
        }
    }
}
