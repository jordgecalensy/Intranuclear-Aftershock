using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public readonly struct EffectContext
    {
        public readonly GameObject Source;
        public readonly Collider HitCollider;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Direction;
        public readonly float Power;
        public readonly GameObject TargetOverride;
        public readonly float DurationOverride;

        public EffectContext(
            GameObject source,
            Collider hitCollider,
            Vector3 point,
            Vector3 normal,
            Vector3 direction,
            float power = 1f,
            GameObject targetOverride = null,
            float durationOverride = 0f)
        {
            Source = source;
            HitCollider = hitCollider;
            Point = point;
            Normal = normal;
            Direction = direction;
            Power = power;
            TargetOverride = targetOverride;
            DurationOverride = Mathf.Max(0f, durationOverride);
        }

        public bool HasDurationOverride => DurationOverride > 0f;

        public float ResolveDuration(float fallbackDuration)
        {
            return HasDurationOverride
                ? DurationOverride
                : fallbackDuration;
        }

        public GameObject TargetObject
        {
            get
            {
                if (TargetOverride != null)
                    return TargetOverride;

                if (HitCollider == null)
                    return null;

                if (HitCollider.attachedRigidbody != null)
                    return HitCollider.attachedRigidbody.gameObject;

                return HitCollider.gameObject;
            }
        }

        public bool TryGet<T>(out T component) where T : class
        {
            component = null;

            if (HitCollider != null)
            {
                component = FindInParents<T>(HitCollider.transform);
                if (component != null)
                    return true;

                if (HitCollider.attachedRigidbody != null)
                {
                    component = FindInParents<T>(HitCollider.attachedRigidbody.transform);
                    if (component != null)
                        return true;
                }

                component = FindInChildren<T>(HitCollider.transform.root);
                if (component != null)
                    return true;
            }

            if (TargetOverride == null)
                return false;

            component = FindInParents<T>(TargetOverride.transform);
            if (component != null)
                return true;

            component = FindInChildren<T>(TargetOverride.transform);
            return component != null;
        }

        public bool TryGetRigidbody(out Rigidbody rb)
        {
            rb = null;

            if (HitCollider != null)
            {
                rb = HitCollider.attachedRigidbody;

                if (rb != null)
                    return true;

                rb = HitCollider.GetComponentInParent<Rigidbody>();
                if (rb != null)
                    return true;
            }

            if (TargetOverride == null)
                return false;

            rb = TargetOverride.GetComponentInParent<Rigidbody>();

            if (rb == null)
                rb = TargetOverride.GetComponentInChildren<Rigidbody>(true);

            return rb != null;
        }

        private static T FindInParents<T>(Transform start) where T : class
        {
            if (start == null)
                return null;

            var behaviours = start.GetComponentsInParent<MonoBehaviour>(true);

            foreach (var behaviour in behaviours)
            {
                if (behaviour is T typed)
                    return typed;
            }

            return null;
        }

        private static T FindInChildren<T>(Transform start) where T : class
        {
            if (start == null)
                return null;

            var behaviours = start.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (var behaviour in behaviours)
            {
                if (behaviour is T typed)
                    return typed;
            }

            return null;
        }
    }
}
