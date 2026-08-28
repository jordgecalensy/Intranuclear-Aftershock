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

    public sealed class EffectApplicationService : IEffectApplicationService, ITickable, IDisposable
    {
        private readonly List<Effect> _effects = new();
        private readonly Dictionary<EffectKey, Effect> _uniqueEffects = new();
        private readonly Dictionary<Effect, EffectKey> _effectKeys = new();
        private readonly Dictionary<Effect, EffectOrigin> _effectOrigins = new();

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

        public void Remove(EffectBundle bundle, EffectContext context)
        {
            if (bundle == null || bundle.Effects == null)
                return;

            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target == null)
                return;

            var definitions = new HashSet<EffectDefinition>();

            foreach (EffectDefinition definition in bundle.Effects)
            {
                if (definition != null)
                    definitions.Add(definition);
            }

            if (definitions.Count == 0)
                return;

            int targetId = target.GetInstanceID();

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                Effect effect = _effects[i];

                if (!_effectOrigins.TryGetValue(effect, out EffectOrigin origin))
                    continue;

                if (origin.TargetId != targetId)
                    continue;

                if (!definitions.Contains(origin.Definition))
                    continue;

                RemoveEffectAt(i);
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
                StartAndStore(effect, definition, targetId);
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
                    reapplicable.OnReapply(effect);

                return;
            }

            effect.Start();

            if (effect.ElapsedAt > Time.time)
            {
                _effects.Add(effect);
                _uniqueEffects.Add(key, effect);
                _effectKeys.Add(effect, key);
                _effectOrigins.Add(
                    effect,
                    new EffectOrigin(definition, targetId));
            }
            else
            {
                effect.Dispose();
            }
        }

        private void StartAndStore(
            Effect effect,
            EffectDefinition definition,
            int targetId)
        {
            effect.Start();

            if (effect.ElapsedAt > Time.time)
            {
                _effects.Add(effect);
                _effectOrigins.Add(
                    effect,
                    new EffectOrigin(definition, targetId));
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

            _effects.RemoveAt(index);
            RemoveTracking(effect);
            effect.Dispose();
        }

        private void RemoveTracking(Effect effect)
        {
            _effectOrigins.Remove(effect);

            if (!_effectKeys.TryGetValue(effect, out EffectKey key))
                return;

            _effectKeys.Remove(effect);
            _uniqueEffects.Remove(key);
        }

        private readonly struct EffectOrigin
        {
            public readonly EffectDefinition Definition;
            public readonly int TargetId;

            public EffectOrigin(
                EffectDefinition definition,
                int targetId)
            {
                Definition = definition;
                TargetId = targetId;
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
