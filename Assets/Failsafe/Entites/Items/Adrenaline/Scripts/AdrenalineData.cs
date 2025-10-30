using UnityEngine;

namespace Failsafe.Items
{
    [CreateAssetMenu(fileName = "AdrenalineData", menuName = "ScriptableObjects/Entities/Items/AdrenalineData")]
    public class AdrenalineData : ScriptableObject
    {
        public float SpeedMultiplier;
        public float Duration;

        /// <summary>
        /// Время с момента использования предмета (например, нажатия кнопки) до срабатывания его эффекта (Нужно для синхронизации анимации, геймплея и звука)
        /// </summary>
        public float StartUseDelay;
        /// <summary>
        /// Кулдаун изпользования предмета
        /// </summary>
        public float UseDelay;
    }
}
