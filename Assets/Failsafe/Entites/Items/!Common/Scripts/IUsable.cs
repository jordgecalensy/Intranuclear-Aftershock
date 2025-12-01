using UnityEngine;

namespace Failsafe.Items
{
    /// <summary>
    /// Предмет, который можно использовать
    /// </summary>
    public interface IUsable
    {
        /// <summary>
        /// Использовать
        /// </summary>
        public ItemUseResult Use();

        /// <summary>
        /// Переключить режим
        /// </summary>
        public void AltMode();

        /// <summary>
        /// Передать конкретный gameobject item в скрипты
        /// </summary>
        public void ParseItem(Item item_object);

        public void GetItemUseDelays(out float startDelay, out float useDelay);
    }
}
