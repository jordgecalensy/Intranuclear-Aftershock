using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "RemoveStatusesEffectDefinition",
        menuName = "Failsafe/Effects/Statuses/Remove Statuses")]
    public class RemoveStatusesEffectDefinition : EffectDefinition
    {
        [Header("Statuses")]
        [SerializeField] private StatusEffectType[] _statusesToRemove =
        {
            StatusEffectType.Frozen
        };

        [Header("Target")]
        [SerializeField] private bool _autoAddStatusState = false;

        public override bool CanApply(EffectContext context)
        {
            return StatusEffectStateResolver.TryResolve(
                       context,
                       _autoAddStatusState,
                       out StatusEffectState state) &&
                   state != null;
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

            return new RemoveStatusesEffect(
                state,
                _statusesToRemove);
        }

        public override string GetStackKey(EffectContext context)
        {
            return $"remove-statuses.{GetInstanceID()}";
        }
    }
}