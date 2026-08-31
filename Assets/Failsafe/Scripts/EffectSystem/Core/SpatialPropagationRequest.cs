using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public readonly struct SpatialPropagationRequest
    {
        public float CurrentTime { get; }
        public bool Enabled { get; }
        public float Interval { get; }
        public float Chance { get; }
        public int MaxChildren { get; }
        public Vector3 Origin { get; }
        public Vector3 FallbackDirection { get; }
        public float SourceRadius { get; }
        public float Distance { get; }
        public LayerMask SurfaceMask { get; }
        public float SurfaceSearchHeight { get; }
        public float SurfaceSearchDistance { get; }

        public SpatialPropagationRequest(
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
            CurrentTime = currentTime;
            Enabled = enabled;
            Interval = interval;
            Chance = chance;
            MaxChildren = maxChildren;
            Origin = origin;
            FallbackDirection = fallbackDirection;
            SourceRadius = sourceRadius;
            Distance = distance;
            SurfaceMask = surfaceMask;
            SurfaceSearchHeight = surfaceSearchHeight;
            SurfaceSearchDistance = surfaceSearchDistance;
        }
    }
}
