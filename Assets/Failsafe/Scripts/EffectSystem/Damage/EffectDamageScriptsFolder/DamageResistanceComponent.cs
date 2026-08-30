using System.Collections.Generic;
using Failsafe.Scripts.Damage;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [DisallowMultipleComponent]
    public class DamageResistanceComponent : MonoBehaviour
    {
        [Header("Base")]
        [SerializeField] private DamageResistanceProfile _baseProfile;

        [Header("Local Overrides")]
        [Tooltip("Локальные значения на конкретном prefab/объекте. Имеют приоритет над Base Profile.")]
        [SerializeField] private DamageResistanceEntry[] _localOverrides;

        [Header("Runtime Modifiers")]
        [SerializeField] private List<RuntimeDamageResistanceModifier> _runtimeModifiers = new();

        [Header("Debug")]
        [SerializeField] private bool _log;

        public DamageResistanceProfile BaseProfile => _baseProfile;

        public float GetDamageMultiplier(DamageType damageType)
        {
            float baseMultiplier = GetBaseMultiplier(damageType);
            float runtimeMultiplier = GetRuntimeMultiplier(damageType);
            float finalMultiplier = baseMultiplier * runtimeMultiplier;

            if (_log)
            {
                EffectLog.Info(EffectLog.Resistance,
                    $"[DamageResistanceComponent] {name}: {damageType} multiplier = base {baseMultiplier:0.###} * runtime {runtimeMultiplier:0.###} = {finalMultiplier:0.###}",
                    this);
            }

            return finalMultiplier;
        }

        public float GetBaseMultiplier(DamageType damageType)
        {
            if (TryGetLocalOverride(damageType, out float localMultiplier))
                return localMultiplier;

            if (_baseProfile != null)
                return _baseProfile.GetBaseMultiplier(damageType);

            return 1f;
        }

        public float GetRuntimeMultiplier(DamageType damageType)
        {
            float result = 1f;

            for (int i = 0; i < _runtimeModifiers.Count; i++)
            {
                RuntimeDamageResistanceModifier modifier = _runtimeModifiers[i];

                if (modifier == null)
                    continue;

                if (modifier.DamageType != damageType)
                    continue;

                result *= modifier.Multiplier;
            }

            return result;
        }

        public void AddRuntimeModifier(
            string sourceId,
            DamageType damageType,
            float multiplier)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                EffectLog.Warning(EffectLog.Resistance,
                    $"[DamageResistanceComponent] {name}: sourceId is empty. Runtime modifier was not added.",
                    this);

                return;
            }

            multiplier = Mathf.Max(0f, multiplier);

            for (int i = 0; i < _runtimeModifiers.Count; i++)
            {
                RuntimeDamageResistanceModifier modifier = _runtimeModifiers[i];

                if (modifier == null)
                    continue;

                if (modifier.SourceId != sourceId)
                    continue;

                if (modifier.DamageType != damageType)
                    continue;

                modifier.SetMultiplier(multiplier);

                if (_log)
                {
                    EffectLog.Info(EffectLog.Resistance,
                        $"[DamageResistanceComponent] {name}: updated runtime modifier {sourceId}, {damageType} x{multiplier:0.###}",
                        this);
                }

                return;
            }

            _runtimeModifiers.Add(
                new RuntimeDamageResistanceModifier(
                    sourceId,
                    damageType,
                    multiplier));

            if (_log)
            {
                EffectLog.Info(EffectLog.Resistance,
                    $"[DamageResistanceComponent] {name}: added runtime modifier {sourceId}, {damageType} x{multiplier:0.###}",
                    this);
            }
        }

        public void RemoveRuntimeModifier(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            for (int i = _runtimeModifiers.Count - 1; i >= 0; i--)
            {
                RuntimeDamageResistanceModifier modifier = _runtimeModifiers[i];

                if (modifier == null)
                {
                    _runtimeModifiers.RemoveAt(i);
                    continue;
                }

                if (modifier.SourceId == sourceId)
                    _runtimeModifiers.RemoveAt(i);
            }

            if (_log)
            {
                EffectLog.Info(EffectLog.Resistance,
                    $"[DamageResistanceComponent] {name}: removed runtime modifiers from {sourceId}",
                    this);
            }
        }

        public void RemoveRuntimeModifier(
            string sourceId,
            DamageType damageType)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            for (int i = _runtimeModifiers.Count - 1; i >= 0; i--)
            {
                RuntimeDamageResistanceModifier modifier = _runtimeModifiers[i];

                if (modifier == null)
                {
                    _runtimeModifiers.RemoveAt(i);
                    continue;
                }

                if (modifier.SourceId == sourceId &&
                    modifier.DamageType == damageType)
                {
                    _runtimeModifiers.RemoveAt(i);
                }
            }

            if (_log)
            {
                EffectLog.Info(EffectLog.Resistance,
                    $"[DamageResistanceComponent] {name}: removed runtime modifier {sourceId}, {damageType}",
                    this);
            }
        }

        public void ClearRuntimeModifiers()
        {
            _runtimeModifiers.Clear();

            if (_log)
            {
                EffectLog.Info(EffectLog.Resistance,
                    $"[DamageResistanceComponent] {name}: cleared all runtime modifiers",
                    this);
            }
        }

        public void SetBaseProfile(DamageResistanceProfile profile)
        {
            _baseProfile = profile;
        }

        private bool TryGetLocalOverride(
            DamageType damageType,
            out float multiplier)
        {
            multiplier = 1f;

            if (_localOverrides == null)
                return false;

            for (int i = 0; i < _localOverrides.Length; i++)
            {
                DamageResistanceEntry entry = _localOverrides[i];

                if (entry == null)
                    continue;

                if (entry.DamageType != damageType)
                    continue;

                multiplier = entry.Multiplier;
                return true;
            }

            return false;
        }
    }
}