using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class StatusEffectState : MonoBehaviour
    {
        [Header("Resistance")]
        [Tooltip("Постоянные иммунитеты/резисты этой цели. Можно оставить пустым.")]
        [SerializeField] private StatusResistanceProfile _resistanceProfile;

        [Header("Runtime Resistance Modifiers")]
        [SerializeField] private List<RuntimeStatusResistanceModifier> _runtimeResistanceModifiers = new();

        private readonly Dictionary<StatusEffectType, IRegisteredStatusEffect> _activeStatuses = new();
        private readonly Dictionary<StatusEffectType, float> _immunityUntil = new();

        public StatusResistanceProfile ResistanceProfile => _resistanceProfile;

        public bool HasStatus(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return false;

            return _activeStatuses.ContainsKey(statusType);
        }

        public bool TryGetStatus(
            StatusEffectType statusType,
            out IRegisteredStatusEffect effect)
        {
            effect = null;

            if (statusType == StatusEffectType.None)
                return false;

            return _activeStatuses.TryGetValue(statusType, out effect) &&
                   effect != null;
        }

        public int GetStatusStage(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return 0;

            if (!_activeStatuses.TryGetValue(statusType, out IRegisteredStatusEffect effect))
                return 0;

            if (effect == null)
                return 0;

            if (effect is IStagedStatusEffect stagedStatus)
                return stagedStatus.CurrentStage;

            return 1;
        }

        public float GetStatusBuildUpValue(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return 0f;

            if (!_activeStatuses.TryGetValue(statusType, out IRegisteredStatusEffect effect))
                return 0f;

            if (effect == null)
                return 0f;

            if (effect is IStagedStatusEffect stagedStatus)
                return stagedStatus.BuildUpValue;

            return 1f;
        }

        public bool IsImmune(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return false;

            return IsTemporarilyImmune(statusType) ||
                   IsPermanentlyImmune(statusType);
        }

        public bool IsTemporarilyImmune(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return false;

            return _immunityUntil.TryGetValue(statusType, out float until) &&
                   until > Time.time;
        }

        public bool IsPermanentlyImmune(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return false;

            return _resistanceProfile != null &&
                   _resistanceProfile.IsImmune(statusType);
        }

        public bool CanReceive(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return false;

            return !IsImmune(statusType);
        }

        public float GetStatusDurationMultiplier(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return 1f;

            if (IsImmune(statusType))
                return 0f;

            float result = 1f;

            if (_resistanceProfile != null)
                result *= _resistanceProfile.GetDurationMultiplier(statusType);

            result *= GetRuntimeDurationMultiplier(statusType);

            return Mathf.Max(0f, result);
        }

        public float GetStatusBuildUpMultiplier(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return 1f;

            if (IsImmune(statusType))
                return 0f;

            float result = 1f;

            if (_resistanceProfile != null)
                result *= _resistanceProfile.GetBuildUpMultiplier(statusType);

            result *= GetRuntimeBuildUpMultiplier(statusType);

            return Mathf.Max(0f, result);
        }

        public float GetRuntimeDurationMultiplier(StatusEffectType statusType)
        {
            float result = 1f;

            for (int i = 0; i < _runtimeResistanceModifiers.Count; i++)
            {
                RuntimeStatusResistanceModifier modifier = _runtimeResistanceModifiers[i];

                if (modifier == null)
                    continue;

                if (modifier.StatusType != statusType)
                    continue;

                result *= modifier.DurationMultiplier;
            }

            return result;
        }

        public float GetRuntimeBuildUpMultiplier(StatusEffectType statusType)
        {
            float result = 1f;

            for (int i = 0; i < _runtimeResistanceModifiers.Count; i++)
            {
                RuntimeStatusResistanceModifier modifier = _runtimeResistanceModifiers[i];

                if (modifier == null)
                    continue;

                if (modifier.StatusType != statusType)
                    continue;

                result *= modifier.BuildUpMultiplier;
            }

            return result;
        }

        public void AddRuntimeResistanceModifier(
            string sourceId,
            StatusEffectType statusType,
            float durationMultiplier,
            float buildUpMultiplier)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                EffectLog.Warning(EffectLog.Status, $"[StatusEffectState] {name}: sourceId is empty. Runtime status modifier was not added.", this);
                return;
            }

            if (statusType == StatusEffectType.None)
                return;

            durationMultiplier = Mathf.Max(0f, durationMultiplier);
            buildUpMultiplier = Mathf.Max(0f, buildUpMultiplier);

            for (int i = 0; i < _runtimeResistanceModifiers.Count; i++)
            {
                RuntimeStatusResistanceModifier modifier = _runtimeResistanceModifiers[i];

                if (modifier == null)
                    continue;

                if (modifier.SourceId != sourceId)
                    continue;

                if (modifier.StatusType != statusType)
                    continue;

                modifier.Set(durationMultiplier, buildUpMultiplier);
                return;
            }

            _runtimeResistanceModifiers.Add(
                new RuntimeStatusResistanceModifier(
                    sourceId,
                    statusType,
                    durationMultiplier,
                    buildUpMultiplier));
        }

        public void RemoveRuntimeResistanceModifier(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            for (int i = _runtimeResistanceModifiers.Count - 1; i >= 0; i--)
            {
                RuntimeStatusResistanceModifier modifier = _runtimeResistanceModifiers[i];

                if (modifier == null)
                {
                    _runtimeResistanceModifiers.RemoveAt(i);
                    continue;
                }

                if (modifier.SourceId == sourceId)
                    _runtimeResistanceModifiers.RemoveAt(i);
            }
        }

        public void RemoveRuntimeResistanceModifier(
            string sourceId,
            StatusEffectType statusType)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            for (int i = _runtimeResistanceModifiers.Count - 1; i >= 0; i--)
            {
                RuntimeStatusResistanceModifier modifier = _runtimeResistanceModifiers[i];

                if (modifier == null)
                {
                    _runtimeResistanceModifiers.RemoveAt(i);
                    continue;
                }

                if (modifier.SourceId == sourceId &&
                    modifier.StatusType == statusType)
                {
                    _runtimeResistanceModifiers.RemoveAt(i);
                }
            }
        }

        public void RegisterStatus(StatusEffectType statusType, IRegisteredStatusEffect effect)
        {
            if (statusType == StatusEffectType.None)
                return;

            if (effect == null)
                return;

            _activeStatuses[statusType] = effect;

            EffectLog.Info(EffectLog.Status, $"[StatusEffectState] {name}: registered {statusType}", this);
        }

        public void UnregisterStatus(StatusEffectType statusType, IRegisteredStatusEffect effect)
        {
            if (statusType == StatusEffectType.None)
                return;

            if (!_activeStatuses.TryGetValue(statusType, out IRegisteredStatusEffect current))
                return;

            if (current != effect)
                return;

            _activeStatuses.Remove(statusType);

            EffectLog.Info(EffectLog.Status, $"[StatusEffectState] {name}: unregistered {statusType}", this);
        }

        public void RemoveStatus(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return;

            if (!_activeStatuses.TryGetValue(statusType, out IRegisteredStatusEffect effect))
                return;

            effect.ForceClearFromStatusState();
        }

        public void RemoveStatuses(IEnumerable<StatusEffectType> statuses)
        {
            if (statuses == null)
                return;

            foreach (StatusEffectType status in statuses)
                RemoveStatus(status);
        }

        public void AddTemporaryImmunity(StatusEffectType statusType, float duration)
        {
            if (statusType == StatusEffectType.None)
                return;

            duration = Mathf.Max(0f, duration);

            if (duration <= 0f)
                return;

            float until = Time.time + duration;

            if (_immunityUntil.TryGetValue(statusType, out float currentUntil))
                _immunityUntil[statusType] = Mathf.Max(currentUntil, until);
            else
                _immunityUntil.Add(statusType, until);

            EffectLog.Info(EffectLog.Status, $"[StatusEffectState] {name}: temporary immunity {statusType} for {duration:0.00}s", this);
        }

        public void AddTemporaryImmunity(IEnumerable<StatusEffectType> statuses, float duration)
        {
            if (statuses == null)
                return;

            foreach (StatusEffectType status in statuses)
                AddTemporaryImmunity(status, duration);
        }

        public void SetResistanceProfile(StatusResistanceProfile resistanceProfile)
        {
            _resistanceProfile = resistanceProfile;
        }
    }
}