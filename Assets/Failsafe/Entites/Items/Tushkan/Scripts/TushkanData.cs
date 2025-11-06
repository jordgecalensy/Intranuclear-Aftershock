using UnityEngine;

namespace Failsafe.Items
{
    [CreateAssetMenu(fileName = "TushkanData", menuName = "ScriptableObjects/Entities/Items/TushkanData")]
    public class TushkanData : ScriptableObject
    {
        public float JumpMultiplier;
        public float Duration;

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
