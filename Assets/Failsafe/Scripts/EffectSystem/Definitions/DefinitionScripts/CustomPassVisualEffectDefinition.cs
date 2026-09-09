using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "CustomPassVisualEffectDefinition",
        menuName = "Failsafe/Effects/Visual/Custom Pass Visual")]
    public sealed class CustomPassVisualEffectDefinition : EffectDefinition
    {
        [Header("Visual")]
        [SerializeField] private Material _material;
        [SerializeField] private string _runtimeObjectName = "CustomPassVisualEffect";
        [SerializeField] private CustomPassInjectionPoint _injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

        [Header("Timing")]
        [SerializeField] private float _duration = 3f;

        [Header("Debug")]
        [SerializeField] private bool _log = false;

        public override bool CanApply(EffectContext context)
        {
            if (_material == null)
            {
                if (_log)
                    EffectLog.Warning(EffectLog.Feedback, "[CustomPassVisualEffectDefinition] Material is null.", this);

                return false;
            }

            return _duration > 0f;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (_material == null)
                return null;

            return new CustomPassVisualEffect(
                _material,
                _duration,
                _runtimeObjectName,
                _injectionPoint,
                _log);
        }

        public override string GetStackKey(EffectContext context)
        {
            return $"visual.custom-pass.{GetInstanceID()}";
        }
    }
}
