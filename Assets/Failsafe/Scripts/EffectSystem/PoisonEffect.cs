using System.Collections.Generic;
using Failsafe.Player.Model;
using Failsafe.Scripts.Damage;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class PoisonEffect :
        Effect,
        IReapplicableEffect,
        IRegisteredStatusEffect,
        IStagedStatusEffect
    {
        private readonly StatusEffectState _state;
        private readonly IStamina _stamina;
        private readonly DamageTarget _damageTarget;
        private readonly GameObject _source;
        private readonly Vector3 _point;
        private readonly Vector3 _direction;
        private readonly float _power;

        private readonly float _maxBuildUp;
        private readonly bool _clampBuildUpToMax;
        private readonly PoisonStageSettings[] _stages;

        private readonly IReadOnlyList<StatusEffectType> _removeStatusesOnApply;
        private readonly IReadOnlyList<StatusEffectType> _immunityStatusesOnEnd;
        private readonly float _immunityDurationOnEnd;

        private float _buildUpValue;
        private int _currentStage;
        private float _damageTimer;
        private bool _cleared;

        public StatusEffectType StatusType => StatusEffectType.Poison;
        public int CurrentStage => _currentStage;
        public float BuildUpValue => _buildUpValue;

        public PoisonEffect(
            StatusEffectState state,
            IStamina stamina,
            DamageTarget damageTarget,
            GameObject source,
            Vector3 point,
            Vector3 direction,
            float power,
            float duration,
            float buildUpPerApplication,
            float maxBuildUp,
            bool clampBuildUpToMax,
            PoisonStageSettings[] stages,
            IReadOnlyList<StatusEffectType> removeStatusesOnApply,
            IReadOnlyList<StatusEffectType> immunityStatusesOnEnd,
            float immunityDurationOnEnd)
        {
            _state = state;
            _stamina = stamina;
            _damageTarget = damageTarget;
            _source = source;
            _point = point;
            _direction = direction;
            _power = Mathf.Max(0f, power);

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

            if (!_state.CanReceive(StatusEffectType.Poison))
            {
                Debug.Log($"[PoisonEffect] {_state.name}: blocked by immunity Poison", _state);
                _duration = 0f;
                return;
            }

            ClampBuildUpIfNeeded();
            RecalculateStage();

            _state.RemoveStatuses(_removeStatusesOnApply);
            _state.RegisterStatus(StatusEffectType.Poison, this);

            ResetDamageTimerForCurrentStage();

            Debug.Log(
                $"[PoisonEffect] {_state.name}: apply Poison stage {_currentStage}, buildUp {_buildUpValue:0.00}, duration {_duration:0.00}s",
                _state);
        }

        public override void Update()
        {
            PoisonStageSettings stage = GetCurrentStageSettings();

            if (stage == null)
                return;

            TickStamina(stage);
            TickDamage(stage);
        }

        public override void ClearEffect()
        {
            if (_cleared)
                return;

            _cleared = true;

            if (_state == null)
                return;

            _state.UnregisterStatus(StatusEffectType.Poison, this);
            _state.AddTemporaryImmunity(_immunityStatusesOnEnd, _immunityDurationOnEnd);

            Debug.Log($"[PoisonEffect] {_state.name}: clear Poison", _state);
        }

        public void ForceClearFromStatusState()
        {
            ClearEffect();
            _duration = 0f;
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not PoisonEffect reapplied)
                return;

            int oldStage = _currentStage;

            _buildUpValue += reapplied._buildUpValue;
            ClampBuildUpIfNeeded();
            RecalculateStage();

            _duration = (Time.time - StarteAt) + reapplied._duration;

            if (_currentStage != oldStage)
            {
                ResetDamageTimerForCurrentStage();

                Debug.Log(
                    $"[PoisonEffect] {_state.name}: Poison stage {oldStage} -> {_currentStage}, buildUp {_buildUpValue:0.00}",
                    _state);
            }
            else
            {
                Debug.Log(
                    $"[PoisonEffect] {_state.name}: refresh Poison stage {_currentStage}, buildUp {_buildUpValue:0.00}",
                    _state);
            }
        }

        private void TickStamina(PoisonStageSettings stage)
        {
            if (_stamina == null)
                return;

            float spend = stage.StaminaSpendPerSecond * Time.deltaTime;

            if (spend <= 0f)
                return;

            _stamina.SpendStamina(spend);
        }

        private void TickDamage(PoisonStageSettings stage)
        {
            if (!_damageTarget.IsValid)
                return;

            if (stage.DamagePerTick <= 0f)
                return;

            _damageTimer -= Time.deltaTime;

            if (_damageTimer > 0f)
                return;

            var damage = new DamageInfo(
                stage.DamagePerTick,
                DamageType.Poison,
                DamageApplicationKind.DotTick,
                _source,
                _point,
                _direction,
                _power);

            DamageResistanceUtility.ApplyDamage(
                _damageTarget,
                damage);

            _damageTimer = stage.DamageTickInterval;

            Debug.Log(
                $"[PoisonEffect] {_state.name}: poison damage {stage.DamagePerTick:0.00} at stage {_currentStage}",
                _state);
        }

        private void ResetDamageTimerForCurrentStage()
        {
            PoisonStageSettings stage = GetCurrentStageSettings();

            if (stage == null || stage.DamagePerTick <= 0f)
            {
                _damageTimer = 0f;
                return;
            }

            _damageTimer = stage.DamageTickInterval;
        }

        private PoisonStageSettings GetCurrentStageSettings()
        {
            if (_stages == null || _stages.Length == 0)
                return null;

            PoisonStageSettings best = null;

            for (int i = 0; i < _stages.Length; i++)
            {
                PoisonStageSettings stage = _stages[i];

                if (stage == null)
                    continue;

                if (stage.Stage != _currentStage)
                    continue;

                best = stage;
                break;
            }

            return best;
        }

        private void ClampBuildUpIfNeeded()
        {
            if (!_clampBuildUpToMax)
                return;

            _buildUpValue = Mathf.Min(_buildUpValue, _maxBuildUp);
        }

        private void RecalculateStage()
        {
            _currentStage = PoisonEffectDefinition.CalculateStage(
                _buildUpValue,
                _stages);
        }
    }
}
