using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Builds a consistent effect context for collider contact and area hits.
    /// </summary>
    public static class ContactEffectContextFactory
    {
        public static EffectContext Create(
            GameObject source,
            Collider targetCollider,
            Transform targetRoot,
            float power)
        {
            Vector3 sourcePosition = source != null
                ? source.transform.position
                : Vector3.zero;

            Vector3 direction = targetRoot != null
                ? targetRoot.position - sourcePosition
                : Vector3.zero;

            if (direction.sqrMagnitude < 0.0001f && targetCollider != null)
                direction = targetCollider.bounds.center - sourcePosition;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = source != null
                    ? source.transform.forward
                    : Vector3.forward;
            }

            direction.Normalize();

            Vector3 point = targetCollider != null
                ? targetCollider.ClosestPoint(sourcePosition)
                : targetRoot != null
                    ? targetRoot.position
                    : sourcePosition;

            return new EffectContext(
                source,
                targetCollider,
                point,
                Vector3.up,
                direction,
                power);
        }
    }
}
