using Failsafe.PlayerMovements;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "StunEffectDefinition",
        menuName = "Failsafe/Effects/Statuses/Stun")]
    public class StunEffectDefinition : EffectDefinition, IStatusEffectDefinition
    {
        [Header("Stun")]
        [SerializeField] private float _duration = 2f;

        [Header("Target")]
        [SerializeField] private bool _autoAddStatusState = true;

        [Header("Enemy")]
        [SerializeField] private bool _disableEnemyState = true;

        [Header("Player")]
        [SerializeField] private bool _blockPlayerControls = true;

        [SerializeField] private PlayerControlBlock _playerBlocks =
            PlayerControlBlock.Movement |
            PlayerControlBlock.Look |
            PlayerControlBlock.Jump |
            PlayerControlBlock.Crouch |
            PlayerControlBlock.Sprint |
            PlayerControlBlock.Interaction |
            PlayerControlBlock.Shooting |
            PlayerControlBlock.ItemUse;

        [Header("On Apply")]
        [SerializeField] private StatusEffectType[] _removeStatusesOnApply;

        [Header("On End")]
        [SerializeField] private StatusEffectType[] _immunityStatusesOnEnd;

        [SerializeField] private float _immunityDurationOnEnd = 0f;

        public StatusEffectType StatusType => StatusEffectType.Stun;

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
                   state.CanReceive(StatusEffectType.Stun) &&
                   StatusResistanceUtility.ApplyDurationMultiplier(
                       state,
                       StatusEffectType.Stun,
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

            Enemy enemy = ResolveEnemy(target, context);
            PlayerControlBlocker playerControlBlocker = ResolvePlayerControlBlocker(target, context);
            float duration = StatusResistanceUtility.ApplyDurationMultiplier(
                state,
                StatusEffectType.Stun,
                _duration);

            return new StunEffect(
                state,
                enemy,
                playerControlBlocker,
                duration,
                context.Source,
                _disableEnemyState,
                _blockPlayerControls,
                _playerBlocks,
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
                return $"status.Stun.{state.GetInstanceID()}";
            }

            if (context.HitCollider != null)
                return $"status.Stun.collider.{context.HitCollider.GetInstanceID()}";

            if (context.TargetObject != null)
                return $"status.Stun.target.{context.TargetObject.GetInstanceID()}";

            return "status.Stun";
        }

        private static Enemy ResolveEnemy(GameObject target, EffectContext context)
        {
            if (target != null)
            {
                Enemy enemy =
                    target.GetComponent<Enemy>() ??
                    target.GetComponentInParent<Enemy>() ??
                    target.GetComponentInChildren<Enemy>(true);

                if (enemy != null)
                    return enemy;
            }

            if (context.HitCollider != null)
            {
                Enemy enemy =
                    context.HitCollider.GetComponentInParent<Enemy>();

                if (enemy != null)
                    return enemy;

                if (context.HitCollider.attachedRigidbody != null)
                {
                    Rigidbody rb = context.HitCollider.attachedRigidbody;

                    enemy =
                        rb.GetComponent<Enemy>() ??
                        rb.GetComponentInParent<Enemy>() ??
                        rb.GetComponentInChildren<Enemy>(true);

                    if (enemy != null)
                        return enemy;
                }
            }

            return null;
        }

        private static PlayerControlBlocker ResolvePlayerControlBlocker(GameObject target, EffectContext context)
        {
            if (target != null)
            {
                PlayerControlBlocker blocker =
                    target.GetComponent<PlayerControlBlocker>() ??
                    target.GetComponentInParent<PlayerControlBlocker>() ??
                    target.GetComponentInChildren<PlayerControlBlocker>(true);

                if (blocker != null)
                    return blocker;
            }

            if (context.HitCollider != null)
            {
                PlayerControlBlocker blocker =
                    context.HitCollider.GetComponentInParent<PlayerControlBlocker>();

                if (blocker != null)
                    return blocker;

                if (context.HitCollider.attachedRigidbody != null)
                {
                    Rigidbody rb = context.HitCollider.attachedRigidbody;

                    blocker =
                        rb.GetComponent<PlayerControlBlocker>() ??
                        rb.GetComponentInParent<PlayerControlBlocker>() ??
                        rb.GetComponentInChildren<PlayerControlBlocker>(true);

                    if (blocker != null)
                        return blocker;
                }
            }

            return null;
        }
    }
}
