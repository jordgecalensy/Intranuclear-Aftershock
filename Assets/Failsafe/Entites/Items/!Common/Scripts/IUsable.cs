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

        public void GetItemUseDelays(out float startDelay, out float useDelay);
    }
}
