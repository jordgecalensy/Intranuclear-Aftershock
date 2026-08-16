using Failsafe.Scripts.Damage;
using Failsafe.Scripts.EffectSystem.Effects;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Definitions
{
    [CreateAssetMenu(
        fileName = "DamageResistanceModifierEffectDefinition",
        menuName = "Failsafe/Effects/Damage/Damage Resistance Modifier")]
    public class DamageResistanceModifierEffectDefinition : EffectDefinition
    {
        [Header("Modifier")]
        [SerializeField] private DamageType _damageType = DamageType.Fire;

        [Tooltip("0.5 = цель получает половину этого типа урона. 1.5 = получает на 50% больше.")]
        [SerializeField, Min(0f)] private float _multiplier = 0.5f;

        [SerializeField] private bool _permanent = false;

        [SerializeField, Min(0.01f)] private float _duration = 5f;

        [Tooltip("Пусто = будет использован id этого SO. Заполняй, если нужно, чтобы несколько SO считались одним баффом.")]
        [SerializeField] private string _modifierIdOverride;

        [Header("Target")]
        [Tooltip("Если у цели нет DamageResistanceComponent, он будет добавлен автоматически.")]
        [SerializeField] private bool _autoAddResistanceComponent = true;

        [Header("Stacking")]
        [SerializeField] private bool _unique = true;

        [Header("Debug")]
        [SerializeField] private bool _log;

        public override bool CanApply(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target == null)
                return false;

            if (_autoAddResistanceComponent)
                return true;

            return DamageResistanceUtility.ResolveResistanceComponent(target) != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);

            if (target == null)
                return null;

            DamageResistanceComponent resistanceComponent =
                DamageResistanceUtility.ResolveResistanceComponent(target);

            if (resistanceComponent == null && _autoAddResistanceComponent)
                resistanceComponent = target.AddComponent<DamageResistanceComponent>();

            if (resistanceComponent == null)
                return null;

            string sourceId = ResolveModifierId();

            return new DamageResistanceModifierEffect(
                resistanceComponent,
                sourceId,
                _damageType,
                _multiplier,
                ResolveDuration(),
                _unique,
                _log);
        }

        public override string GetStackKey(EffectContext context)
        {
            GameObject target = ResolveTargetObject(context);
            int targetId = target != null ? target.GetInstanceID() : 0;

            return $"damage-resistance-modifier.{ResolveModifierId()}.target.{targetId}";
        }

        private string ResolveModifierId()
        {
            if (!string.IsNullOrWhiteSpace(_modifierIdOverride))
                return _modifierIdOverride;

            return $"{name}.{GetInstanceID()}";
        }

        private float ResolveDuration()
        {
            return _permanent
                ? float.PositiveInfinity
                : _duration;
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
    }
}
