using System;
using System.Collections.Generic;
using Failsafe.Player.Model;
using Failsafe.PlayerMovements;
using Failsafe.Scripts.Health;
using Failsafe.Scripts.Modifiebles;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    public enum PlayerParameterModifierOperation
    {
        Multiply,
        Add
    }

    public enum PlayerParameterModifierKind
    {
        None,
        WalkSpeed,
        RunSpeed,
        CrouchSpeed,
        JumpMaxHeight,
        JumpMaxSpeed,
        ThrowPower,
        ThrowTorquePower,
        ThrowItemPower,
        MaxHealth,
        MaxStamina,
        HealthRegenerationPerSecond,
        StaminaRegenerationPerSecond,
        NoiseStrengthMultiplier
    }

    [CreateAssetMenu(
        fileName = "PlayerParameterModifierEffectDefinition",
        menuName = "Failsafe/Effects/Positive/Player Parameter Modifier")]
    public sealed class PlayerParameterModifierEffectDefinition : EffectDefinition
    {
        [Header("Modifier")]
        [SerializeField] private bool _permanent = false;

        [SerializeField] private float _duration = 5f;

        [SerializeField] private PlayerParameterModifierOperation _operation =
            PlayerParameterModifierOperation.Multiply;

        [FormerlySerializedAs("_multiplier")]
        [SerializeField] private float _modifierValue = 1.25f;

        [SerializeField] private int _priority = 100;

        [Header("Parameters")]
        [SerializeField] private PlayerParameterModifierKind[] _parameters =
        {
            PlayerParameterModifierKind.WalkSpeed,
            PlayerParameterModifierKind.RunSpeed,
            PlayerParameterModifierKind.CrouchSpeed
        };

        [Header("Debug")]
        [SerializeField] private bool _logResolveErrors = false;
        [SerializeField] private bool _logApply = false;

        public override bool CanApply(EffectContext context)
        {
            return ResolveDuration() > 0f &&
                   IsModifierValueValid() &&
                   TryBuildBindings(context, out PlayerParameterModifierBinding[] bindings) &&
                   bindings.Length > 0;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!TryBuildBindings(context, out PlayerParameterModifierBinding[] bindings))
                return null;

            if (bindings.Length == 0)
                return null;

            return new PlayerParameterModifierEffect(
                bindings,
                ResolveDuration(),
                _operation,
                _modifierValue,
                _priority,
                _logApply);
        }

        public override string GetStackKey(EffectContext context)
        {
            return $"positive.player-parameter-modifier.{GetInstanceID()}";
        }

        private float ResolveDuration()
        {
            return _permanent
                ? float.PositiveInfinity
                : _duration;
        }

        private bool IsModifierValueValid()
        {
            return _operation == PlayerParameterModifierOperation.Multiply
                ? _modifierValue > 0f
                : !Mathf.Approximately(_modifierValue, 0f);
        }

        private bool TryBuildBindings(
            EffectContext context,
            out PlayerParameterModifierBinding[] bindings)
        {
            bindings = Array.Empty<PlayerParameterModifierBinding>();

            if (_parameters == null || _parameters.Length == 0)
                return false;

            GameObject target = ResolveTargetObject(context);

            if (target == null)
            {
                if (_logResolveErrors)
                    EffectLog.Warning(EffectLog.Parameters, "[PlayerParameterModifierEffectDefinition] Target not found.");

                return false;
            }

            LifetimeScope scope = ResolveLifetimeScope(target);

            if (scope == null || scope.Container == null)
            {
                if (_logResolveErrors)
                {
                    EffectLog.Warning(EffectLog.Parameters,
                        $"[PlayerParameterModifierEffectDefinition] LifetimeScope not found near target {target.name}.",
                        target);
                }

                return false;
            }

            PlayerMovementParameters movementParameters = null;
            PlayerModelParameters modelParameters = null;
            PlayerRuntimeParameters runtimeParameters = null;
            PlayerHealth playerHealth = null;
            PlayerStamina playerStamina = null;

            bool movementResolved = false;
            bool modelResolved = false;
            bool runtimeResolved = false;
            bool healthResolved = false;
            bool staminaResolved = false;

            var result = new List<PlayerParameterModifierBinding>(_parameters.Length);
            var usedParameters = new HashSet<PlayerParameterModifierKind>();

            foreach (PlayerParameterModifierKind parameter in _parameters)
            {
                if (parameter == PlayerParameterModifierKind.None)
                    continue;

                if (!usedParameters.Add(parameter))
                    continue;

                if (TryResolveField(
                        scope,
                        parameter,
                        ref movementParameters,
                        ref movementResolved,
                        ref modelParameters,
                        ref modelResolved,
                        ref runtimeParameters,
                        ref runtimeResolved,
                        out ModifiableField<float> field))
                {
                    if (parameter == PlayerParameterModifierKind.MaxHealth)
                    {
                        if (!healthResolved)
                            healthResolved = TryResolve(scope, out playerHealth);

                        if (playerHealth != null)
                        {
                            result.Add(new PlayerParameterModifierBinding(
                                parameter,
                                field,
                                playerHealth.AddMaxHealthModifier,
                                playerHealth.RemoveMaxHealthModifier));
                        }
                    }
                    else if (parameter == PlayerParameterModifierKind.MaxStamina)
                    {
                        if (!staminaResolved)
                            staminaResolved = TryResolve(scope, out playerStamina);

                        if (playerStamina != null)
                        {
                            result.Add(new PlayerParameterModifierBinding(
                                parameter,
                                field,
                                playerStamina.AddMaxStaminaModifier,
                                playerStamina.RemoveMaxStaminaModifier));
                        }
                    }
                    else
                    {
                        result.Add(new PlayerParameterModifierBinding(parameter, field));
                    }
                }
                else if (_logResolveErrors)
                {
                    EffectLog.Warning(EffectLog.Parameters,
                        $"[PlayerParameterModifierEffectDefinition] Cannot resolve player parameter {parameter} near target {target.name}.",
                        target);
                }
            }

            bindings = result.ToArray();
            return bindings.Length > 0;
        }

        private static bool TryResolveField(
            LifetimeScope scope,
            PlayerParameterModifierKind parameter,
            ref PlayerMovementParameters movementParameters,
            ref bool movementResolved,
            ref PlayerModelParameters modelParameters,
            ref bool modelResolved,
            ref PlayerRuntimeParameters runtimeParameters,
            ref bool runtimeResolved,
            out ModifiableField<float> field)
        {
            field = null;

            switch (parameter)
            {
                case PlayerParameterModifierKind.WalkSpeed:
                    if (!movementResolved)
                        movementResolved = TryResolve(scope, out movementParameters);

                    field = movementParameters?.WalkSpeed;
                    return field != null;

                case PlayerParameterModifierKind.RunSpeed:
                    if (!movementResolved)
                        movementResolved = TryResolve(scope, out movementParameters);

                    field = movementParameters?.RunSpeed;
                    return field != null;

                case PlayerParameterModifierKind.CrouchSpeed:
                    if (!movementResolved)
                        movementResolved = TryResolve(scope, out movementParameters);

                    field = movementParameters?.CrouchSpeed;
                    return field != null;

                case PlayerParameterModifierKind.JumpMaxHeight:
                    if (!movementResolved)
                        movementResolved = TryResolve(scope, out movementParameters);

                    field = movementParameters?.JumpMaxHeight;
                    return field != null;

                case PlayerParameterModifierKind.JumpMaxSpeed:
                    if (!movementResolved)
                        movementResolved = TryResolve(scope, out movementParameters);

                    field = movementParameters?.JumpMaxSpeed;
                    return field != null;

                case PlayerParameterModifierKind.ThrowPower:
                    if (!modelResolved)
                        modelResolved = TryResolve(scope, out modelParameters);

                    field = modelParameters?.ThrowPower;
                    return field != null;

                case PlayerParameterModifierKind.ThrowTorquePower:
                    if (!modelResolved)
                        modelResolved = TryResolve(scope, out modelParameters);

                    field = modelParameters?.ThrowTorquePower;
                    return field != null;

                case PlayerParameterModifierKind.ThrowItemPower:
                    if (!modelResolved)
                        modelResolved = TryResolve(scope, out modelParameters);

                    field = modelParameters?.ThrowItemPower;
                    return field != null;

                case PlayerParameterModifierKind.MaxHealth:
                    if (!runtimeResolved)
                        runtimeResolved = TryResolve(scope, out runtimeParameters);

                    field = runtimeParameters?.MaxHealth;
                    return field != null;

                case PlayerParameterModifierKind.MaxStamina:
                    if (!runtimeResolved)
                        runtimeResolved = TryResolve(scope, out runtimeParameters);

                    field = runtimeParameters?.MaxStamina;
                    return field != null;

                case PlayerParameterModifierKind.HealthRegenerationPerSecond:
                    if (!runtimeResolved)
                        runtimeResolved = TryResolve(scope, out runtimeParameters);

                    field = runtimeParameters?.HealthRegenerationPerSecond;
                    return field != null;

                case PlayerParameterModifierKind.StaminaRegenerationPerSecond:
                    if (!runtimeResolved)
                        runtimeResolved = TryResolve(scope, out runtimeParameters);

                    field = runtimeParameters?.StaminaRegenerationPerSecond;
                    return field != null;

                case PlayerParameterModifierKind.NoiseStrengthMultiplier:
                    if (!runtimeResolved)
                        runtimeResolved = TryResolve(scope, out runtimeParameters);

                    field = runtimeParameters?.NoiseStrengthMultiplier;
                    return field != null;

                default:
                    return false;
            }
        }

        private static bool TryResolve<T>(
            LifetimeScope scope,
            out T result)
            where T : class
        {
            result = null;

            if (scope == null || scope.Container == null)
                return false;

            try
            {
                result = scope.Container.Resolve<T>();
                return result != null;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        private static GameObject ResolveTargetObject(EffectContext context)
        {
            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target != null)
                return target;

            if (context.TargetObject != null)
                return context.TargetObject;

            if (context.HitCollider != null)
                return context.HitCollider.transform.root.gameObject;

            return null;
        }

        private static LifetimeScope ResolveLifetimeScope(GameObject target)
        {
            if (target == null)
                return null;

            return target.GetComponent<LifetimeScope>() ??
                   target.GetComponentInParent<LifetimeScope>() ??
                   target.GetComponentInChildren<LifetimeScope>(true);
        }
    }
}
