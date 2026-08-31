using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Schedules a limited number of propagation attempts and finds a nearby
    /// surface point. The callback decides what is created there.
    /// </summary>
    public sealed class SpatialPropagation
    {
        private const float MinimumInterval = 0.01f;

        private readonly Func<Vector3, bool> _trySpawn;
        private float _nextAttemptAt;
        private int _spawnedCount;

        public int SpawnedCount => _spawnedCount;

        public SpatialPropagation(Func<Vector3, bool> trySpawn)
        {
            _trySpawn = trySpawn ??
                throw new ArgumentNullException(nameof(trySpawn));
        }

        public void Initialize(float currentTime, float interval)
        {
            _nextAttemptAt = currentTime + NormalizeInterval(interval);
        }

        public bool Tick(
            float currentTime,
            bool enabled,
            float interval,
            float chance,
            int maxChildren,
            Vector3 origin,
            Vector3 fallbackDirection,
            float sourceRadius,
            float distance,
            LayerMask surfaceMask,
            float surfaceSearchHeight,
            float surfaceSearchDistance)
        {
            if (!enabled ||
                maxChildren <= 0 ||
                _spawnedCount >= maxChildren ||
                currentTime < _nextAttemptAt)
            {
                return false;
            }

            _nextAttemptAt = currentTime + NormalizeInterval(interval);

            if (UnityEngine.Random.value > Mathf.Clamp01(chance))
                return false;

            Vector3 spawnPosition = FindSpawnPosition(
                origin,
                fallbackDirection,
                sourceRadius,
                distance,
                surfaceMask,
                surfaceSearchHeight,
                surfaceSearchDistance);

            if (!_trySpawn(spawnPosition))
                return false;

            _spawnedCount++;
            return true;
        }

        private static Vector3 FindSpawnPosition(
            Vector3 origin,
            Vector3 fallbackDirection,
            float sourceRadius,
            float distance,
            LayerMask surfaceMask,
            float surfaceSearchHeight,
            float surfaceSearchDistance)
        {
            Vector3 direction = UnityEngine.Random.insideUnitSphere;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = fallbackDirection;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.forward;

            direction.Normalize();

            Vector3 spawnPosition = origin +
                direction * (Mathf.Max(0f, sourceRadius) + Mathf.Max(0f, distance));
            float searchHeight = Mathf.Max(0f, surfaceSearchHeight);
            float searchDistance = Mathf.Max(0f, surfaceSearchDistance);

            if (searchDistance > 0f &&
                Physics.Raycast(
                    spawnPosition + Vector3.up * searchHeight,
                    Vector3.down,
                    out RaycastHit hit,
                    searchDistance,
                    surfaceMask,
                    QueryTriggerInteraction.Ignore))
            {
                spawnPosition = hit.point;
            }

            return spawnPosition;
        }

        private static float NormalizeInterval(float interval)
        {
            return Mathf.Max(MinimumInterval, interval);
        }
    }
}
