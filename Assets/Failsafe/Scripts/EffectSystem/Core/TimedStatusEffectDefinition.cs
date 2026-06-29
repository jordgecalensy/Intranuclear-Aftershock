using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "TimedStatusEffectDefinition",
        menuName = "Failsafe/Effects/Statuses/Timed Status")]
    public class TimedStatusEffectDefinition : EffectDefinition, IStatusEffectDefinition, IStopEffectBundleOnStatusReaction
    {
        [Header("Status")]
        [SerializeField] private StatusEffectType _statusType = StatusEffectType.None;
        [SerializeField] private float _duration = 5f;

        [Header("Target")]
        [Tooltip("Если true, StatusEffectState будет автоматически добавлен на цель.")]
        [SerializeField] private bool _autoAddStatusState = true;

        [Header("On Apply")]
        [Tooltip("Статусы, которые будут сняты при наложении этого статуса.")]
        [SerializeField] private StatusEffectType[] _removeStatusesOnApply;

        [Header("On End")]
        [Tooltip("Статусы, к которым цель временно получает иммунитет после окончания этого статуса.")]
        [SerializeField] private StatusEffectType[] _immunityStatusesOnEnd;

        [Tooltip("Длительность иммунитета после окончания статуса.")]
        [SerializeField] private float _immunityDurationOnEnd = 0f;
        [Header("Reaction")]
        [Tooltip("Если true, при статусной реакции оставшиеся эффекты текущего bundle не будут применены.")]
        [SerializeField] private bool _stopEffectBundleOnStatusReaction = false;

        public bool StopEffectBundleOnStatusReaction => _stopEffectBundleOnStatusReaction;
        public StatusEffectType StatusType => _statusType;

        public override bool CanApply(EffectContext context)
        {
            if (_statusType == StatusEffectType.None)
                return false;

            return StatusEffectStateResolver.TryResolve(
                       context,
                       _autoAddStatusState,
                       out StatusEffectState state) &&
                   state != null &&
                   state.CanReceive(_statusType);
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

            return new TimedStatusEffect(
                state,
                _statusType,
                _duration,
                _removeStatusesOnApply,
                _immunityStatusesOnEnd,
                _immunityDurationOnEnd);
        }

        public override string GetStackKey(EffectContext context)
        {
            if (StatusEffectStateResolver.TryResolve(
                    context,
                    false,
                    out StatusEffectState state) &&
                state != null)
            {
                return $"status.{_statusType}.{state.GetInstanceID()}";
            }

            if (context.HitCollider != null)
                return $"status.{_statusType}.collider.{context.HitCollider.GetInstanceID()}";

            if (context.TargetObject != null)
                return $"status.{_statusType}.target.{context.TargetObject.GetInstanceID()}";

            return $"status.{_statusType}";
        }
    }
}