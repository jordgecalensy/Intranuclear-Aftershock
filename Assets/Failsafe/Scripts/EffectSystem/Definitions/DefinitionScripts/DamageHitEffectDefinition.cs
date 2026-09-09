using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "DamageHitEffectDefinition",
        menuName = "Failsafe/Effects/Feedback/Damage Hit")]
    public sealed class DamageHitEffectDefinition : EffectDefinition
    {
        [SerializeField, Min(0f)] private float _damageAmount = 0.25f;

        public override bool CanApply(EffectContext context)
        {
            return context.TargetObject != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            return new DamageHitEffect(_damageAmount);
        }

        public override string GetStackKey(EffectContext context)
        {
            return "feedback.damage-hit";
        }
    }
}
