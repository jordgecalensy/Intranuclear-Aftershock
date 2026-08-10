using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    public interface IEffectApplicationService
    {
        void Apply(EffectBundle bundle, EffectContext context);
    }

    public sealed class EffectApplicationService : IEffectApplicationService, ITickable, IDisposable
    {
        private readonly List<Effect> _effects = new();
        private readonly Dictionary<EffectKey, Effect> _uniqueEffects = new();
        private readonly Dictionary<Effect, EffectKey> _effectKeys = new();

        private readonly IStatusReactionService _statusReactionService;

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

        private void RegisterEffect(
            Effect effect,
            EffectDefinition definition,
            EffectContext context)
        {
            if (!effect.IsUniqueEffect)
            {
                StartAndStore(effect);
                return;
            }

            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target == null)
                return;

            var key = new EffectKey(
                target.GetInstanceID(),
                definition.GetStackKey(context));

            if (_uniqueEffects.TryGetValue(key, out var existing))
            {
                if (existing is IReapplicableEffect reapplicable)
                    reapplicable.OnReapply(effect);

                return;
            }

            effect.Start();

            if (effect.ElapsedAt > Time.time)
            {
                _effects.Add(effect);
                _uniqueEffects.Add(key, effect);
                _effectKeys.Add(effect, key);
            }
            else
            {
                effect.Dispose();
            }
        }

        private void StartAndStore(Effect effect)
        {
            effect.Start();

            if (effect.ElapsedAt > Time.time)
                _effects.Add(effect);
            else
                effect.Dispose();
        }

        public void Tick()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];

                effect.Update();

                if (effect.ElapsedAt > Time.time)
                    continue;

                effect.Dispose();
                _effects.RemoveAt(i);

                if (_effectKeys.TryGetValue(effect, out var key))
                {
                    _effectKeys.Remove(effect);
                    _uniqueEffects.Remove(key);
                }
            }
        }

        public void Dispose()
        {
            foreach (var effect in _effects)
                effect.Dispose();

            _effects.Clear();
            _uniqueEffects.Clear();
            _effectKeys.Clear();
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