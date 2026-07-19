using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class StagedStatusEffect :
        Effect,
        IReapplicableEffect,
        IRegisteredStatusEffect,
        IStagedStatusEffect
    {
        private readonly StatusEffectState _state;
        private readonly StatusEffectType _statusType;

        private readonly float _maxBuildUp;
        private readonly bool _clampBuildUpToMax;
        private readonly StagedStatusStage[] _stages;

        private readonly IReadOnlyList<StatusEffectType> _removeStatusesOnApply;
        private readonly IReadOnlyList<StatusEffectType> _immunityStatusesOnEnd;
        private readonly float _immunityDurationOnEnd;

        private float _buildUpValue;
        private int _currentStage;
        private bool _cleared;

        public StatusEffectType StatusType => _statusType;
        public int CurrentStage => _currentStage;
        public float BuildUpValue => _buildUpValue;

        public StagedStatusEffect(
            StatusEffectState state,
            StatusEffectType statusType,
            float duration,
            float buildUpPerApplication,
            float maxBuildUp,
            bool clampBuildUpToMax,
            StagedStatusStage[] stages,
            IReadOnlyList<StatusEffectType> removeStatusesOnApply,
            IReadOnlyList<StatusEffectType> immunityStatusesOnEnd,
            float immunityDurationOnEnd)
        {
            _state = state;
            _statusType = statusType;

            _duration = Mathf.Max(0f, duration);

            _buildUpValue = Mathf.Max(0f, buildUpPerApplication);
            _maxBuildUp = Mathf.Max(0f, maxBuildUp);
            _clampBuildUpToMax = clampBuildUpToMax;
            _stages = stages;

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
                Debug.Log($"[StagedStatusEffect] {_state.name}: blocked by immunity {_statusType}", _state);
                _duration = 0f;
                return;
            }

            ClampBuildUpIfNeeded();
            RecalculateStage();

            _state.RemoveStatuses(_removeStatusesOnApply);
            _state.RegisterStatus(_statusType, this);

            Debug.Log(
                $"[StagedStatusEffect] {_state.name}: apply {_statusType}, stage {_currentStage}, buildUp {_buildUpValue:0.00}, duration {_duration:0.00}s",
                _state);
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

            Debug.Log($"[StagedStatusEffect] {_state.name}: clear {_statusType}", _state);
        }

        public void ForceClearFromStatusState()
        {
            ClearEffect();
            _duration = 0f;
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not StagedStatusEffect reapplied)
                return;

            int oldStage = _currentStage;

            _buildUpValue += reapplied._buildUpValue;
            ClampBuildUpIfNeeded();
            RecalculateStage();

            _duration = (Time.time - StarteAt) + reapplied._duration;

            if (_currentStage != oldStage)
            {
                Debug.Log(
                    $"[StagedStatusEffect] {_state.name}: {_statusType} stage {oldStage} -> {_currentStage}, buildUp {_buildUpValue:0.00}",
                    _state);
            }
            else
            {
                Debug.Log(
                    $"[StagedStatusEffect] {_state.name}: refresh {_statusType}, stage {_currentStage}, buildUp {_buildUpValue:0.00}",
                    _state);
            }
        }

        private void ClampBuildUpIfNeeded()
        {
            if (!_clampBuildUpToMax)
                return;

            _buildUpValue = Mathf.Min(_buildUpValue, _maxBuildUp);
        }

        private void RecalculateStage()
        {
            _currentStage = StagedStatusEffectDefinition.CalculateStage(
                _buildUpValue,
                _stages);
        }
    }
}