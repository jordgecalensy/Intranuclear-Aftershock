using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public abstract class EffectDefinition : ScriptableObject
    {
        public abstract bool CanApply(EffectContext context);

        public abstract Effect CreateEffect(EffectContext context);

        public virtual string GetStackKey(EffectContext context)
        {
            return GetType().FullName;
        }
    }
}