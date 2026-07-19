using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public static class StatusResistanceUtility
    {
        public static float ApplyDurationMultiplier(
            StatusEffectState state,
            StatusEffectType statusType,
            float baseDuration)
        {
            baseDuration = Mathf.Max(0f, baseDuration);

            if (state == null)
                return baseDuration;

            return baseDuration * state.GetStatusDurationMultiplier(statusType);
        }

        public static float ApplyBuildUpMultiplier(
            StatusEffectState state,
            StatusEffectType statusType,
            float baseBuildUp)
        {
            baseBuildUp = Mathf.Max(0f, baseBuildUp);

            if (state == null)
                return baseBuildUp;

            return baseBuildUp * state.GetStatusBuildUpMultiplier(statusType);
        }
    }
}