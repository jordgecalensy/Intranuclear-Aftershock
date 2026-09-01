using System;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Tracks colliders touching one obstacle and applies its effect bundle
    /// at the configured interval.
    /// </summary>
    public sealed class ObstacleContactEffects
    {
        private readonly GameObject _source;
        private readonly Func<GameObject, bool> _isAllowedTarget;
        private readonly Dictionary<Transform, int> _overlapCount = new();
        private readonly Dictionary<Transform, float> _timers = new();
        private readonly Dictionary<Transform, Collider> _targetColliders = new();
        private readonly List<Transform> _targetsBuffer = new();

        public ObstacleContactEffects(
            GameObject source,
            Func<GameObject, bool> isAllowedTarget)
        {
            _source = source != null
                ? source
                : throw new ArgumentNullException(nameof(source));
            _isAllowedTarget =
                isAllowedTarget ??
                throw new ArgumentNullException(nameof(isAllowedTarget));
        }

        public void Enter(Collider other)
        {
            if (other == null)
                return;

            Transform targetTransform =
                ColliderTargetResolver.ResolveRoot(other);

            if (targetTransform == null ||
                !_isAllowedTarget(targetTransform.gameObject))
            {
                return;
            }

            if (_overlapCount.TryGetValue(targetTransform, out int count))
                _overlapCount[targetTransform] = count + 1;
            else
                _overlapCount[targetTransform] = 1;

            _targetColliders[targetTransform] = other;

            if (!_timers.ContainsKey(targetTransform))
                _timers[targetTransform] = 0f;
        }

        public void Exit(Collider other)
        {
            Transform targetTransform =
                ColliderTargetResolver.ResolveRoot(other);

            if (targetTransform == null ||
                !_overlapCount.TryGetValue(targetTransform, out int count))
            {
                return;
            }

            count--;

            if (count <= 0)
            {
                RemoveTarget(targetTransform);
                return;
            }

            _overlapCount[targetTransform] = count;

            if (_targetColliders.TryGetValue(
                    targetTransform,
                    out Collider storedCollider) &&
                storedCollider == other)
            {
                _targetColliders[targetTransform] =
                    FindFirstEnabledCollider(targetTransform);
            }
        }

        public void Tick(
            float deltaTime,
            IEffectApplicationService effects,
            EffectBundle bundle,
            float power,
            float applicationInterval)
        {
            if (effects == null || bundle == null || _timers.Count == 0)
                return;

            float interval = Mathf.Max(0.01f, applicationInterval);
            _targetsBuffer.Clear();

            foreach (KeyValuePair<Transform, float> pair in _timers)
                _targetsBuffer.Add(pair.Key);

            for (int i = 0; i < _targetsBuffer.Count; i++)
            {
                Transform targetTransform = _targetsBuffer[i];

                if (targetTransform == null)
                {
                    RemoveTarget(targetTransform);
                    continue;
                }

                Collider targetCollider =
                    GetValidTargetCollider(targetTransform);

                if (targetCollider == null)
                {
                    RemoveTarget(targetTransform);
                    continue;
                }

                float timer = _timers[targetTransform] - deltaTime;

                if (timer <= 0f)
                {
                    Apply(
                        effects,
                        bundle,
                        power,
                        targetTransform,
                        targetCollider);
                    timer = interval;
                }

                _timers[targetTransform] = timer;
            }
        }

        public void Clear()
        {
            _overlapCount.Clear();
            _timers.Clear();
            _targetColliders.Clear();
            _targetsBuffer.Clear();
        }

        private void Apply(
            IEffectApplicationService effects,
            EffectBundle bundle,
            float power,
            Transform targetTransform,
            Collider targetCollider)
        {
            EffectContext context = ContactEffectContextFactory.Create(
                _source,
                targetCollider,
                targetTransform,
                power);

            effects.Apply(bundle, context);
        }

        private void RemoveTarget(Transform targetTransform)
        {
            _overlapCount.Remove(targetTransform);
            _timers.Remove(targetTransform);
            _targetColliders.Remove(targetTransform);
        }

        private Collider GetValidTargetCollider(Transform targetTransform)
        {
            if (targetTransform == null)
                return null;

            if (_targetColliders.TryGetValue(
                    targetTransform,
                    out Collider collider) &&
                collider != null &&
                collider.enabled &&
                collider.gameObject.activeInHierarchy)
            {
                return collider;
            }

            collider = FindFirstEnabledCollider(targetTransform);

            if (collider != null)
                _targetColliders[targetTransform] = collider;

            return collider;
        }

        private static Collider FindFirstEnabledCollider(
            Transform targetTransform)
        {
            if (targetTransform == null)
                return null;

            Collider[] colliders =
                targetTransform.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];

                if (collider != null &&
                    collider.enabled &&
                    collider.gameObject.activeInHierarchy)
                {
                    return collider;
                }
            }

            return null;
        }

    }
}
