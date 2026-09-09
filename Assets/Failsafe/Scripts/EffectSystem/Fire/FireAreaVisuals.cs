using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Owns the runtime particle instance and its intensity mapping.
    /// </summary>
    public sealed class FireAreaVisuals
    {
        private readonly Transform _owner;

        private ParticleSystem _instance;
        private ParticleSystem[] _particleSystems =
            Array.Empty<ParticleSystem>();
        private float[] _fadeStartEmission = Array.Empty<float>();
        private float _scaleVelocity;
        private float _emissionVelocity;
        private float _currentScale = 1f;
        private float _currentEmission = 1f;
        private float _fadeDuration;
        private float _fadeTime;

        public FireAreaVisuals(Transform owner)
        {
            _owner = owner != null
                ? owner
                : throw new ArgumentNullException(nameof(owner));
        }

        public void Initialize(
            ParticleSystem prefab,
            float prewarmTime,
            float intensity,
            float peakIntensity,
            AnimationCurve scaleCurve,
            AnimationCurve emissionCurve)
        {
            Dispose();
            _scaleVelocity = 0f;
            _emissionVelocity = 0f;
            _currentScale = 1f;
            _currentEmission = 1f;
            _fadeTime = 0f;

            if (prefab == null)
                return;

            _instance = UnityEngine.Object.Instantiate(prefab, _owner);
            _instance.name = prefab.name + "(Runtime)";
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
            _particleSystems =
                _instance.GetComponentsInChildren<ParticleSystem>(true);

            ParticleSystem.MainModule main = _instance.main;
            main.prewarm = true;

            if (prewarmTime > 0f)
            {
                _instance.Simulate(
                    prewarmTime,
                    withChildren: true,
                    restart: true,
                    fixedTimeStep: false);
            }

            _instance.Play(true);
            ApplyIntensity(
                intensity,
                peakIntensity,
                scaleCurve,
                emissionCurve,
                0f,
                true);
        }

        public void ApplyIntensity(
            float intensity,
            float peakIntensity,
            AnimationCurve scaleCurve,
            AnimationCurve emissionCurve,
            float smoothTime,
            bool immediate,
            float deltaTime = 0f)
        {
            if (_instance == null)
                return;

            float normalized = Mathf.InverseLerp(
                0f,
                Mathf.Max(0.001f, peakIntensity),
                Mathf.Max(0f, intensity));
            float targetScale = Evaluate(scaleCurve, normalized, 1f);
            float targetEmission = Evaluate(emissionCurve, normalized, 1f);

            if (immediate)
            {
                _currentScale = targetScale;
                _currentEmission = targetEmission;
            }
            else
            {
                float safeSmoothTime = Mathf.Max(0.0001f, smoothTime);
                float safeDeltaTime = Mathf.Max(0f, deltaTime);
                _currentScale = Mathf.SmoothDamp(
                    _currentScale,
                    targetScale,
                    ref _scaleVelocity,
                    safeSmoothTime,
                    Mathf.Infinity,
                    safeDeltaTime);
                _currentEmission = Mathf.SmoothDamp(
                    _currentEmission,
                    targetEmission,
                    ref _emissionVelocity,
                    safeSmoothTime,
                    Mathf.Infinity,
                    safeDeltaTime);
            }

            _instance.transform.localScale = Vector3.one * _currentScale;
            SetEmissionMultiplier(_currentEmission);
        }

        public bool BeginExtinguish(float duration)
        {
            if (_instance == null)
                return true;

            _fadeDuration = Mathf.Max(0.05f, duration);
            _fadeTime = 0f;

            if (_fadeStartEmission.Length != _particleSystems.Length)
                _fadeStartEmission = new float[_particleSystems.Length];

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];
                _fadeStartEmission[i] = particleSystem != null
                    ? particleSystem.emission.rateOverTime.curveMultiplier
                    : 0f;
            }

            return false;
        }

        public bool TickExtinguish(float deltaTime)
        {
            if (_instance == null)
                return true;

            _fadeTime += Mathf.Max(0f, deltaTime);
            float remaining = 1f - Mathf.Clamp01(_fadeTime / _fadeDuration);

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];

                if (particleSystem == null)
                    continue;

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
                rate.curveMultiplier = _fadeStartEmission[i] * remaining;
                emission.rateOverTime = rate;
            }

            _instance.transform.localScale = Vector3.one *
                Mathf.Lerp(0.4f, _currentScale, remaining);

            if (_fadeTime < _fadeDuration)
                return false;

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];

                if (particleSystem != null)
                {
                    particleSystem.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (_instance != null)
            {
                _instance.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                UnityEngine.Object.Destroy(_instance.gameObject);
            }

            _instance = null;
            _particleSystems = Array.Empty<ParticleSystem>();
            _fadeStartEmission = Array.Empty<float>();
        }

        private void SetEmissionMultiplier(float multiplier)
        {
            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];

                if (particleSystem == null)
                    continue;

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
                rate.curveMultiplier = multiplier;
                emission.rateOverTime = rate;
            }
        }

        private static float Evaluate(
            AnimationCurve curve,
            float time,
            float fallback)
        {
            return curve != null ? curve.Evaluate(time) : fallback;
        }
    }
}
