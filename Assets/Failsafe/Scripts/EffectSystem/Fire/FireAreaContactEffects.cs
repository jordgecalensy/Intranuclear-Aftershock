using System;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Finds unique targets inside one fire area and applies its bundles.
    /// </summary>
    public sealed class FireAreaContactEffects
    {
        private const float MinimumTickInterval = 0.02f;

        private readonly GameObject _source;
        private readonly Collider[] _colliders = new Collider[256];
        private readonly HashSet<Transform> _seenTargets = new(128);

        private float _nextTickAt;

        public FireAreaContactEffects(GameObject source)
        {
            _source = source != null
                ? source
                : throw new ArgumentNullException(nameof(source));
        }

        public void Initialize(float currentTime, float tickInterval)
        {
            _nextTickAt = currentTime + NormalizeInterval(tickInterval);
            _seenTargets.Clear();
        }

        public void Tick(
            float currentTime,
            Vector3 center,
            float radius,
            LayerMask targetMask,
            int maxTargets,
            float tickInterval,
            FireAreaAdvanced.Tier tier,
            float weakContactDps,
            float mediumContactDps,
            float strongContactDps,
            float dotDpsPerIntensity,
            float mediumDotIntensity,
            float strongDotIntensity,
            IEffectApplicationService effects,
            EffectBundle contactEffects,
            EffectBundle burnEffects)
        {
            if (currentTime < _nextTickAt)
                return;

            float interval = NormalizeInterval(tickInterval);
            _nextTickAt = currentTime + interval;

            if (effects == null || maxTargets <= 0)
                return;

            _seenTargets.Clear();

            int count = Physics.OverlapSphereNonAlloc(
                center,
                Mathf.Max(0f, radius),
                _colliders,
                targetMask,
                QueryTriggerInteraction.Collide);
            float contactDps = ResolveContactDps(
                tier,
                weakContactDps,
                mediumContactDps,
                strongContactDps);
            int processed = 0;

            for (int i = 0; i < count && processed < maxTargets; i++)
            {
                Collider targetCollider = _colliders[i];
                Transform targetRoot =
                    ColliderTargetResolver.ResolveRoot(targetCollider);

                if (targetRoot == null || !_seenTargets.Add(targetRoot))
                    continue;

                processed++;
                ApplyContactEffect(
                    effects,
                    contactEffects,
                    targetCollider,
                    targetRoot,
                    contactDps * interval);
                ApplyBurnEffect(
                    effects,
                    burnEffects,
                    targetCollider,
                    targetRoot,
                    tier,
                    dotDpsPerIntensity,
                    mediumDotIntensity,
                    strongDotIntensity);
            }
        }

        public void Clear()
        {
            _seenTargets.Clear();
            Array.Clear(_colliders, 0, _colliders.Length);
        }

        private void ApplyContactEffect(
            IEffectApplicationService effects,
            EffectBundle bundle,
            Collider targetCollider,
            Transform targetRoot,
            float power)
        {
            if (bundle == null || power <= 0f)
                return;

            EffectContext context = ContactEffectContextFactory.Create(
                _source,
                targetCollider,
                targetRoot,
                power);
            effects.Apply(bundle, context);
        }

        private void ApplyBurnEffect(
            IEffectApplicationService effects,
            EffectBundle bundle,
            Collider targetCollider,
            Transform targetRoot,
            FireAreaAdvanced.Tier tier,
            float dpsPerIntensity,
            float mediumIntensity,
            float strongIntensity)
        {
            if (bundle == null || tier == FireAreaAdvanced.Tier.Weak)
                return;

            float tierIntensity = tier == FireAreaAdvanced.Tier.Medium
                ? mediumIntensity
                : strongIntensity;
            float power = dpsPerIntensity * tierIntensity;

            EffectContext context = ContactEffectContextFactory.Create(
                _source,
                targetCollider,
                targetRoot,
                power);
            effects.Apply(bundle, context);
        }

        private static float ResolveContactDps(
            FireAreaAdvanced.Tier tier,
            float weak,
            float medium,
            float strong)
        {
            return tier switch
            {
                FireAreaAdvanced.Tier.Weak => weak,
                FireAreaAdvanced.Tier.Medium => medium,
                _ => strong
            };
        }

        private static float NormalizeInterval(float interval)
        {
            return Mathf.Max(MinimumTickInterval, interval);
        }
    }
}
