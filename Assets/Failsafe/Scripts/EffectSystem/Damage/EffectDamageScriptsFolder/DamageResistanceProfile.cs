using Failsafe.Scripts.Damage;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "DamageResistanceProfile",
        menuName = "Failsafe/Effects/Damage/Damage Resistance Profile")]
    public class DamageResistanceProfile : ScriptableObject
    {
        [SerializeField] private DamageResistanceEntry[] _entries;

        public float GetBaseMultiplier(DamageType damageType)
        {
            if (_entries == null)
                return 1f;

            for (int i = 0; i < _entries.Length; i++)
            {
                DamageResistanceEntry entry = _entries[i];

                if (entry == null)
                    continue;

                if (entry.DamageType == damageType)
                    return entry.Multiplier;
            }

            return 1f;
        }
    }
}