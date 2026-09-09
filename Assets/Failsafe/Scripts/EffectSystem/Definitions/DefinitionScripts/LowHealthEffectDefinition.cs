using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "LowHealthEffectDefinition",
        menuName = "Failsafe/Effects/Feedback/Low Health")]
    public sealed class LowHealthEffectDefinition : EffectDefinition
    {
        public override bool CanApply(EffectContext context)
        {
            return context.TargetObject != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            return new LowHealthEffect();
        }

        public override string GetStackKey(EffectContext context)
        {
            return "feedback.low-health";
        }
    }
}
