using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class StatusResistanceModifierEffect : Effect, IReapplicableEffect
    {
        private readonly StatusEffectState _state;
        private readonly string _sourceId;
        private readonly StatusEffectType _statusType;
        private readonly bool _log;

        private float _durationMultiplier;
        private float _buildUpMultiplier;
        private float _baseDuration;

        public StatusResistanceModifierEffect(
            StatusEffectState state,
            string sourceId,
            StatusEffectType statusType,
            float durationMultiplier,
            float buildUpMultiplier,
            float duration,
            bool unique,
            bool log)
        {
            _state = state;
            _sourceId = sourceId;
            _statusType = statusType;
            _durationMultiplier = Mathf.Max(0f, durationMultiplier);
            _buildUpMultiplier = Mathf.Max(0f, buildUpMultiplier);
            _baseDuration = Mathf.Max(0.01f, duration);
            _duration = _baseDuration;
            _log = log;

            IsUniqueEffect = unique;
        }

        public override void ApplyEffect()
        {
            if (_state == null)
                return;

            _state.AddRuntimeResistanceModifier(
                _sourceId,
                _statusType,
                _durationMultiplier,
                _buildUpMultiplier);

            if (_log)
            {
                EffectLog.Info(EffectLog.Resistance,
                    $"[StatusResistanceModifierEffect] {_state.name}: apply {_statusType}, duration x{_durationMultiplier:0.###}, buildUp x{_buildUpMultiplier:0.###} for {_baseDuration:0.###}s",
                    _state);
            }
        }

        public override void ClearEffect()
        {
            if (_state == null)
                return;

            _state.RemoveRuntimeResistanceModifier(
                _sourceId,
                _statusType);

            if (_log)
            {
                EffectLog.Info(EffectLog.Resistance,
                    $"[StatusResistanceModifierEffect] {_state.name}: clear {_statusType} modifier {_sourceId}",
                    _state);
            }
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not StatusResistanceModifierEffect reapplied)
                return;

            _durationMultiplier = reapplied._durationMultiplier;
            _buildUpMultiplier = reapplied._buildUpMultiplier;
            _baseDuration = reapplied._baseDuration;

            _duration = (Time.time - StarteAt) + _baseDuration;

            if (_state != null)
            {
                _state.AddRuntimeResistanceModifier(
                    _sourceId,
                    _statusType,
                    _durationMultiplier,
                    _buildUpMultiplier);
            }

            if (_log && _state != null)
            {
                EffectLog.Info(EffectLog.Resistance,
                    $"[StatusResistanceModifierEffect] {_state.name}: refresh {_statusType}, duration x{_durationMultiplier:0.###}, buildUp x{_buildUpMultiplier:0.###}",
                    _state);
            }
        }
    }
}