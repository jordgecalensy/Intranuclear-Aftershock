using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public abstract class EffectDefinition : ScriptableObject
    {
        [Header("HUD")]
        [SerializeField] private bool _showInHud;
        [SerializeField] private Sprite _hudIcon;
        [SerializeField] private Color _hudDurationColor = Color.white;

        public bool ShowInHud => _showInHud;
        public Sprite HudIcon => _hudIcon;
        public Color HudDurationColor => _hudDurationColor;

        public abstract bool CanApply(EffectContext context);

        public abstract Effect CreateEffect(EffectContext context);

        public virtual string GetStackKey(EffectContext context)
        {
            return GetType().FullName;
        }
    }
}
