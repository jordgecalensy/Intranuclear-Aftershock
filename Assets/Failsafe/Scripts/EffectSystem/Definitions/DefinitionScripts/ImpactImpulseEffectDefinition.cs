using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "ImpactImpulseEffectDefinition",
        menuName = "Failsafe/Effects/Physics/Impact Impulse")]
    public class ImpactImpulseEffectDefinition : EffectDefinition
    {
        [Header("Impulse")]
        [Tooltip("Сила импульса.")]
        [SerializeField] private float _strength = 8f;

        [Tooltip("Добавка вверх. 0 = строго от точки удара, 0.2 = слегка подбрасывает.")]
        [SerializeField] private float _upwardBias = 0.15f;

        [Tooltip("Если true, сила умножается на EffectContext.Power.")]
        [SerializeField] private bool _scaleByContextPower = true;

        [Tooltip("Максимальная итоговая сила импульса. 0 = без ограничения.")]
        [SerializeField] private float _maxImpulseMagnitude = 30f;

        [Header("Rigidbody")]
        [SerializeField] private ForceMode _forceMode = ForceMode.Impulse;

        [Tooltip("Если true, Rigidbody получит импульс в точке удара, что может добавить вращение.")]
        [SerializeField] private bool _applyAtImpactPoint = true;

        [Tooltip("Если false, kinematic Rigidbody игнорируются.")]
        [SerializeField] private bool _affectKinematicRigidbodies = false;

        [Header("Receiver")]
        [Tooltip("Сначала пробовать IImpactImpulseReceiver. Нужно для игрока на CharacterController.")]
        [SerializeField] private bool _preferImpactReceiver = true;

        [Header("Debug")]
        [SerializeField] private bool _log;

        public override bool CanApply(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target == null && context.HitCollider == null)
                return false;

            if (_preferImpactReceiver && ResolveImpactReceiver(target, context) != null)
                return true;

            Rigidbody rb = ResolveRigidbody(target, context);

            if (rb == null)
                return false;

            if (rb.isKinematic && !_affectKinematicRigidbodies)
                return false;

            return true;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            IImpactImpulseReceiver receiver = _preferImpactReceiver
                ? ResolveImpactReceiver(target, context)
                : null;

            Rigidbody rb = receiver == null
                ? ResolveRigidbody(target, context)
                : null;

            if (receiver == null && rb == null)
                return null;

            if (rb != null && rb.isKinematic && !_affectKinematicRigidbodies)
                return null;

            Vector3 targetCenter = ResolveTargetCenter(target, context, rb);
            Vector3 impactPoint = ResolveImpactPoint(context, targetCenter);
            Vector3 direction = ResolveImpulseDirection(context, targetCenter, impactPoint);

            float strength = Mathf.Max(0f, _strength);

            if (_scaleByContextPower)
                strength *= Mathf.Max(0f, context.Power);

            Vector3 impulse = direction * strength;

            if (_maxImpulseMagnitude > 0f &&
                impulse.magnitude > _maxImpulseMagnitude)
            {
                impulse = impulse.normalized * _maxImpulseMagnitude;
            }

            return new ImpactImpulseEffect(
                receiver,
                rb,
                impulse,
                impactPoint,
                context.Source,
                _forceMode,
                _applyAtImpactPoint,
                _log);
        }

        public override string GetStackKey(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target != null)
                return $"physics.impact-impulse.{GetInstanceID()}.target.{target.GetInstanceID()}";

            if (context.HitCollider != null)
                return $"physics.impact-impulse.{GetInstanceID()}.collider.{context.HitCollider.GetInstanceID()}";

            return $"physics.impact-impulse.{GetInstanceID()}";
        }

        private Vector3 ResolveImpulseDirection(
            EffectContext context,
            Vector3 targetCenter,
            Vector3 impactPoint)
        {
            Vector3 direction = targetCenter - impactPoint;

            if (direction.sqrMagnitude <= 0.0001f && context.Source != null)
                direction = targetCenter - context.Source.transform.position;

            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.up;

            direction.Normalize();

            if (Mathf.Abs(_upwardBias) > 0.0001f)
            {
                direction += Vector3.up * _upwardBias;

                if (direction.sqrMagnitude > 0.0001f)
                    direction.Normalize();
            }

            return direction;
        }

        private static Vector3 ResolveImpactPoint(
            EffectContext context,
            Vector3 targetCenter)
        {
            if (context.HitCollider != null && context.Source != null)
                return context.HitCollider.ClosestPoint(context.Source.transform.position);

            if (context.HitCollider != null)
                return context.HitCollider.ClosestPoint(targetCenter);

            return targetCenter;
        }

        private static Vector3 ResolveTargetCenter(
            GameObject target,
            EffectContext context,
            Rigidbody rb)
        {
            if (rb != null)
                return rb.worldCenterOfMass;

            if (context.HitCollider != null)
                return context.HitCollider.bounds.center;

            if (target != null)
                return target.transform.position;

            return Vector3.zero;
        }

        private static GameObject ResolveTargetObject(EffectContext context)
        {
            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target != null)
                return target;

            if (context.TargetObject != null)
                return context.TargetObject;

            if (context.HitCollider != null)
            {
                if (context.HitCollider.attachedRigidbody != null)
                    return context.HitCollider.attachedRigidbody.gameObject;

                return context.HitCollider.transform.root.gameObject;
            }

            return null;
        }

        private static Rigidbody ResolveRigidbody(
            GameObject target,
            EffectContext context)
        {
            if (context.HitCollider != null &&
                context.HitCollider.attachedRigidbody != null)
            {
                return context.HitCollider.attachedRigidbody;
            }

            if (target == null)
                return null;

            return target.GetComponent<Rigidbody>() ??
                   target.GetComponentInParent<Rigidbody>() ??
                   target.GetComponentInChildren<Rigidbody>(true);
        }

        private static IImpactImpulseReceiver ResolveImpactReceiver(
            GameObject target,
            EffectContext context)
        {
            if (target != null)
            {
                IImpactImpulseReceiver receiver = FindReceiver(target);

                if (receiver != null)
                    return receiver;
            }

            if (context.HitCollider != null)
            {
                IImpactImpulseReceiver receiver =
                    FindReceiver(context.HitCollider.gameObject);

                if (receiver != null)
                    return receiver;

                if (context.HitCollider.attachedRigidbody != null)
                {
                    receiver = FindReceiver(
                        context.HitCollider.attachedRigidbody.gameObject);

                    if (receiver != null)
                        return receiver;
                }
            }

            return null;
        }

        private static IImpactImpulseReceiver FindReceiver(GameObject root)
        {
            if (root == null)
                return null;

            MonoBehaviour[] parentBehaviours =
                root.GetComponentsInParent<MonoBehaviour>(true);

            for (int i = 0; i < parentBehaviours.Length; i++)
            {
                if (parentBehaviours[i] is IImpactImpulseReceiver receiver)
                    return receiver;
            }

            MonoBehaviour[] childBehaviours =
                root.GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = 0; i < childBehaviours.Length; i++)
            {
                if (childBehaviours[i] is IImpactImpulseReceiver receiver)
                    return receiver;
            }

            return null;
        }
    }
}