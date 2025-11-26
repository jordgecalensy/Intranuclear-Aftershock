using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using FMODUnity;

namespace Failsafe.Scripts.EffectSystem
{
    public class DamageEffect : Effect, IReapplicableEffect
    {
        private Material _damageMaterial;
        private CustomPassVolume _customPassVolume;
        private StudioEventEmitter _damageEmitter;

        private EventReference _damageEvent;

        /// <summary>
        /// Базовая длительность эффекта
        /// </summary>

        public DamageEffect(float duration)
        {
            _damageMaterial = Resources.Load<Material>("StimpckEffect");
            if (_damageMaterial == null)
                Debug.LogWarning("DamageEffect: не найден материал DamageEffect в Resources/");

            _duration = duration;     // <-- 🔥 задаём длительность здесь
            IsUniqueEffect = true;

            _damageEvent = EventReference.Find("event:/UI/LowHP/LowHealthSFX");
        }

        public override void ApplyEffect()
        {
            // --- Создаём volume
            _customPassVolume = new GameObject("DamageEffectPass")
                .AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

            var pass = new CustomPassDrawer(_damageMaterial);
            _customPassVolume.customPasses.Add(pass);

            // --- Создание и проигрывание звука
            _damageEmitter = _customPassVolume.gameObject.AddComponent<StudioEventEmitter>();
            _damageEmitter.EventReference = _damageEvent;
            _damageEmitter.Play();

            Debug.Log("DamageEffect applied");
        }

        public override void ClearEffect()
        {
            if (_damageEmitter != null)
                _damageEmitter.Stop();

            if (_customPassVolume != null)
                Object.Destroy(_customPassVolume.gameObject);

            Debug.Log("DamageEffect cleared");
        }

        /// <summary>
        /// Срабатывает, если эффект был вызван повторно
        /// </summary>
        public void OnReapply(Effect newEffect)
        {
            DamageEffect reapplied = newEffect as DamageEffect;
            if (reapplied == null)
                return;

            Debug.Log("DamageEffect reapplied — extending duration");

            // 1. Узнаём, сколько времени осталось у эффекта
            float remaining = ElapsedAt - Time.time;
            if (remaining < 0f)
                remaining = 0f;

            // 2. Новая длительность = оставшееся время + длительность нового вызова
            _duration = remaining + reapplied._duration;

            // 3. Перезапускаем звук (визуальный пасс уже включён)
            if (_damageEmitter != null)
            {
                _damageEmitter.Stop();
                _damageEmitter.Play();
            }
        }
    }
}
