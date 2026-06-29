using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class TimedStatusEffect : Effect, IReapplicableEffect, IRegisteredStatusEffect
    {
        private readonly StatusEffectState _state;
        private readonly StatusEffectType _statusType;
        private readonly IReadOnlyList<StatusEffectType> _removeStatusesOnApply;
        private readonly IReadOnlyList<StatusEffectType> _immunityStatusesOnEnd;
        private readonly float _immunityDurationOnEnd;

        private bool _cleared;

        public StatusEffectType StatusType => _statusType;

        public TimedStatusEffect(
            StatusEffectState state,
            StatusEffectType statusType,
            float duration,
            IReadOnlyList<StatusEffectType> removeStatusesOnApply,
            IReadOnlyList<StatusEffectType> immunityStatusesOnEnd,
            float immunityDurationOnEnd)
        {
            _state = state;
            _statusType = statusType;
            _duration = Mathf.Max(0f, duration);
            _removeStatusesOnApply = removeStatusesOnApply;
            _immunityStatusesOnEnd = immunityStatusesOnEnd;
            _immunityDurationOnEnd = Mathf.Max(0f, immunityDurationOnEnd);

            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            if (_state == null)
                return;

            if (!_state.CanReceive(_statusType))
            {
                Debug.Log($"[TimedStatusEffect] {_state.name}: blocked by immunity {_statusType}", _state);
                _duration = 0f;
                return;
            }

            _state.RemoveStatuses(_removeStatusesOnApply);
            _state.RegisterStatus(_statusType, this);

            Debug.Log($"[TimedStatusEffect] {_state.name}: apply {_statusType} for {_duration:0.00}s", _state);
        }

        public override void ClearEffect()
        {
            if (_cleared)
                return;

            _cleared = true;

            if (_state == null)
                return;

            _state.UnregisterStatus(_statusType, this);
            _state.AddTemporaryImmunity(_immunityStatusesOnEnd, _immunityDurationOnEnd);

            Debug.Log($"[TimedStatusEffect] {_state.name}: clear {_statusType}", _state);
        }

        public void ForceClearFromStatusState()
        {
            ClearEffect();
            _duration = 0f;
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not TimedStatusEffect reapplied)
                return;

            _duration = (Time.time - StarteAt) + reapplied._duration;

            Debug.Log($"[TimedStatusEffect] {_state.name}: refresh {_statusType} for {reapplied._duration:0.00}s", _state);
        }
    }
}