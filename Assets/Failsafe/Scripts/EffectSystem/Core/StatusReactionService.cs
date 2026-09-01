using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public interface IStatusReactionService
    {
        bool TryHandleBeforeApply(
            EffectDefinition definition,
            EffectContext context,
            IEffectApplicationService effectApplicationService);
    }

    public sealed class StatusReactionService : IStatusReactionService
    {
        private readonly StatusReactionProfile _profile;
        private int _reactionDepth;

        public StatusReactionService(StatusReactionProfile profile)
        {
            _profile = profile;
        }

        public bool TryHandleBeforeApply(
            EffectDefinition definition,
            EffectContext context,
            IEffectApplicationService effectApplicationService)
        {
            if (definition is not IStatusEffectDefinition statusDefinition)
                return false;

            StatusEffectType incomingStatus = statusDefinition.StatusType;

            if (incomingStatus == StatusEffectType.None)
                return false;

            if (!StatusEffectStateResolver.TryResolve(
                    context,
                    true,
                    out StatusEffectState state))
            {
                return false;
            }

            if (state == null)
                return false;

            if (state.IsImmune(incomingStatus))
            {
                EffectLog.Info(EffectLog.Status, $"[StatusReactionService] {state.name}: {incomingStatus} blocked by immunity.", state);
                return true;
            }

            if (incomingStatus == StatusEffectType.Shock &&
                state.HasStatus(StatusEffectType.Frozen))
            {
                EffectLog.Info(EffectLog.Status, $"[StatusReactionService] {state.name}: Shock blocked by Frozen.", state);
                return true;
            }

            if (TryHandleWetColdReaction(
                    definition,
                    incomingStatus,
                    state,
                    context,
                    effectApplicationService))
            {
                return true;
            }

            if (TryHandleWetShockReaction(
                    incomingStatus,
                    state,
                    context,
                    effectApplicationService))
            {
                return true;
            }

            return false;
        }

        private bool TryHandleWetColdReaction(
            EffectDefinition definition,
            StatusEffectType incomingStatus,
            StatusEffectState state,
            EffectContext context,
            IEffectApplicationService effectApplicationService)
        {
            int minColdStage = _profile != null
                ? _profile.MinColdStageForFrozen
                : 2;

            bool incomingColdOnWet =
                incomingStatus == StatusEffectType.Cold &&
                state.HasStatus(StatusEffectType.Wet);

            bool incomingWetOnCold =
                incomingStatus == StatusEffectType.Wet &&
                state.HasStatus(StatusEffectType.Cold);

            if (!incomingColdOnWet && !incomingWetOnCold)
                return false;

            int coldStage;

            if (incomingColdOnWet)
            {
                coldStage = PredictIncomingColdStage(
                    definition,
                    state,
                    context);
            }
            else
            {
                coldStage = state.GetStatusStage(StatusEffectType.Cold);
            }

            if (coldStage < minColdStage)
            {
                EffectLog.Info(EffectLog.Status,
                    $"[StatusReactionService] {state.name}: Wet + Cold ignored, Cold stage {coldStage} < {minColdStage}",
                    state);

                return false;
            }

            if (_profile == null || _profile.FrozenReactionBundle == null)
            {
                EffectLog.Warning(EffectLog.Status, "[StatusReactionService] FrozenReactionBundle is not assigned. Reaction skipped.", state);
                return false;
            }

            EffectLog.Info(EffectLog.Status,
                $"[StatusReactionService] {state.name}: Wet + Cold stage {coldStage} => Frozen",
                state);

            state.RemoveStatus(StatusEffectType.Wet);
            state.RemoveStatus(StatusEffectType.Cold);

            ApplyReactionBundle(
                _profile.FrozenReactionBundle,
                context,
                effectApplicationService);

            return true;
        }

        private int PredictIncomingColdStage(
            EffectDefinition definition,
            StatusEffectState state,
            EffectContext context)
        {
            if (definition is IStagedStatusEffectDefinition stagedDefinition)
                return stagedDefinition.PredictStageAfterApply(state, context);

            if (state.HasStatus(StatusEffectType.Cold))
                return Mathf.Max(1, state.GetStatusStage(StatusEffectType.Cold));

            return 1;
        }

        private bool TryHandleWetShockReaction(
            StatusEffectType incomingStatus,
            StatusEffectState state,
            EffectContext context,
            IEffectApplicationService effectApplicationService)
        {
            bool incomingShockOnWet =
                incomingStatus == StatusEffectType.Shock &&
                state.HasStatus(StatusEffectType.Wet);

            bool incomingWetOnShock =
                incomingStatus == StatusEffectType.Wet &&
                state.HasStatus(StatusEffectType.Shock);

            if (!incomingShockOnWet && !incomingWetOnShock)
                return false;

            if (_profile == null || _profile.StunReactionBundle == null)
            {
                EffectLog.Warning(EffectLog.Status, "[StatusReactionService] StunReactionBundle is not assigned. Reaction skipped.", state);
                return false;
            }

            EffectLog.Info(EffectLog.Status, $"[StatusReactionService] {state.name}: Wet + Shock => Stun", state);

            state.RemoveStatus(StatusEffectType.Shock);

            ApplyReactionBundle(
                _profile.StunReactionBundle,
                context,
                effectApplicationService);

            if (incomingShockOnWet)
                return true;

            return false;
        }

        private void ApplyReactionBundle(
            EffectBundle bundle,
            EffectContext context,
            IEffectApplicationService effectApplicationService)
        {
            if (bundle == null)
                return;

            if (effectApplicationService == null)
                return;

            if (_reactionDepth > 8)
            {
                EffectLog.Error(EffectLog.Status, "[StatusReactionService] Reaction recursion limit reached.");
                return;
            }

            _reactionDepth++;

            try
            {
                effectApplicationService.Apply(bundle, context);
            }
            finally
            {
                _reactionDepth--;
            }
        }
    }
}