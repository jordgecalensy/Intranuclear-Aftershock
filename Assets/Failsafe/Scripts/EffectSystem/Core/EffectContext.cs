using System.Linq;
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

        public EffectContext(
            GameObject source,
            Collider hitCollider,
            Vector3 point,
            Vector3 normal,
            Vector3 direction,
            float power = 1f)
        {
            Source = source;
            HitCollider = hitCollider;
            Point = point;
            Normal = normal;
            Direction = direction;
            Power = power;
        }

        public GameObject TargetObject
        {
            get
            {
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

            if (HitCollider == null)
                return false;

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
            return component != null;
        }

        public bool TryGetRigidbody(out Rigidbody rb)
        {
            rb = null;

            if (HitCollider == null)
                return false;

            rb = HitCollider.attachedRigidbody;

            if (rb != null)
                return true;

            rb = HitCollider.GetComponentInParent<Rigidbody>();
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