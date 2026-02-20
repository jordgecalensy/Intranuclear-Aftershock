using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using FMODUnity;
using System.Collections.Generic;

namespace Failsafe.Scripts.EffectSystem
{
    public class DamageHitEffect : Effect, IReapplicableEffect
    {
        private Material _damageHitMaterial;
        private CustomPassVolume _customPassVolume;
        private StudioEventEmitter _damageHitEmitter;
        private EventReference _damageHitEvent;

        private const string _alphaIntensity = "_AlphaIntensity";

        // ---------- НАСТРОЙКИ ПОРОГОВ ----------
        private enum HitType
        {
            Light,
            Medium,
            Heavy,
            Critical
        }

        private struct HitPreset
        {
            public float Alpha;
            public float Duration;
        }

        private static readonly Dictionary<HitType, HitPreset> _presets = new()
        {
            { HitType.Light,    new HitPreset { Alpha = 0.25f, Duration = 0.18f } },
            { HitType.Medium,   new HitPreset { Alpha = 0.45f, Duration = 0.28f } },
            { HitType.Heavy,    new HitPreset { Alpha = 0.70f, Duration = 0.38f } },
            { HitType.Critical, new HitPreset { Alpha = 1.00f, Duration = 0.55f } },
        };

        // ---------- ИМПУЛЬС ----------
        private struct HitImpulse
        {
            public float Time;
            public float Duration;
            public float Alpha;
        }

        private readonly List<HitImpulse> _impulses = new();

        private float _lastGivenDuration;

        // ---------- КОНСТРУКТОР ----------
        public DamageHitEffect(float damageAmount)
        {
            IsUniqueEffect = true;

            HitType type = EvaluateHit(damageAmount);
            var preset = _presets[type];

            _duration = preset.Duration;
            _lastGivenDuration = preset.Duration;

            _damageHitMaterial = Object.Instantiate(Resources.Load<Material>("TakingDamage"));
            if (_damageHitMaterial == null)
                Debug.LogWarning("DamageHitEffect: material TakingDamage not found!");

            //_damageHitEvent = EventReference.Find("event:/UI/LowHP/LowHealthSFX");
        }

        // ---------- APPLY ----------
        public override void ApplyEffect()
        {
            _customPassVolume = new GameObject("DamageHitEffectPass")
                .AddComponent<CustomPassVolume>();

            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

            var pass = new CustomPassDrawer(_damageHitMaterial);
            _customPassVolume.customPasses.Add(pass);

            //_damageHitEmitter = _customPassVolume.gameObject.AddComponent<StudioEventEmitter>();
            //_damageHitEmitter.EventReference = _damageHitEvent;
            //_damageHitEmitter.Play();

            AddImpulse(_duration);
        }

        // ---------- REAPPLY ----------
        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not DamageHitEffect reapplied)
                return;

            AddImpulse(reapplied._lastGivenDuration);

            if (_damageHitEmitter != null)
            {
                _damageHitEmitter.Stop();
                _damageHitEmitter.Play();
            }
        }

        // ---------- UPDATE ----------
        public override void Update()
        {
            float finalAlpha = 0f;
            float longestRemaining = 0f;

            for (int i = _impulses.Count - 1; i >= 0; i--)
            {
                var imp = _impulses[i];
                imp.Time += Time.deltaTime;

                if (imp.Time >= imp.Duration)
                {
                    _impulses.RemoveAt(i);
                    continue;
                }

                float t = 1f - (imp.Time / imp.Duration);
                float alpha = imp.Alpha * t;

                finalAlpha = Mathf.Max(finalAlpha, alpha);
                longestRemaining = Mathf.Max(longestRemaining, imp.Duration - imp.Time);

                _impulses[i] = imp;
            }

            SetAlpha(finalAlpha);

            // держим эффект живым пока есть импульсы
            _duration = longestRemaining;
        }

        // ---------- CLEAR ----------
        public override void ClearEffect()
        {
            if (_damageHitEmitter != null)
                _damageHitEmitter.Stop();

            if (_customPassVolume != null)
                Object.Destroy(_customPassVolume.gameObject);

            if (_damageHitMaterial != null)
                Object.Destroy(_damageHitMaterial);

            _impulses.Clear();
        }

        // ---------- LOGIC ----------
        private void AddImpulse(float duration)
        {
            float alpha = EvaluateAlpha(duration);

            _impulses.Add(new HitImpulse
            {
                Time = 0f,
                Duration = duration,
                Alpha = alpha
            });
        }

        private static HitType EvaluateHit(float damage)
        {
            if (damage < 0.1f) return HitType.Light;
            if (damage < 0.5f) return HitType.Medium;
            if (damage < 50f) return HitType.Heavy;
            return HitType.Critical;
        }

        private static float EvaluateAlpha(float duration)
        {
            if (duration <= _presets[HitType.Light].Duration) return _presets[HitType.Light].Alpha;
            if (duration <= _presets[HitType.Medium].Duration) return _presets[HitType.Medium].Alpha;
            if (duration <= _presets[HitType.Heavy].Duration) return _presets[HitType.Heavy].Alpha;
            return _presets[HitType.Critical].Alpha;
        }

        private void SetAlpha(float value)
        {
            if (_damageHitMaterial != null)
                _damageHitMaterial.SetFloat(_alphaIntensity, value);
        }
    }
}
