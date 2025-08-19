using System;
using System.Collections.Generic;

namespace Failsafe.Scripts.Modifiebles
{
    /// <summary>
    /// Модифицируемое поле
    /// </summary>
    /// <typeparam name="T">Тип подифицируемого поля</typeparam>
    [Serializable]
    public class ModifiableField<T>
    {
        /// <summary>
        /// Начальное значение
        /// </summary>
        public T BaseValue;
        /// <summary>
        /// Значение после модификаций
        /// </summary>
        public T Value => _cachedValue;
        private T _cachedValue;
        private List<IModificator<T>> _modificators;

        public ModifiableField(T baseValue)
        {
            BaseValue = baseValue;
            _cachedValue = baseValue;
            _modificators = new List<IModificator<T>>();
        }

        /// <summary>
        /// Добавить модификатор
        /// </summary>
        /// <param name="modification"></param>
        public void AddModificator(IModificator<T> modification)
        {
            _modificators.Add(modification);
            _modificators.Sort((m1, m2) => m1.Priority.CompareTo(m2.Priority));
            Recalculate();
        }

        /// <summary>
        /// Удалить модификатор
        /// </summary>
        /// <param name="modification"></param>
        public void RemoveModificator(IModificator<T> modification)
        {
            _modificators.Remove(modification);
            Recalculate();
        }

        /// <summary>
        /// Пересчитать значение применив все модификаторы
        /// </summary>
        /// <remarks>
        /// Значение пересчитывается автоматически когда добавляется/удаляется модификатор. 
        /// Выполнять в случае если используются сложные модификаторы, у которых значения меняются со временем или зависят от внешних событий
        /// </remarks>
        public void Recalculate()
        {
            _cachedValue = BaseValue;
            foreach (var modificator in _modificators)
            {
                _cachedValue = modificator.Modify(_cachedValue);
            }
        }

        public static implicit operator T(ModifiableField<T> m) => m.Value;
        public static implicit operator ModifiableField<T>(T t) => new(t);
    }
}