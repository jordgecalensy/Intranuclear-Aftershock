using UnityEngine;

namespace Failsafe.Items
{
    [CreateAssetMenu(fileName = "StimpackData", menuName = "ScriptableObjects/Entities/Items/StimpackData")]
    public class StimpackData : ScriptableObject
    {
        /// <summary>
        /// На сколько лечит
        /// </summary>
        public int HealAmount;
        /// <summary>
        /// На сколько увеличивается максимальное здоровье
        /// </summary>
        public float MaxHealthBonus;
    }
}
