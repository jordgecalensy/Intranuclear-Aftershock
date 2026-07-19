using System;
using System.Collections.Generic;
using Failsafe.Scripts.Modifiebles;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public readonly struct PlayerParameterModifierBinding
    {
        public readonly PlayerParameterModifierKind Parameter;
        public readonly ModifiableField<float> Field;

        public PlayerParameterModifierBinding(
            PlayerParameterModifierKind parameter,
            ModifiableField<float> field)
        {
            Parameter = parameter;
            Field = field;
        }
    }

    public sealed class PlayerParameterModifierEffect : Effect, IReapplicableEffect
    {
        private readonly int _priority;
        private readonly bool _log;
        private readonly List<AppliedModifier> _appliedModifiers = new();

        private PlayerParameterModifierBinding[] _bindings;
        private float _multiplier;
        private bool _applied;

        public PlayerParameterModifierEffect(
            PlayerParameterModifierBinding[] bindings,
            float duration,
            float multiplier,
            int priority,
            bool log = false)
        {
            _bindings = bindings ?? Array.Empty<PlayerParameterModifierBinding>();
            _duration = Mathf.Max(0f, duration);
            _multiplier = Mathf.Max(0.01f, multiplier);
            _priority = priority;
            _log = log;

            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            if (_applied)
                return;

            foreach (PlayerParameterModifierBinding binding in _bindings)
            {
                if (binding.Field == null)
                    continue;

                var modifier = new MultiplierFloat(_multiplier, _priority);
                binding.Field.AddModificator(modifier);
                _appliedModifiers.Add(new AppliedModifier(binding, modifier));
            }

            _applied = true;

            if (_log)
            {
                Debug.Log(
                    $"[PlayerParameterModifierEffect] Applied {_appliedModifiers.Count} modifiers. Multiplier: {_multiplier:0.###}, duration: {_duration:0.##}s.");
            }
        }

        public override void ClearEffect()
        {
            for (int i = 0; i < _appliedModifiers.Count; i++)
            {
                AppliedModifier appliedModifier = _appliedModifiers[i];
                appliedModifier.Binding.Field?.RemoveModificator(appliedModifier.Modifier);
            }

            if (_log && _appliedModifiers.Count > 0)
            {
                Debug.Log(
                    $"[PlayerParameterModifierEffect] Cleared {_appliedModifiers.Count} modifiers.");
            }

            _appliedModifiers.Clear();
            _applied = false;
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not PlayerParameterModifierEffect reapplied)
                return;

            ClearEffect();

            _bindings = reapplied._bindings;
            _multiplier = reapplied._multiplier;
            _duration = reapplied._duration + (Time.time - StarteAt);

            ApplyEffect();
        }

        private readonly struct AppliedModifier
        {
            public readonly PlayerParameterModifierBinding Binding;
            public readonly IModificator<float> Modifier;

            public AppliedModifier(
                PlayerParameterModifierBinding binding,
                IModificator<float> modifier)
            {
                Binding = binding;
                Modifier = modifier;
            }
        }
    }
}
