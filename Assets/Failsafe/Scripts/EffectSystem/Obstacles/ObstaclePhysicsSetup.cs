using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Performs the one-time physics setup required by an obstacle.
    /// </summary>
    public static class ObstaclePhysicsSetup
    {
        public static Collider PrepareContactTrigger(
            GameObject owner,
            Collider configuredTrigger)
        {
            Collider trigger = configuredTrigger;

            if (trigger == null && owner != null)
            {
                Collider[] colliders = owner.GetComponents<Collider>();

                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider collider = colliders[i];

                    if (collider != null && collider.isTrigger)
                    {
                        trigger = collider;
                        break;
                    }
                }
            }

            if (trigger != null && !trigger.isTrigger)
                trigger.isTrigger = true;

            return trigger;
        }

        public static Rigidbody PrepareRigidbody(
            GameObject owner,
            bool addWhenMissing)
        {
            if (owner == null)
                return null;

            Rigidbody rigidbody = owner.GetComponent<Rigidbody>();

            if (rigidbody == null && addWhenMissing)
                rigidbody = owner.AddComponent<Rigidbody>();

            if (rigidbody == null)
                return null;

            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            return rigidbody;
        }
    }
}
