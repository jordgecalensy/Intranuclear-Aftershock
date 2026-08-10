using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public static class StatusEffectStateResolver
    {
        public static bool TryResolve(
            EffectContext context,
            bool autoAdd,
            out StatusEffectState state)
        {
            state = null;

            Transform targetRoot = null;

            if (context.HitCollider != null)
            {
                state = context.HitCollider.GetComponentInParent<StatusEffectState>();

                if (state != null)
                    return true;

                if (context.HitCollider.attachedRigidbody != null)
                {
                    Rigidbody rb = context.HitCollider.attachedRigidbody;

                    state = rb.GetComponent<StatusEffectState>();

                    if (state != null)
                        return true;

                    state = rb.GetComponentInChildren<StatusEffectState>(true);

                    if (state != null)
                        return true;

                    state = rb.GetComponentInParent<StatusEffectState>();

                    if (state != null)
                        return true;

                    targetRoot = rb.transform;
                }

                if (targetRoot == null)
                    targetRoot = context.HitCollider.transform.root;
            }

            if (state == null && context.TargetObject != null)
            {
                state = context.TargetObject.GetComponentInParent<StatusEffectState>();

                if (state != null)
                    return true;

                targetRoot = context.TargetObject.transform.root;
            }

            if (state == null && context.TryGetRigidbody(out Rigidbody contextRb) && contextRb != null)
            {
                state = contextRb.GetComponent<StatusEffectState>();

                if (state != null)
                    return true;

                targetRoot = contextRb.transform;
            }

            if (state == null && autoAdd && targetRoot != null)
                state = targetRoot.gameObject.AddComponent<StatusEffectState>();

            return state != null;
        }

        public static GameObject ResolveTargetObject(EffectContext context)
        {
            if (context.TargetObject != null)
                return context.TargetObject;

            if (context.HitCollider != null)
            {
                if (context.HitCollider.attachedRigidbody != null)
                    return context.HitCollider.attachedRigidbody.gameObject;

                return context.HitCollider.transform.root.gameObject;
            }

            if (context.TryGetRigidbody(out Rigidbody rb) && rb != null)
                return rb.gameObject;

            return null;
        }
    }
}