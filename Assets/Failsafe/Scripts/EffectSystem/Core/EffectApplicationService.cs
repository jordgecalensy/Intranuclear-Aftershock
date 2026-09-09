using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    public interface IEffectApplicationService
    {
        void Apply(EffectBundle bundle, EffectContext context);
        void Remove(EffectBundle bundle, EffectContext context);
    }

    public sealed class EffectApplicationService :
        IEffectApplicationService,
        IEffectPresentationSource,
        ITickable,
        IDisposable
    {
        private readonly List<Effect> _effects = new();
        private readonly Dictionary<EffectKey, Effect> _uniqueEffects = new();
        private readonly Dictionary<Effect, EffectKey> _effectKeys = new();
        private readonly Dictionary<Effect, EffectOrigin> _effectOrigins = new();

        // Переиспользуемый буфер: Remove вызывается при каждом выходе цели
        // из зоны действия эффектов, и новый HashSet на каждый такой вызов давал мусор в GC.
        // Метод не реентерабельный, но реентерабельного пути и нет: изнутри системы
        // эффектов Remove не вызывается (StatusReactionService дёргает только Apply).
        private readonly HashSet<EffectDefinition> _removalDefinitions = new();

        private readonly IStatusReactionService _statusReactionService;

        private readonly PresentationEvent _effectAdded = new();
        private readonly PresentationEvent _effectRefreshed = new();
        private readonly PresentationEvent _effectRemoved = new();

        public event Action<EffectPresentation> EffectAdded
        {
            add => _effectAdded.Add(value);
            remove => _effectAdded.Remove(value);
        }

        public event Action<EffectPresentation> EffectRefreshed
        {
            add => _effectRefreshed.Add(value);
            remove => _effectRefreshed.Remove(value);
        }

        public event Action<EffectPresentation> EffectRemoved
        {
            add => _effectRemoved.Add(value);
            remove => _effectRemoved.Remove(value);
        }

        public EffectApplicationService(IStatusReactionService statusReactionService)
        {
            _statusReactionService = statusReactionService;
        }

        public void Apply(EffectBundle bundle, EffectContext context)
        {
            if (bundle == null || bundle.Effects == null)
                return;

            foreach (var definition in bundle.Effects)
            {
                if (definition == null)
                    continue;

                if (_statusReactionService != null &&
                    _statusReactionService.TryHandleBeforeApply(
                        definition,
                        context,
                        this))
                {
                    if (definition is IStopEffectBundleOnStatusReaction stopReaction &&
                        stopReaction.StopEffectBundleOnStatusReaction)
                    {
                        break;
                    }

                    continue;
                }

                if (!definition.CanApply(context))
                    continue;

                var effect = definition.CreateEffect(context);

                if (effect == null)
                    continue;

                RegisterEffect(effect, definition, context);
            }
        }

        public void Remove(EffectBundle bundle, EffectContext context)
        {
            if (bundle == null || bundle.Effects == null)
                return;

            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target == null)
                return;

            _removalDefinitions.Clear();

            foreach (EffectDefinition definition in bundle.Effects)
            {
                if (definition != null)
                    _removalDefinitions.Add(definition);
            }

            if (_removalDefinitions.Count == 0)
                return;

            int targetId = target.GetInstanceID();

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                Effect effect = _effects[i];

                if (!_effectOrigins.TryGetValue(effect, out EffectOrigin origin))
                    continue;

                if (origin.TargetId != targetId)
                    continue;

                if (!_removalDefinitions.Contains(origin.Definition))
                    continue;

                RemoveEffectAt(i);
            }
        }

        public void GetActiveEffects(List<EffectPresentation> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();

            foreach (Effect effect in _effects)
            {
                if (_effectOrigins.TryGetValue(effect, out EffectOrigin origin))
                    results.Add(origin.Presentation);
            }
        }

        private void RegisterEffect(
            Effect effect,
            EffectDefinition definition,
            EffectContext context)
        {
            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);
            int targetId = target != null
                ? target.GetInstanceID()
                : 0;

            if (!effect.IsUniqueEffect)
            {
                StartAndStore(effect, definition, target, targetId);
                return;
            }

            if (target == null)
                return;

            var key = new EffectKey(
                target.GetInstanceID(),
                definition.GetStackKey(context));

            if (_uniqueEffects.TryGetValue(key, out var existing))
            {
                if (existing is IReapplicableEffect reapplicable)
                {
                    reapplicable.OnReapply(effect);

                    if (_effectOrigins.TryGetValue(existing, out EffectOrigin origin))
                    {
                        origin.Presentation.Refresh(
                            Time.time,
                            GetRemainingDuration(existing));

                        _effectRefreshed.Raise(origin.Presentation);
                    }
                }

                return;
            }

            effect.Start();

            if (effect.ElapsedAt > Time.time)
            {
                var presentation = new EffectPresentation(
                    effect,
                    definition,
                    target,
                    Time.time,
                    GetRemainingDuration(effect));

                _effects.Add(effect);
                _uniqueEffects.Add(key, effect);
                _effectKeys.Add(effect, key);
                _effectOrigins.Add(
                    effect,
                    new EffectOrigin(definition, targetId, presentation));

                _effectAdded.Raise(presentation);
            }
            else
            {
                effect.Dispose();
            }
        }

        private void StartAndStore(
            Effect effect,
            EffectDefinition definition,
            GameObject target,
            int targetId)
        {
            effect.Start();

            if (effect.ElapsedAt > Time.time)
            {
                var presentation = new EffectPresentation(
                    effect,
                    definition,
                    target,
                    Time.time,
                    GetRemainingDuration(effect));

                _effects.Add(effect);
                _effectOrigins.Add(
                    effect,
                    new EffectOrigin(definition, targetId, presentation));

                _effectAdded.Raise(presentation);
            }
            else
            {
                effect.Dispose();
            }
        }

        public void Tick()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];

                effect.Update();

                if (effect.ElapsedAt > Time.time)
                    continue;

                RemoveEffectAt(i);
            }
        }

        public void Dispose()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
                RemoveEffectAt(i);

            _effects.Clear();
            _uniqueEffects.Clear();
            _effectKeys.Clear();
            _effectOrigins.Clear();
        }

        private void RemoveEffectAt(int index)
        {
            Effect effect = _effects[index];

            if (_effectOrigins.TryGetValue(effect, out EffectOrigin origin))
                _effectRemoved.Raise(origin.Presentation);

            _effects.RemoveAt(index);
            RemoveTracking(effect);
            effect.Dispose();
        }

        private static float GetRemainingDuration(Effect effect)
        {
            if (float.IsPositiveInfinity(effect.ElapsedAt))
                return Mathf.Infinity;

            return Mathf.Max(0f, effect.ElapsedAt - Time.time);
        }

        private void RemoveTracking(Effect effect)
        {
            _effectOrigins.Remove(effect);

            if (!_effectKeys.TryGetValue(effect, out EffectKey key))
                return;

            _effectKeys.Remove(effect);
            _uniqueEffects.Remove(key);
        }

        /// <summary>
        /// Событие с кэшированным списком подписчиков.
        /// </summary>
        /// <remarks>
        /// Раньше рассылка шла через GetInvocationList() на каждый вызов — а это новый массив
        /// делегатов при каждом добавлении, обновлении и снятии эффекта. Здесь массив
        /// пересобирается только когда меняются подписки, поэтому в установившемся режиме
        /// рассылка не аллоцирует ничего.
        ///
        /// Перед циклом массив читается в локальную переменную: если подписчик внутри
        /// обработчика подпишется или отпишется, итерация пойдёт по снимку —
        /// ровно та же семантика, что давал GetInvocationList().
        /// </remarks>
        private sealed class PresentationEvent
        {
            private Action<EffectPresentation> _handler;
            private Action<EffectPresentation>[] _subscribers;

            public void Add(Action<EffectPresentation> handler)
            {
                if (handler == null)
                    return;

                _handler += handler;
                _subscribers = null;
            }

            public void Remove(Action<EffectPresentation> handler)
            {
                if (handler == null)
                    return;

                _handler -= handler;
                _subscribers = null;
            }

            public void Raise(EffectPresentation presentation)
            {
                if (_handler == null)
                {
                    _subscribers = null;
                    return;
                }

                if (_subscribers == null)
                {
                    Delegate[] invocationList = _handler.GetInvocationList();
                    var subscribers = new Action<EffectPresentation>[invocationList.Length];

                    for (int i = 0; i < invocationList.Length; i++)
                        subscribers[i] = (Action<EffectPresentation>)invocationList[i];

                    _subscribers = subscribers;
                }

                Action<EffectPresentation>[] snapshot = _subscribers;

                for (int i = 0; i < snapshot.Length; i++)
                {
                    try
                    {
                        snapshot[i](presentation);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
        }

        private readonly struct EffectOrigin
        {
            public readonly EffectDefinition Definition;
            public readonly int TargetId;
            public readonly EffectPresentation Presentation;

            public EffectOrigin(
                EffectDefinition definition,
                int targetId,
                EffectPresentation presentation)
            {
                Definition = definition;
                TargetId = targetId;
                Presentation = presentation;
            }
        }

        private readonly struct EffectKey : IEquatable<EffectKey>
        {
            private readonly int _targetId;
            private readonly string _effectKey;

            public EffectKey(int targetId, string effectKey)
            {
                _targetId = targetId;
                _effectKey = effectKey;
            }

            public bool Equals(EffectKey other)
            {
                return _targetId == other._targetId && _effectKey == other._effectKey;
            }

            public override bool Equals(object obj)
            {
                return obj is EffectKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_targetId, _effectKey);
            }
        }
    }
}
