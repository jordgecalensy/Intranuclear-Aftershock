using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class FrozenEffect : Effect, IReapplicableEffect, IRegisteredStatusEffect
    {
        private readonly StatusEffectState _state;
        private readonly FrozenPhysicsResponder _physicsResponder;
        private readonly Stasisable _stasisFallback;
        private readonly GameObject _source;

        private readonly IReadOnlyList<StatusEffectType> _removeStatusesOnApply;
        private readonly IReadOnlyList<StatusEffectType> _immunityStatusesOnEnd;
        private readonly float _immunityDurationOnEnd;

        private bool _cleared;

        public StatusEffectType StatusType => StatusEffectType.Frozen;

        public FrozenEffect(
            StatusEffectState state,
            FrozenPhysicsResponder physicsResponder,
            Stasisable stasisFallback,
            float duration,
            GameObject source,
            IReadOnlyList<StatusEffectType> removeStatusesOnApply,
            IReadOnlyList<StatusEffectType> immunityStatusesOnEnd,
            float immunityDurationOnEnd)
        {
            _state = state;
            _physicsResponder = physicsResponder;
            _stasisFallback = stasisFallback;
            _source = source;

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

            if (!_state.CanReceive(StatusEffectType.Frozen))
            {
                Debug.Log($"[FrozenEffect] {_state.name}: blocked by immunity Frozen", _state);
                _duration = 0f;
                return;
            }

            _state.RemoveStatuses(_removeStatusesOnApply);
            _state.RegisterStatus(StatusEffectType.Frozen, this);

            if (_physicsResponder != null)
            {
                _physicsResponder.ApplyFrozen(_duration, _source);
            }
            else if (_stasisFallback != null)
            {
                _stasisFallback.ApplyStasis(
                    _duration,
                    restoreVelocityOnExit: false,
                    source: _source);
            }

            Debug.Log($"[FrozenEffect] {_state.name}: apply Frozen for {_duration:0.00}s", _state);
        }

        public override void ClearEffect()
        {
            if (_cleared)
                return;

            _cleared = true;

            if (_physicsResponder != null)
            {
                _physicsResponder.ClearFrozen(_source);
            }
            else if (_stasisFallback != null)
            {
                _stasisFallback.ClearStasis(
                    restoreVelocityOnExit: false,
                    source: _source);
            }

            if (_state != null)
            {
                _state.UnregisterStatus(StatusEffectType.Frozen, this);
                _state.AddTemporaryImmunity(_immunityStatusesOnEnd, _immunityDurationOnEnd);

                Debug.Log($"[FrozenEffect] {_state.name}: clear Frozen", _state);
            }
        }

        public void ForceClearFromStatusState()
        {
            ClearEffect();
            _duration = 0f;
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not FrozenEffect reapplied)
                return;

            _duration = (Time.time - StarteAt) + reapplied._duration;

            if (_physicsResponder != null)
                _physicsResponder.ApplyFrozen(reapplied._duration, _source);

            Debug.Log($"[FrozenEffect] {_state.name}: refresh Frozen for {reapplied._duration:0.00}s", _state);
        }
    }
}