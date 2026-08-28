using System;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public interface IEffectPresentationSource
    {
        event Action<EffectPresentation> EffectAdded;
        event Action<EffectPresentation> EffectRefreshed;
        event Action<EffectPresentation> EffectRemoved;

        void GetActiveEffects(List<EffectPresentation> results);
    }

    public sealed class EffectPresentation
    {
        public Effect RuntimeEffect { get; }
        public EffectDefinition Definition { get; }
        public GameObject Target { get; }
        public float AppliedAt { get; private set; }
        public float AppliedDuration { get; private set; }

        public float RemainingDuration
        {
            get
            {
                float expiresAt = RuntimeEffect.ElapsedAt;

                if (float.IsPositiveInfinity(expiresAt))
                    return Mathf.Infinity;

                return Mathf.Max(0f, expiresAt - Time.time);
            }
        }

        public float NormalizedRemaining
        {
            get
            {
                if (float.IsPositiveInfinity(AppliedDuration))
                    return 1f;

                if (AppliedDuration <= 0f)
                    return 0f;

                return Mathf.Clamp01(RemainingDuration / AppliedDuration);
            }
        }

        public int Stage => RuntimeEffect is IStagedStatusEffect stagedEffect
            ? stagedEffect.CurrentStage
            : 0;

        internal EffectPresentation(
            Effect runtimeEffect,
            EffectDefinition definition,
            GameObject target,
            float appliedAt,
            float appliedDuration)
        {
            RuntimeEffect = runtimeEffect;
            Definition = definition;
            Target = target;
            AppliedAt = appliedAt;
            AppliedDuration = appliedDuration;
        }

        internal void Refresh(float appliedAt, float appliedDuration)
        {
            AppliedAt = appliedAt;
            AppliedDuration = appliedDuration;
        }
    }
}
