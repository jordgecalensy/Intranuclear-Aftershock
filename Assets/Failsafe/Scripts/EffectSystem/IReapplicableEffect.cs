namespace Failsafe.Scripts.EffectSystem
{
    public interface IReapplicableEffect
    {
        /// <summary>
        /// Вызывается, если прилетает такой же уникальный эффект повторно.
        /// Можно продлить длительность/обновить параметры.
        /// </summary>
        void OnReapply(Effect newEffect);
    }
}