using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "FrozenEffectDefinition",
        menuName = "Failsafe/Effects/Statuses/Frozen")]
    public class FrozenEffectDefinition : EffectDefinition, IStatusEffectDefinition
    {
        [Header("Frozen")]
        [SerializeField] private float _duration = 3f;

        [Header("Target")]
        [SerializeField] private bool _autoAddStatusState = true;

        [Tooltip("Если true, FrozenPhysicsResponder будет добавлен автоматически, если его нет.")]
        [SerializeField] private bool _autoAddFrozenPhysicsResponder = false;

        [Tooltip("Если FrozenPhysicsResponder не найден, использовать Stasisable как fallback.")]
        [SerializeField] private bool _useStasisableFallback = true;

        [Header("On Apply")]
        [SerializeField] private StatusEffectType[] _removeStatusesOnApply =
        {
            StatusEffectType.Wet,
            StatusEffectType.Cold
        };

        [Header("On End")]
        [SerializeField] private StatusEffectType[] _immunityStatusesOnEnd =
        {
            StatusEffectType.Wet,
            StatusEffectType.Cold
        };

        [SerializeField] private float _immunityDurationOnEnd = 2f;

        public StatusEffectType StatusType => StatusEffectType.Frozen;

        public override bool CanApply(EffectContext context)
        {
            if (!StatusEffectStateResolver.TryResolve(
                    context,
                    _autoAddStatusState,
                    out StatusEffectState state))
            {
                return false;
            }

            return state != null &&
                   state.CanReceive(StatusEffectType.Frozen) &&
                   StatusResistanceUtility.ApplyDurationMultiplier(
                       state,
                       StatusEffectType.Frozen,
                       _duration) > 0f;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!StatusEffectStateResolver.TryResolve(
                    context,
                    _autoAddStatusState,
                    out StatusEffectState state))
            {
                return null;
            }

            if (state == null)
                return null;

            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            FrozenPhysicsResponder physicsResponder = ResolveFrozenPhysicsResponder(target);

            Stasisable stasisFallback = null;

            if (physicsResponder == null && _useStasisableFallback)
                stasisFallback = ResolveStasisable(target, context);

            float duration = StatusResistanceUtility.ApplyDurationMultiplier(
                state,
                StatusEffectType.Frozen,
                _duration);

            return new FrozenEffect(
                state,
                physicsResponder,
                stasisFallback,
                duration,
                context.Source,
                _removeStatusesOnApply,
                _immunityStatusesOnEnd,
                _immunityDurationOnEnd);
        }

        public override string GetStackKey(EffectContext context)
        {
            if (StatusEffectStateResolver.TryResolve(
                    context,
                    false,
                    out StatusEffectState state) &&
                state != null)
            {
                return $"status.Frozen.{state.GetInstanceID()}";
            }

            if (context.HitCollider != null)
                return $"status.Frozen.collider.{context.HitCollider.GetInstanceID()}";

            if (context.TargetObject != null)
                return $"status.Frozen.target.{context.TargetObject.GetInstanceID()}";

            return "status.Frozen";
        }

        private FrozenPhysicsResponder ResolveFrozenPhysicsResponder(GameObject target)
        {
            if (target == null)
                return null;

            FrozenPhysicsResponder responder =
                target.GetComponent<FrozenPhysicsResponder>() ??
                target.GetComponentInParent<FrozenPhysicsResponder>() ??
                target.GetComponentInChildren<FrozenPhysicsResponder>(true);

            if (responder == null && _autoAddFrozenPhysicsResponder)
                responder = target.AddComponent<FrozenPhysicsResponder>();

            return responder;
        }

        private static Stasisable ResolveStasisable(GameObject target, EffectContext context)
        {
            if (target != null)
            {
                Stasisable stasisable =
                    target.GetComponent<Stasisable>() ??
                    target.GetComponentInParent<Stasisable>() ??
                    target.GetComponentInChildren<Stasisable>(true);

                if (stasisable != null)
                    return stasisable;
            }

            if (context.HitCollider != null)
            {
                Stasisable stasisable =
                    context.HitCollider.GetComponentInParent<Stasisable>();

                if (stasisable != null)
                    return stasisable;

                if (context.HitCollider.attachedRigidbody != null)
                {
                    Rigidbody rb = context.HitCollider.attachedRigidbody;

                    stasisable =
                        rb.GetComponent<Stasisable>() ??
                        rb.GetComponentInChildren<Stasisable>(true) ??
                        rb.GetComponentInParent<Stasisable>();

                    if (stasisable != null)
                        return stasisable;
                }
            }

            return null;
        }
    }
}
