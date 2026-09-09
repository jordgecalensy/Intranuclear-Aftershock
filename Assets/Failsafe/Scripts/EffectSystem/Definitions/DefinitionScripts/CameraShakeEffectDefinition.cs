using System;
using Failsafe.PlayerMovements;
using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public struct CameraShakeStage
    {
        [Min(0f)] public float MinPower;
        [Min(0f)] public float Intensity;
        [Min(0.01f)] public float Duration;
        [Min(0f)] public float Frequency;
        [Min(0f)] public float FadeInFraction;
        [Min(0f)] public float FadeInMaximum;
        [Min(0f)] public float FadeOutFraction;
        [Min(0f)] public float FadeOutMaximum;
    }

    [CreateAssetMenu(
        fileName = "CameraShakeEffectDefinition",
        menuName = "Failsafe/Effects/Feedback/Camera Shake")]
    public sealed class CameraShakeEffectDefinition : EffectDefinition
    {
        [SerializeField] private CameraShakeStage[] _stages;

        public override bool CanApply(EffectContext context)
        {
            return TrySelectStage(context.Power, out _) &&
                   TryResolveRotation(context, out _);
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!TrySelectStage(context.Power, out CameraShakeStage stage) ||
                !TryResolveRotation(context, out PlayerRotationController rotation))
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

            return new CameraShakeEffect(
                rotation,
                stage.Intensity,
                duration,
                stage.Frequency,
                fadeInDuration,
                fadeOutDuration);
        }

        public override string GetStackKey(EffectContext context)
        {
            return "feedback.camera-shake";
        }

        private bool TrySelectStage(
            float power,
            out CameraShakeStage selected)
        {
            selected = default;

            if (_stages == null || _stages.Length == 0)
                return false;

            bool found = false;
            float selectedThreshold = float.NegativeInfinity;

            for (int i = 0; i < _stages.Length; i++)
            {
                CameraShakeStage candidate = _stages[i];

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
                   selected.Intensity > 0f;
        }

        private static bool TryResolveRotation(
            EffectContext context,
            out PlayerRotationController rotation)
        {
            rotation = null;

            if (!EffectTargetResolver.TryResolve(
                    context,
                    out PlayerController playerController))
            {
                return false;
            }

            rotation = playerController.PlayerRotationController;
            return rotation != null;
        }
    }
}
