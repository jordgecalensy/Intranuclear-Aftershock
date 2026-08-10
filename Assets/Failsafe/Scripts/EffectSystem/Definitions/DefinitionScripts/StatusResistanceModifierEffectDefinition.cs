using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "StatusResistanceModifierEffectDefinition",
        menuName = "Failsafe/Effects/Statuses/Status Resistance Modifier")]
    public class StatusResistanceModifierEffectDefinition : EffectDefinition
    {
        [Header("Modifier")]
        [SerializeField] private StatusEffectType _statusType = StatusEffectType.Stun;

        [Tooltip("Множитель длительности статуса. 0.5 = в два раза короче, 0 = не висит.")]
        [SerializeField, Min(0f)] private float _durationMultiplier = 0.5f;

        [Tooltip("Множитель накопления стадии. Для Cold/Poison.")]
        [SerializeField, Min(0f)] private float _buildUpMultiplier = 1f;

        [SerializeField, Min(0.01f)] private float _duration = 5f;

        [Tooltip("Пусто = id этого SO. Заполняй, если несколько SO должны считаться одним баффом.")]
        [SerializeField] private string _modifierIdOverride;

        [Header("Target")]
        [SerializeField] private bool _autoAddStatusEffectState = true;

        [Header("Stacking")]
        [SerializeField] private bool _unique = true;

        [Header("Debug")]
        [SerializeField] private bool _log;

        public override bool CanApply(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target == null)
                return false;

            if (_autoAddStatusEffectState)
                return true;

            return ResolveStatusState(target) != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target == null)
                return null;

            StatusEffectState state = ResolveStatusState(target);

            if (state == null && _autoAddStatusEffectState)
                state = target.AddComponent<StatusEffectState>();

            if (state == null)
                return null;

            string sourceId = ResolveModifierId();

            return new StatusResistanceModifierEffect(
                state,
                sourceId,
                _statusType,
                _durationMultiplier,
                _buildUpMultiplier,
                _duration,
                _unique,
                _log);
        }

        public override string GetStackKey(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);
            int targetId = target != null ? target.GetInstanceID() : 0;

            return $"status-resistance-modifier.{ResolveModifierId()}.target.{targetId}";
        }

        private string ResolveModifierId()
        {
            if (!string.IsNullOrWhiteSpace(_modifierIdOverride))
                return _modifierIdOverride;

            return $"{name}.{GetInstanceID()}";
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

        private static StatusEffectState ResolveStatusState(GameObject target)
        {
            if (target == null)
                return null;

            return target.GetComponent<StatusEffectState>() ??
                   target.GetComponentInParent<StatusEffectState>() ??
                   target.GetComponentInChildren<StatusEffectState>(true);
        }
    }
}