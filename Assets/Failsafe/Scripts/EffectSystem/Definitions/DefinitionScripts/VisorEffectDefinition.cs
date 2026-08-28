using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "VisorEffectDefinition",
        menuName = "Failsafe/Effects/Feedback/Visor")]
    public sealed class VisorEffectDefinition : EffectDefinition
    {
        public override bool CanApply(EffectContext context)
        {
            return EffectTargetResolver.ResolveTargetTransform(context) != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            Transform target = EffectTargetResolver.ResolveTargetTransform(context);
            return target != null
                ? new VisorEffect(target)
                : null;
        }

        public override string GetStackKey(EffectContext context)
        {
            return "feedback.visor";
        }
    }
}
