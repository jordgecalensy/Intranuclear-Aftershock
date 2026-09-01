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

        private readonly Action<IModificator<float>> _addModifier;
        private readonly Action<IModificator<float>> _removeModifier;

        public PlayerParameterModifierBinding(
            PlayerParameterModifierKind parameter,
            ModifiableField<float> field,
            Action<IModificator<float>> addModifier = null,
            Action<IModificator<float>> removeModifier = null)
        {
            Parameter = parameter;
            Field = field;
            _addModifier = addModifier;
            _removeModifier = removeModifier;
        }

        public void AddModifier(IModificator<float> modifier)
        {
            if (_addModifier != null)
                _addModifier(modifier);
            else
                Field?.AddModificator(modifier);
        }

        public void RemoveModifier(IModificator<float> modifier)
        {
            if (_removeModifier != null)
                _removeModifier(modifier);
            else
                Field?.RemoveModificator(modifier);
        }
    }

    public sealed class PlayerParameterModifierEffect : Effect, IReapplicableEffect
    {
        private readonly int _priority;
        private readonly bool _log;
        private readonly List<AppliedModifier> _appliedModifiers = new();

        private PlayerParameterModifierBinding[] _bindings;
        private PlayerParameterModifierOperation _operation;
        private float _modifierValue;
        private bool _applied;

        public PlayerParameterModifierEffect(
            PlayerParameterModifierBinding[] bindings,
            float duration,
            PlayerParameterModifierOperation operation,
            float modifierValue,
            int priority,
            bool log = false)
        {
            _bindings = bindings ?? Array.Empty<PlayerParameterModifierBinding>();
            _duration = Mathf.Max(0f, duration);
            _operation = operation;
            _modifierValue = modifierValue;
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

                IModificator<float> modifier = CreateModifier();
                binding.AddModifier(modifier);
                _appliedModifiers.Add(new AppliedModifier(binding, modifier));
            }

            _applied = true;

            if (_log)
            {
                EffectLog.Info(EffectLog.Parameters,
                    $"[PlayerParameterModifierEffect] Applied {_appliedModifiers.Count} modifiers. Operation: {_operation}, value: {_modifierValue:0.###}, duration: {_duration:0.##}s.");
            }
        }

        public override void ClearEffect()
        {
            for (int i = 0; i < _appliedModifiers.Count; i++)
            {
                AppliedModifier appliedModifier = _appliedModifiers[i];
                appliedModifier.Binding.RemoveModifier(appliedModifier.Modifier);
            }

            if (_log && _appliedModifiers.Count > 0)
            {
                EffectLog.Info(EffectLog.Parameters,
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
            _operation = reapplied._operation;
            _modifierValue = reapplied._modifierValue;
            _duration = reapplied._duration + (Time.time - StarteAt);

            ApplyEffect();
        }

        private IModificator<float> CreateModifier()
        {
            return _operation == PlayerParameterModifierOperation.Add
                ? new AdderFloat(_modifierValue, _priority)
                : new MultiplierFloat(_modifierValue, _priority);
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
