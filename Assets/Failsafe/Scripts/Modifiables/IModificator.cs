namespace Failsafe.Scripts.Modifiebles
{
    /// <summary>
    /// Модификатор
    /// </summary>
    /// <typeparam name="T">Тип изменяемого параметра</typeparam>
    public interface IModificator<T>
    {
        /// <summary>
        /// Приоритет срабатывания
        /// </summary>
        /// <remarks>
        /// Чем выше, тем раньше примениться эффект. 
        /// Модификаторы с низким приоритетом накладываются в последнюю очередь
        /// </remarks>
        public int Priority => 0;
        /// <summary>
        /// Модификация параметра
        /// </summary>
        /// <param name="input">Текущее значение</param>
        /// <returns>Измененное значение</returns>
        public T Modify(T input);
    }

    /// <summary>
    /// Умножить
    /// </summary>
    public class MultiplierFloat : IModificator<float>
    {
        private readonly float _multiplier;

        /// <summary>
        /// Умножить на заданное значение
        /// </summary>
        /// <param name="multiplier">Множитель</param>
        /// <param name="priority">Приоритет срабатывания</param>
        public MultiplierFloat(float multiplier, int priority = 0)
        {
            _multiplier = multiplier;
            Priority = priority;
        }

        public int Priority { get; }

        public float Modify(float input) => input * _multiplier;
    }

    /// <summary>
    /// Прибавить
    /// </summary>
    public class AdderFloat : IModificator<float>
    {
        private readonly float _addition;

        /// <summary>
        /// Прибавить заданное значение
        /// </summary>
        /// <param name="addition">Сколько добавить</param>
        /// <param name="priority">Приоритет срабатывания</param>
        public AdderFloat(float addition, int priority = 0)
        {
            _addition = addition;
            Priority = priority;
        }

        public int Priority { get; }

        public float Modify(float input) => input + _addition;
    }
}