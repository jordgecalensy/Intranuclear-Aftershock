using UnityEngine;

namespace Failsafe.Items
{
    [CreateAssetMenu(fileName = "GorillaData", menuName = "ScriptableObjects/Entities/Items/GorillaData")]
    public class GorillaData : ScriptableObject
    {
        public float ThrowPowerMultiplier;
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
