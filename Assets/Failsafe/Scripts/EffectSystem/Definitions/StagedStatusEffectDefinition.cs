using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "StagedStatusEffectDefinition",
        menuName = "Failsafe/Effects/Statuses/Staged Status")]
    public class StagedStatusEffectDefinition :
        EffectDefinition,
        IStatusEffectDefinition,
        IStagedStatusEffectDefinition,
        IStopEffectBundleOnStatusReaction
    {
        [Header("Status")]
        [SerializeField] private StatusEffectType _statusType = StatusEffectType.Cold;

        [Tooltip("Сколько секунд статус живёт без повторного получения.")]
        [SerializeField] private float _duration = 5f;

        [Header("Build Up")]
        [Tooltip("Сколько накопления добавляет одно применение эффекта.")]
        [SerializeField] private float _buildUpPerApplication = 1f;

        [Tooltip("Максимальное накопление.")]
        [SerializeField] private float _maxBuildUp = 3f;

        [Tooltip("Ограничивать накопление максимальным значением.")]
        [SerializeField] private bool _clampBuildUpToMax = true;

        [Header("Stages")]
        [SerializeField] private StagedStatusStage[] _stages =
        {
            new StagedStatusStage(),
            new StagedStatusStage(),
            new StagedStatusStage()
        };

        [Header("Target")]
        [SerializeField] private bool _autoAddStatusState = true;

        [Header("On Apply")]
        [SerializeField] private StatusEffectType[] _removeStatusesOnApply;

        [Header("On End")]
        [SerializeField] private StatusEffectType[] _immunityStatusesOnEnd;

        [SerializeField] private float _immunityDurationOnEnd = 0f;

        public StatusEffectType StatusType => _statusType;
        
        [Header("Reaction")]
        [Tooltip("Если true, при статусной реакции оставшиеся эффекты текущего bundle не будут применены.")]
        [SerializeField] private bool _stopEffectBundleOnStatusReaction = false;

        public bool StopEffectBundleOnStatusReaction => _stopEffectBundleOnStatusReaction;

        public override bool CanApply(EffectContext context)
        {
            if (_statusType == StatusEffectType.None)
                return false;

            if (!StatusEffectStateResolver.TryResolve(
                    context,
                    _autoAddStatusState,
                    out StatusEffectState state))
            {
                return false;
            }

            return state != null && state.CanReceive(_statusType);
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!StatusEffectStateResolver.TryResolve(
                    context,
                    _autoAddStatusState,
                    out StatusEffectState state))
            {
                return null;
            }

            if (state == null)
                return null;

            return new StagedStatusEffect(
                state,
                _statusType,
                _duration,
                _buildUpPerApplication,
                _maxBuildUp,
                _clampBuildUpToMax,
                _stages,
                _removeStatusesOnApply,
                _immunityStatusesOnEnd,
                _immunityDurationOnEnd);
        }

        public int PredictStageAfterApply(StatusEffectState state, EffectContext context)
        {
            if (state == null)
                return CalculateStage(_buildUpPerApplication, _stages);

            float currentBuildUp = state.GetStatusBuildUpValue(_statusType);
            float predictedBuildUp = currentBuildUp + Mathf.Max(0f, _buildUpPerApplication);

            if (_clampBuildUpToMax)
                predictedBuildUp = Mathf.Min(predictedBuildUp, Mathf.Max(0f, _maxBuildUp));

            return CalculateStage(predictedBuildUp, _stages);
        }

        public override string GetStackKey(EffectContext context)
        {
            if (StatusEffectStateResolver.TryResolve(
                    context,
                    false,
                    out StatusEffectState state) &&
                state != null)
            {
                return $"status.staged.{_statusType}.{state.GetInstanceID()}";
            }

            if (context.HitCollider != null)
                return $"status.staged.{_statusType}.collider.{context.HitCollider.GetInstanceID()}";

            if (context.TargetObject != null)
                return $"status.staged.{_statusType}.target.{context.TargetObject.GetInstanceID()}";

            return $"status.staged.{_statusType}";
        }

        public static int CalculateStage(
            float buildUpValue,
            StagedStatusStage[] stages)
        {
            if (buildUpValue <= 0f)
                return 0;

            if (stages == null || stages.Length == 0)
                return 1;

            int result = 0;

            for (int i = 0; i < stages.Length; i++)
            {
                StagedStatusStage stage = stages[i];

                if (stage == null)
                    continue;

                if (buildUpValue >= stage.MinBuildUp)
                    result = Mathf.Max(result, stage.Stage);
            }

            if (result <= 0)
                result = 1;

            return result;
        }
    }
}