using UnityEngine;

namespace Failsafe.Items
{
    [CreateAssetMenu(fileName = "StimpackData", menuName = "ScriptableObjects/Entities/Items/StimpackData")]
    public class StimpackData : ScriptableObject
    {
        /// <summary>
        /// Длительность визуального эффекта
        /// </summary>
        public float Duration; 
        /// <summary>
        /// На сколько лечит
        /// </summary>
        public int HealAmount;
        /// <summary>
        /// На сколько увеличивается максимальное здоровье
        /// </summary>
        public float MaxHealthBonus;

        /// <summary>
        /// Время с момента использования предмета (например, нажатия кнопки) до срабатывания его эффекта (Нужно для синхронизации анимации, гейплея и звука)
        /// </summary>
        public float StartUseDelay;
        /// <summary>
        /// Кулдаун изпользования предмета
        /// </summary>
        public float UseDelay;
    }
}
