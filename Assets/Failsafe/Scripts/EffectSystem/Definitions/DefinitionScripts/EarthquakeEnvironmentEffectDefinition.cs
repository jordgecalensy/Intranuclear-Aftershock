using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public struct EarthquakeEnvironmentStage
    {
        [Min(0f)] public float MinPower;
        [Min(0f)] public float HorizontalForce;
        [Min(0f)] public float VerticalForce;
        [Min(0.01f)] public float Duration;
        [Min(0.01f)] public float TickInterval;
        public bool DestroyObjects;
        [Min(0f)] public float DestroyStartTime;
        [Min(0f)] public float DestroyEndTime;
    }

    [CreateAssetMenu(
        fileName = "EarthquakeEnvironmentEffectDefinition",
        menuName = "Failsafe/Effects/Environment/Earthquake")]
    public sealed class EarthquakeEnvironmentEffectDefinition : EffectDefinition
    {
        [SerializeField] private EarthquakeEnvironmentStage[] _stages;
        [SerializeField] private ForceMode _forceMode = ForceMode.Impulse;

        public override bool CanApply(EffectContext context)
        {
            return TrySelectStage(context.Power, out _) &&
                   context.TryGet(out EarthquakeEnvironmentZone zone) &&
                   zone != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!TrySelectStage(
                    context.Power,
                    out EarthquakeEnvironmentStage stage) ||
                !context.TryGet(out EarthquakeEnvironmentZone zone) ||
                zone == null)
            {
                return null;
            }

            float duration = context.HasDurationOverride
                ? Mathf.Max(stage.Duration, context.DurationOverride)
                : stage.Duration;

            float destroyStartTime = stage.DestroyObjects
                ? Mathf.Clamp(stage.DestroyStartTime, 0f, duration)
                : 0f;

            float destroyEndTime = stage.DestroyObjects
                ? Mathf.Clamp(stage.DestroyEndTime, destroyStartTime, duration)
                : 0f;

            return new EarthquakeEnvironmentEffect(
                zone,
                stage.HorizontalForce,
                stage.VerticalForce,
                duration,
                stage.TickInterval,
                _forceMode,
                stage.DestroyObjects,
                destroyStartTime,
                destroyEndTime);
        }

        public override string GetStackKey(EffectContext context)
        {
            return "environment.earthquake";
        }

        private bool TrySelectStage(
            float power,
            out EarthquakeEnvironmentStage selected)
        {
            selected = default;

            if (_stages == null || _stages.Length == 0)
                return false;

            bool found = false;
            float selectedThreshold = float.NegativeInfinity;

            for (int i = 0; i < _stages.Length; i++)
            {
                EarthquakeEnvironmentStage candidate = _stages[i];

                if (power < candidate.MinPower ||
                    candidate.MinPower < selectedThreshold)
                {
                    continue;
                }

                selected = candidate;
                selectedThreshold = candidate.MinPower;
                found = true;
            }

            return found &&
                   selected.Duration > 0f &&
                   selected.TickInterval > 0f;
        }
    }
}
