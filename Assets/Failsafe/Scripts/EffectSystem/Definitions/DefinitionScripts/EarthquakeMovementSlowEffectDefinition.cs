using System;
using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public struct EarthquakeMovementSlowStage
    {
        [Min(0f)] public float MinPower;
        [Range(0.01f, 1f)] public float Multiplier;
        [Min(0.01f)] public float Duration;
        [Min(0f)] public float FadeInFraction;
        [Min(0f)] public float FadeInMaximum;
        [Min(0f)] public float FadeOutFraction;
        [Min(0f)] public float FadeOutMaximum;
    }

    [CreateAssetMenu(
        fileName = "EarthquakeMovementSlowEffectDefinition",
        menuName = "Failsafe/Effects/Movement/Earthquake Slow")]
    public sealed class EarthquakeMovementSlowEffectDefinition : EffectDefinition
    {
        [SerializeField] private EarthquakeMovementSlowStage[] _stages;

        public override bool CanApply(EffectContext context)
        {
            return TrySelectStage(context.Power, out _) &&
                   EffectTargetResolver.TryResolve(
                       context,
                       out PlayerMovementController _);
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!TrySelectStage(
                    context.Power,
                    out EarthquakeMovementSlowStage stage) ||
                !EffectTargetResolver.TryResolve(
                    context,
                    out PlayerMovementController movementController))
            {
                return null;
            }

            float duration = context.HasDurationOverride
                ? Mathf.Max(stage.Duration, context.DurationOverride)
                : stage.Duration;

            float fadeInDuration = Mathf.Min(
                stage.FadeInMaximum,
                duration * stage.FadeInFraction);

            float fadeOutDuration = Mathf.Min(
                stage.FadeOutMaximum,
                duration * stage.FadeOutFraction);

            return new EarthquakeMovementSlowEffect(
                movementController,
                stage.Multiplier,
                duration,
                fadeInDuration,
                fadeOutDuration);
        }

        public override string GetStackKey(EffectContext context)
        {
            return "movement.earthquake-slow";
        }

        private bool TrySelectStage(
            float power,
            out EarthquakeMovementSlowStage selected)
        {
            selected = default;

            if (_stages == null || _stages.Length == 0)
                return false;

            bool found = false;
            float selectedThreshold = float.NegativeInfinity;

            for (int i = 0; i < _stages.Length; i++)
            {
                EarthquakeMovementSlowStage candidate = _stages[i];

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
                   selected.Multiplier > 0f;
        }
    }
}
