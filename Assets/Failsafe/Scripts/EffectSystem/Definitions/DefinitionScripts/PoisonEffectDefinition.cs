using System;
using Failsafe.Player.Model;
using Failsafe.Scripts.Damage;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "PoisonEffectDefinition",
        menuName = "Failsafe/Effects/Statuses/Poison")]
    public class PoisonEffectDefinition :
        EffectDefinition,
        IStatusEffectDefinition,
        IStagedStatusEffectDefinition
    {
        [Header("Poison")]
        [Tooltip("Сколько секунд отравление живёт без повторного получения.")]
        [SerializeField] private float _duration = 8f;

        [Header("Build Up")]
        [Tooltip("Сколько накопления добавляет одно применение.")]
        [SerializeField] private float _buildUpPerApplication = 1f;

        [Tooltip("Максимальное накопление.")]
        [SerializeField] private float _maxBuildUp = 3f;

        [SerializeField] private bool _clampBuildUpToMax = true;

        [Header("Stages")]
        [SerializeField] private PoisonStageSettings[] _stages =
        {
            new PoisonStageSettings(),
            new PoisonStageSettings(),
            new PoisonStageSettings()
        };

        [Header("Target")]
        [Tooltip("Если true, StatusEffectState будет автоматически добавлен на игрока.")]
        [SerializeField] private bool _autoAddStatusState = true;

        [Tooltip("Если true, эффект будет применяться только к объектам, где можно resolve IStamina.")]
        [SerializeField] private bool _requirePlayerStamina = true;

        [Header("On Apply")]
        [SerializeField] private StatusEffectType[] _removeStatusesOnApply;

        [Header("On End")]
        [SerializeField] private StatusEffectType[] _immunityStatusesOnEnd;

        [SerializeField] private float _immunityDurationOnEnd = 0f;

        [Header("Debug")]
        [SerializeField] private bool _logResolveErrors = true;

        public StatusEffectType StatusType => StatusEffectType.Poison;

        public override bool CanApply(EffectContext context)
        {
            if (!StatusEffectStateResolver.TryResolve(
                    context,
                    _autoAddStatusState,
                    out StatusEffectState state))
            {
                return false;
            }

            if (state == null ||
                !state.CanReceive(StatusEffectType.Poison) ||
                StatusResistanceUtility.ApplyDurationMultiplier(
                    state,
                    StatusEffectType.Poison,
                    _duration) <= 0f ||
                StatusResistanceUtility.ApplyBuildUpMultiplier(
                    state,
                    StatusEffectType.Poison,
                    _buildUpPerApplication) <= 0f)
            {
                return false;
            }

            if (!_requirePlayerStamina)
                return true;

            return ResolveStamina(context) != null;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            if (!StatusEffectStateResolver.TryResolve(
                    context,
                    _autoAddStatusState,
                    out StatusEffectState state))
            {
                return null;
            }

            if (state == null)
                return null;

            IStamina stamina = ResolveStamina(context);

            if (_requirePlayerStamina && stamina == null)
                return null;

            DamageTargetResolver.TryResolve(context, out DamageTarget damageTarget);

            float duration = StatusResistanceUtility.ApplyDurationMultiplier(
                state,
                StatusEffectType.Poison,
                _duration);

            float buildUpPerApplication = StatusResistanceUtility.ApplyBuildUpMultiplier(
                state,
                StatusEffectType.Poison,
                _buildUpPerApplication);

            return new PoisonEffect(
                state,
                stamina,
                damageTarget,
                context.Source,
                context.Point,
                context.Direction,
                context.Power,
                duration,
                buildUpPerApplication,
                _maxBuildUp,
                _clampBuildUpToMax,
                _stages,
                _removeStatusesOnApply,
                _immunityStatusesOnEnd,
                _immunityDurationOnEnd);
        }

        public int PredictStageAfterApply(StatusEffectState state, EffectContext context)
        {
            float currentBuildUp = state != null
                ? state.GetStatusBuildUpValue(StatusEffectType.Poison)
                : 0f;

            float buildUpPerApplication = state != null
                ? StatusResistanceUtility.ApplyBuildUpMultiplier(
                    state,
                    StatusEffectType.Poison,
                    _buildUpPerApplication)
                : Mathf.Max(0f, _buildUpPerApplication);

            float predictedBuildUp = currentBuildUp + Mathf.Max(0f, buildUpPerApplication);

            if (_clampBuildUpToMax)
                predictedBuildUp = Mathf.Min(predictedBuildUp, Mathf.Max(0f, _maxBuildUp));

            return CalculateStage(predictedBuildUp, _stages);
        }

        public override string GetStackKey(EffectContext context)
        {
            if (StatusEffectStateResolver.TryResolve(
                    context,
                    false,
                    out StatusEffectState state) &&
                state != null)
            {
                return $"status.Poison.{state.GetInstanceID()}";
            }

            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target != null)
                return $"status.Poison.target.{target.GetInstanceID()}";

            if (context.HitCollider != null)
                return $"status.Poison.collider.{context.HitCollider.GetInstanceID()}";

            return "status.Poison";
        }

        public static int CalculateStage(
            float buildUpValue,
            PoisonStageSettings[] stages)
        {
            if (buildUpValue <= 0f)
                return 0;

            if (stages == null || stages.Length == 0)
                return 1;

            int result = 0;

            for (int i = 0; i < stages.Length; i++)
            {
                PoisonStageSettings stage = stages[i];

                if (stage == null)
                    continue;

                if (buildUpValue >= stage.MinBuildUp)
                    result = Mathf.Max(result, stage.Stage);
            }

            if (result <= 0)
                result = 1;

            return result;
        }

        private IStamina ResolveStamina(EffectContext context)
        {
            GameObject target = StatusEffectStateResolver.ResolveTargetObject(context);

            if (target == null && context.HitCollider != null)
                target = context.HitCollider.transform.root.gameObject;

            if (target == null)
                return null;

            LifetimeScope scope = ResolveLifetimeScope(target);

            if (scope == null)
            {
                if (_logResolveErrors)
                {
                    EffectLog.Warning(EffectLog.Status,
                        $"[PoisonEffectDefinition] LifetimeScope not found near target {target.name}. Poison applies only to player.",
                        target);
                }

                return null;
            }

            if (scope.Container == null)
            {
                if (_logResolveErrors)
                {
                    EffectLog.Warning(EffectLog.Status,
                        $"[PoisonEffectDefinition] LifetimeScope container is null on {scope.name}.",
                        scope);
                }

                return null;
            }

            try
            {
                return scope.Container.Resolve<IStamina>();
            }
            catch (Exception e)
            {
                if (_logResolveErrors)
                {
                    EffectLog.Warning(EffectLog.Status,
                        $"[PoisonEffectDefinition] Cannot resolve IStamina from scope {scope.name}. {e.Message}",
                        scope);
                }

                return null;
            }
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
