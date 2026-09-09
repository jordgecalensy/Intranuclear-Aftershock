using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Maps any collider belonging to an object to one stable target root.
    /// </summary>
    public static class ColliderTargetResolver
    {
        public static Transform ResolveRoot(Collider collider)
        {
            if (collider == null)
                return null;

            if (collider.attachedRigidbody != null)
                return collider.attachedRigidbody.transform;

            return collider.transform.root;
        }
    }
}
