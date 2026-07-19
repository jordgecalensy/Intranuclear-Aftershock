using System;
using Failsafe.PlayerMovements.Controllers;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "NoiseEffectDefinition",
        menuName = "Failsafe/Effects/Negative/Noise")]
    public class NoiseEffectDefinition : EffectDefinition
    {
        [Header("Noise")]
        [Tooltip("Сила шума. Сейчас фактически работает как радиус сферы шума.")]
        [SerializeField] private float _strength = 10f;

        [Tooltip("Сколько секунд шум будет существовать.")]
        [SerializeField] private float _duration = 3f;

        [Tooltip("Если true, сила шума умножается на context.Power.")]
        [SerializeField] private bool _scaleByContextPower = false;

        [Header("Resolve")]
        [Tooltip("Если PlayerNoiseController не найден через DI, использовать SignalManager напрямую.")]
        [SerializeField] private bool _useSignalManagerFallback = true;

        [Header("Debug")]
        [SerializeField] private bool _log = true;

        public override bool CanApply(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target == null)
            {
                if (_log)
                    Debug.LogWarning("[NoiseEffectDefinition] CanApply false: target not found.");

                return false;
            }

            if (TryResolvePlayerNoiseController(target, out _))
                return true;

            if (_useSignalManagerFallback && TryResolveSignalManager(out _))
                return true;

            if (_log)
            {
                Debug.LogWarning(
                    $"[NoiseEffectDefinition] CanApply false: PlayerNoiseController and SignalManager not found for {target.name}.",
                    target);
            }

            return false;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target == null)
            {
                if (_log)
                    Debug.LogWarning("[NoiseEffectDefinition] CreateEffect failed: target not found.");

                return null;
            }

            float finalStrength = _scaleByContextPower
                ? _strength * context.Power
                : _strength;

            if (TryResolvePlayerNoiseController(target, out PlayerNoiseController noiseController))
            {
                if (_log)
                    Debug.Log($"[NoiseEffectDefinition] Using PlayerNoiseController on target {target.name}.", target);

                return new NoiseEffect(
                    noiseController,
                    finalStrength,
                    _duration,
                    _log);
            }

            if (_useSignalManagerFallback &&
                TryResolveSignalManager(out SignalManager signalManager))
            {
                if (_log)
                {
                    Debug.Log(
                        $"[NoiseEffectDefinition] PlayerNoiseController not found. Using SignalManager fallback for {target.name}.",
                        target);
                }

                return new NoiseEffect(
                    signalManager,
                    target.transform,
                    finalStrength,
                    _duration,
                    _log);
            }

            if (_log)
            {
                Debug.LogWarning(
                    $"[NoiseEffectDefinition] CreateEffect failed: no noise target resolved for {target.name}.",
                    target);
            }

            return null;
        }

        public override string GetStackKey(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target != null)
                return $"negative.noise.{GetInstanceID()}.target.{target.GetInstanceID()}";

            if (context.HitCollider != null)
                return $"negative.noise.{GetInstanceID()}.collider.{context.HitCollider.GetInstanceID()}";

            if (context.TargetObject != null)
                return $"negative.noise.{GetInstanceID()}.target-object.{context.TargetObject.GetInstanceID()}";

            return $"negative.noise.{GetInstanceID()}";
        }

        private static GameObject ResolveTargetObject(EffectContext context)
        {
            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target != null)
                return target;

            if (context.TargetObject != null)
                return context.TargetObject;

            if (context.HitCollider != null)
            {
                if (context.HitCollider.attachedRigidbody != null)
                    return context.HitCollider.attachedRigidbody.gameObject;

                return context.HitCollider.transform.root.gameObject;
            }

            return null;
        }

        private bool TryResolvePlayerNoiseController(
            GameObject target,
            out PlayerNoiseController noiseController)
        {
            noiseController = null;

            LifetimeScope scope = ResolveLifetimeScope(target);

            if (scope == null)
            {
                if (_log)
                {
                    Debug.Log(
                        $"[NoiseEffectDefinition] LifetimeScope not found near {target.name}. Will try fallback.",
                        target);
                }

                return false;
            }

            if (scope.Container == null)
            {
                if (_log)
                {
                    Debug.Log(
                        $"[NoiseEffectDefinition] LifetimeScope container is null on {scope.name}. Will try fallback.",
                        scope);
                }

                return false;
            }

            try
            {
                noiseController = scope.Container.Resolve<PlayerNoiseController>();
                return noiseController != null;
            }
            catch (Exception e)
            {
                if (_log)
                {
                    Debug.Log(
                        $"[NoiseEffectDefinition] Cannot resolve PlayerNoiseController from scope {scope.name}. Will try fallback. {e.Message}",
                        scope);
                }

                return false;
            }
        }

        private static LifetimeScope ResolveLifetimeScope(GameObject target)
        {
            if (target == null)
                return null;

            return target.GetComponent<LifetimeScope>() ??
                   target.GetComponentInParent<LifetimeScope>() ??
                   target.GetComponentInChildren<LifetimeScope>(true);
        }

        private bool TryResolveSignalManager(out SignalManager signalManager)
        {
            signalManager = SignalManager.Instance;

            if (signalManager != null)
                return true;

            signalManager = UnityEngine.Object.FindObjectOfType<SignalManager>();

            if (signalManager != null)
                return true;

            if (_log)
                Debug.LogWarning("[NoiseEffectDefinition] SignalManager not found in scene.");

            return false;
        }
    }
}