using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class StatusEffectState : MonoBehaviour
    {
        private readonly Dictionary<StatusEffectType, IRegisteredStatusEffect> _activeStatuses = new();
        private readonly Dictionary<StatusEffectType, float> _immunityUntil = new();

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

            return _immunityUntil.TryGetValue(statusType, out float until) &&
                   until > Time.time;
        }

        public bool CanReceive(StatusEffectType statusType)
        {
            if (statusType == StatusEffectType.None)
                return false;

            return !IsImmune(statusType);
        }

        public void RegisterStatus(StatusEffectType statusType, IRegisteredStatusEffect effect)
        {
            if (statusType == StatusEffectType.None)
                return;

            if (effect == null)
                return;

            _activeStatuses[statusType] = effect;

            Debug.Log($"[StatusEffectState] {name}: registered {statusType}", this);
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

            Debug.Log($"[StatusEffectState] {name}: unregistered {statusType}", this);
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

            Debug.Log($"[StatusEffectState] {name}: immunity {statusType} for {duration:0.00}s", this);
        }

        public void AddTemporaryImmunity(IEnumerable<StatusEffectType> statuses, float duration)
        {
            if (statuses == null)
                return;

            foreach (StatusEffectType status in statuses)
                AddTemporaryImmunity(status, duration);
        }
    }
}