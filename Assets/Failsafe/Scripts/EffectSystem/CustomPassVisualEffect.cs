using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Failsafe.Scripts.EffectSystem
{
    public sealed class CustomPassVisualEffect : Effect, IReapplicableEffect
    {
        private readonly Material _material;
        private readonly string _runtimeObjectName;
        private readonly CustomPassInjectionPoint _injectionPoint;
        private readonly bool _log;

        private CustomPassVolume _customPassVolume;

        public CustomPassVisualEffect(
            Material material,
            float duration,
            string runtimeObjectName,
            CustomPassInjectionPoint injectionPoint,
            bool log = false)
        {
            _material = material;
            _duration = Mathf.Max(0f, duration);
            _runtimeObjectName = string.IsNullOrWhiteSpace(runtimeObjectName)
                ? "CustomPassVisualEffect"
                : runtimeObjectName;
            _injectionPoint = injectionPoint;
            _log = log;

            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            if (_material == null)
                return;

            if (_customPassVolume != null)
                return;

            _customPassVolume = new GameObject(_runtimeObjectName)
                .AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = _injectionPoint;
            _customPassVolume.customPasses.Add(new CustomPassDrawer(_material));

            if (_log)
            {
                Debug.Log(
                    $"[CustomPassVisualEffect] Applied {_material.name} for {_duration:0.##}s.",
                    _customPassVolume);
            }
        }

        public override void ClearEffect()
        {
            if (_customPassVolume == null)
                return;

            Object.Destroy(_customPassVolume.gameObject);

            if (_log)
                Debug.Log($"[CustomPassVisualEffect] Cleared {_runtimeObjectName}.");

            _customPassVolume = null;
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not CustomPassVisualEffect reapplied)
                return;

            _duration = reapplied._duration + (Time.time - StarteAt);
        }
    }
}
