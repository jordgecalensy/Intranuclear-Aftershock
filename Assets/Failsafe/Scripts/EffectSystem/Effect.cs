using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Базовый класс для всех Эффектов в игре
    /// </summary>
    public abstract class Effect : IDisposable
    {
        protected float _duration;
        private float _startedAt;

        public float ElapsedAt => _startedAt + _duration;

        /// <summary>
        /// Является уникальным эффектом, нельзя применить повторно
        /// </summary>
        public bool IsUniqueEffect { get; protected set; }

        /// <summary>
        /// Применить эффект
        /// </summary>
        /// <remarks>
        /// Вызывается когда применяется эффект через менеджер <see cref="IEffectManager.ApplyEffect(Effect)"/>
        /// </remarks>
        public abstract void ApplyEffect();
        /// <summary>
        /// Очистить эффект
        /// </summary>
        /// <remarks>
        /// Вызывается когда заканчивается действие эффекта
        /// </remarks>
        public abstract void ClearEffect();

        public void Start()
        {
            _startedAt = Time.time;
            ApplyEffect();
        }

        /// <summary>
        /// Выполняется каждый тик
        /// </summary>
        public virtual void Update() { }

        public void Dispose()
        {
            ClearEffect();
        }
    }
}
