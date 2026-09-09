using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    public static class EffectTargetResolver
    {
        public static bool TryResolve<T>(
            EffectContext context,
            out T result)
            where T : class
        {
            if (context.TryGet(out result) && result != null)
                return true;

            result = null;
            LifetimeScope scope = FindClosestScope(context);

            if (scope == null || scope.Container == null)
                return false;

            try
            {
                result = scope.Container.Resolve<T>();
                return result != null;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        public static Transform ResolveTargetTransform(EffectContext context)
        {
            if (context.TargetObject != null)
                return context.TargetObject.transform;

            if (context.HitCollider != null)
                return context.HitCollider.transform.root;

            return null;
        }

        private static LifetimeScope FindClosestScope(EffectContext context)
        {
            Transform target = ResolveTargetTransform(context);

            if (target == null)
                return null;

            return target.GetComponent<LifetimeScope>() ??
                   target.GetComponentInParent<LifetimeScope>() ??
                   target.GetComponentInChildren<LifetimeScope>(true);
        }
    }
}
