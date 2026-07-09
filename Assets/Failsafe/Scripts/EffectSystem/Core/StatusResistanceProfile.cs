using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "StatusResistanceProfile",
        menuName = "Failsafe/Effects/Statuses/Status Resistance Profile")]
    public class StatusResistanceProfile : ScriptableObject
    {
        [Header("Permanent Immunities")]
        [Tooltip("Статусы, которые цель вообще не может получить.")]
        [SerializeField] private StatusEffectType[] _immuneStatuses;

        public bool IsImmune(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return false;

            if (_immuneStatuses == null)
                return false;

            for (int i = 0; i < _immuneStatuses.Length; i++)
            {
                if (_immuneStatuses[i] == statusType)
                    return true;
            }

            return false;
        }
    }
}