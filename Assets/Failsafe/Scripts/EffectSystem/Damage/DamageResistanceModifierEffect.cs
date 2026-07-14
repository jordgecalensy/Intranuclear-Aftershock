using Failsafe.Scripts.Damage;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Effects
{
    public class DamageResistanceModifierEffect : Effect, IReapplicableEffect
    {
        private readonly DamageResistanceComponent _resistanceComponent;
        private readonly string _sourceId;
        private readonly DamageType _damageType;
        private readonly bool _log;

        private float _multiplier;
        private float _baseDuration;

        public DamageResistanceModifierEffect(
            DamageResistanceComponent resistanceComponent,
            string sourceId,
            DamageType damageType,
            float multiplier,
            float duration,
            bool unique,
            bool log)
        {
            _resistanceComponent = resistanceComponent;
            _sourceId = sourceId;
            _damageType = damageType;
            _multiplier = Mathf.Max(0f, multiplier);
            _baseDuration = Mathf.Max(0.01f, duration);
            _duration = _baseDuration;
            _log = log;

            IsUniqueEffect = unique;
        }

        public override void ApplyEffect()
        {
            if (_resistanceComponent == null)
                return;

            _resistanceComponent.AddRuntimeModifier(
                _sourceId,
                _damageType,
                _multiplier);

            if (_log)
            {
                Debug.Log(
                    $"[DamageResistanceModifierEffect] {_resistanceComponent.name}: apply {_damageType} x{_multiplier:0.###} for {_baseDuration:0.###}s",
                    _resistanceComponent);
            }
        }

        public override void ClearEffect()
        {
            if (_resistanceComponent == null)
                return;

            _resistanceComponent.RemoveRuntimeModifier(
                _sourceId,
                _damageType);

            if (_log)
            {
                Debug.Log(
                    $"[DamageResistanceModifierEffect] {_resistanceComponent.name}: clear {_damageType} modifier {_sourceId}",
                    _resistanceComponent);
            }
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not DamageResistanceModifierEffect reapplied)
                return;

            _multiplier = reapplied._multiplier;
            _baseDuration = reapplied._baseDuration;

            _duration = (Time.time - StarteAt) + _baseDuration;

            if (_resistanceComponent != null)
            {
                _resistanceComponent.AddRuntimeModifier(
                    _sourceId,
                    _damageType,
                    _multiplier);
            }

            if (_log && _resistanceComponent != null)
            {
                Debug.Log(
                    $"[DamageResistanceModifierEffect] {_resistanceComponent.name}: refresh {_damageType} x{_multiplier:0.###} for {_baseDuration:0.###}s",
                    _resistanceComponent);
            }
        }
    }
}