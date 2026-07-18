using System;
using System.Collections.Generic;
using Failsafe.Player.Model;
using Failsafe.PlayerMovements;
using Failsafe.Scripts.Modifiebles;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
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
        ThrowItemPower
    }

    [CreateAssetMenu(
        fileName = "PlayerParameterModifierEffectDefinition",
        menuName = "Failsafe/Effects/Positive/Player Parameter Modifier")]
    public sealed class PlayerParameterModifierEffectDefinition : EffectDefinition
    {
        [Header("Modifier")]
        [SerializeField] private float _duration = 5f;

        [SerializeField, Min(0.01f)] private float _multiplier = 1.25f;

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
            return _duration > 0f &&
                   _multiplier > 0f &&
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
                _duration,
                _multiplier,
                _priority,
                _logApply);
        }

        public override string GetStackKey(EffectContext context)
        {
            return $"positive.player-parameter-modifier.{GetInstanceID()}";
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
                    Debug.LogWarning("[PlayerParameterModifierEffectDefinition] Target not found.");

                return false;
            }

            LifetimeScope scope = ResolveLifetimeScope(target);

            if (scope == null || scope.Container == null)
            {
                if (_logResolveErrors)
                {
                    Debug.LogWarning(
                        $"[PlayerParameterModifierEffectDefinition] LifetimeScope not found near target {target.name}.",
                        target);
                }

                return false;
            }

            PlayerMovementParameters movementParameters = null;
            PlayerModelParameters modelParameters = null;

            bool movementResolved = false;
            bool modelResolved = false;

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
                        out ModifiableField<float> field))
                {
                    result.Add(new PlayerParameterModifierBinding(parameter, field));
                }
                else if (_logResolveErrors)
                {
                    Debug.LogWarning(
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
