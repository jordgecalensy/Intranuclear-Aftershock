using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class StagedSpeedModifierEffect : Effect, IReapplicableEffect
    {
        private readonly StatusEffectState _state;
        private readonly PlayerMovementController _controller;
        private readonly StatusEffectType _observedStatus;
        private readonly StagedSpeedModifierStage[] _stageModifiers;
        private readonly int _modifierId;
        private readonly bool _clearWhenStatusMissing;

        private bool _cleared;

        public StagedSpeedModifierEffect(
            StatusEffectState state,
            PlayerMovementController controller,
            StatusEffectType observedStatus,
            float duration,
            StagedSpeedModifierStage[] stageModifiers,
            int modifierId,
            bool unique,
            bool clearWhenStatusMissing)
        {
            _state = state;
            _controller = controller;
            _observedStatus = observedStatus;
            _duration = Mathf.Max(0f, duration);
            _stageModifiers = stageModifiers;
            _modifierId = modifierId;
            _clearWhenStatusMissing = clearWhenStatusMissing;

            IsUniqueEffect = unique;
        }

        public override void ApplyEffect()
        {
            ApplyCurrentStageModifier();
        }

        public override void Update()
        {
            if (_state == null || _controller == null)
            {
                EndNow();
                return;
            }

            if (_clearWhenStatusMissing && !_state.HasStatus(_observedStatus))
            {
                EndNow();
                return;
            }

            ApplyCurrentStageModifier();
        }

        public override void ClearEffect()
        {
            if (_cleared)
                return;

            _cleared = true;

            if (_controller != null)
                _controller.RemoveSpeedModifier(_modifierId);
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not StagedSpeedModifierEffect reapplied)
                return;

            _duration = (Time.time - StarteAt) + reapplied._duration;

            ApplyCurrentStageModifier();
        }

        private void ApplyCurrentStageModifier()
        {
            if (_state == null || _controller == null)
                return;

            int stage = _state.GetStatusStage(_observedStatus);

            if (stage <= 0)
            {
                if (_clearWhenStatusMissing)
                {
                    EndNow();
                    return;
                }

                _controller.SetSpeedModifier(_modifierId, 1f);
                return;
            }

            float multiplier = ResolveMultiplier(stage);
            _controller.SetSpeedModifier(_modifierId, multiplier);
        }

        private float ResolveMultiplier(int stage)
        {
            if (_stageModifiers == null || _stageModifiers.Length == 0)
                return 1f;

            StagedSpeedModifierStage best = null;

            for (int i = 0; i < _stageModifiers.Length; i++)
            {
                StagedSpeedModifierStage candidate = _stageModifiers[i];

                if (candidate == null)
                    continue;

                if (candidate.Stage != stage)
                    continue;

                best = candidate;
                break;
            }

            if (best != null)
                return best.SpeedMultiplier;

            return 1f;
        }

        private void EndNow()
        {
            if (!_cleared && _controller != null)
                _controller.RemoveSpeedModifier(_modifierId);

            _duration = 0f;
        }
    }
}