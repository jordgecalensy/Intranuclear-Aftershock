using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "StasisEffectDefinition",
        menuName = "Failsafe/Effects/Stasis")]
    public class StasisEffectDefinition : EffectDefinition
    {
        [Header("Stasis")]
        [SerializeField] private float _duration = 3f;

        [Tooltip("Если true, длительность умножается на EffectContext.Power.")]
        [SerializeField] private bool _scaleDurationByContextPower = false;

        [Tooltip("Если true, после выхода из стазиса Rigidbody получит обратно сохранённую скорость.")]
        [SerializeField] private bool _restoreVelocityOnExit = false;

        [Tooltip("Если true, то при попадании в Rigidbody без Stasisable компонент Stasisable будет добавлен автоматически.")]
        [SerializeField] private bool _autoAddStasisableToRigidbody = true;

        public override bool CanApply(EffectContext context)
        {
            if (TryResolveStasisable(context, out _))
                return true;

            if (_autoAddStasisableToRigidbody && context.TryGetRigidbody(out Rigidbody rb) && rb != null)
                return true;

            return false;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            Stasisable stasisable = null;

            if (!TryResolveStasisable(context, out stasisable))
            {
                if (_autoAddStasisableToRigidbody && context.TryGetRigidbody(out Rigidbody rb) && rb != null)
                    stasisable = rb.GetComponent<Stasisable>() ?? rb.gameObject.AddComponent<Stasisable>();
            }

            if (stasisable == null)
                return null;

            float finalDuration = Mathf.Max(0f, _duration);

            if (_scaleDurationByContextPower)
                finalDuration *= Mathf.Max(0f, context.Power);

            return new StasisEffect(
                stasisable,
                finalDuration,
                _restoreVelocityOnExit,
                context.Source);
        }

        public override string GetStackKey(EffectContext context)
        {
            if (TryResolveStasisable(context, out Stasisable stasisable) && stasisable != null)
                return $"status.stasis.{stasisable.GetInstanceID()}";

            if (context.TryGetRigidbody(out Rigidbody rb) && rb != null)
                return $"status.stasis.rigidbody.{rb.GetInstanceID()}";

            return "status.stasis";
        }

        private static bool TryResolveStasisable(EffectContext context, out Stasisable stasisable)
        {
            stasisable = null;

            if (context.HitCollider == null)
                return false;

            stasisable = context.HitCollider.GetComponentInParent<Stasisable>();

            if (stasisable != null)
                return true;

            if (context.HitCollider.attachedRigidbody != null)
            {
                stasisable = context.HitCollider.attachedRigidbody.GetComponent<Stasisable>();

                if (stasisable != null)
                    return true;

                stasisable = context.HitCollider.attachedRigidbody.GetComponentInChildren<Stasisable>(true);

                if (stasisable != null)
                    return true;

                stasisable = context.HitCollider.attachedRigidbody.GetComponentInParent<Stasisable>();

                if (stasisable != null)
                    return true;
            }

            stasisable = context.HitCollider.transform.root.GetComponentInChildren<Stasisable>(true);

            return stasisable != null;
        }
    }
}