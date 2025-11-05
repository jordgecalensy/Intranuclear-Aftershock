using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Менеджер эффектов
    /// </summary>
    public interface IEffectManager
    {
        /// <summary>
        /// Применить эффект
        /// </summary>
        /// <param name="effect">Эффект</param>
        void ApplyEffect(Effect effect);
    }

    public class EffectManager : IEffectManager, ITickable, IDisposable
    {
        private List<Effect> _effects = new List<Effect>();

        public void ApplyEffect(Effect effect)
        {
            if (effect.IsUniqueEffect)
            {
                var currentEffect = _effects.FirstOrDefault(x => x.GetType() == effect.GetType());
                if (currentEffect != null)
                {
                    return;
                }
            }

            effect.Start();
            _effects.Add(effect);
        }

        public void Tick()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                Effect effect = _effects[i];
                effect.Update();
                if (effect.ElapsedAt < Time.time)
                {
                    effect.Dispose();
                    _effects.RemoveAt(i);
                }
            }
        }

        public void Dispose()
        {
            foreach (var effect in _effects)
            {
                effect.Dispose();
            }
            _effects.Clear();
        }
    }
}
