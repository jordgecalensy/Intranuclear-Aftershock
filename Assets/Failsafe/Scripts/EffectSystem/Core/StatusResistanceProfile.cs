using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "StatusResistanceProfile",
        menuName = "Failsafe/Effects/Statuses/Status Resistance Profile")]
    public class StatusResistanceProfile : ScriptableObject
    {
        [Header("Backward Compatibility")]
        [Tooltip("Старый простой список иммунитетов. Можно оставить пустым и использовать Entries.")]
        [SerializeField] private StatusEffectType[] _immuneStatuses;

        [Header("Entries")]
        [SerializeField] private StatusResistanceEntry[] _entries;

        public bool IsImmune(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return false;

            if (_immuneStatuses != null)
            {
                for (int i = 0; i < _immuneStatuses.Length; i++)
                {
                    if (_immuneStatuses[i] == statusType)
                        return true;
                }
            }

            StatusResistanceEntry entry = FindEntry(statusType);

            return entry != null && entry.Immune;
        }

        public float GetDurationMultiplier(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return 1f;

            StatusResistanceEntry entry = FindEntry(statusType);

            if (entry == null)
                return 1f;

            return entry.DurationMultiplier;
        }

        public float GetBuildUpMultiplier(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return 1f;

            StatusResistanceEntry entry = FindEntry(statusType);

            if (entry == null)
                return 1f;

            return entry.BuildUpMultiplier;
        }

        private StatusResistanceEntry FindEntry(StatusEffectType statusType)
        {
            if (_entries == null)
                return null;

            for (int i = 0; i < _entries.Length; i++)
            {
                StatusResistanceEntry entry = _entries[i];

                if (entry == null)
                    continue;

                if (entry.StatusType == statusType)
                    return entry;
            }

            return null;
        }
    }
}