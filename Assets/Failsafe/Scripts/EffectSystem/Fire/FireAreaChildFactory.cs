using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Creates and configures children produced by one fire area.
    /// </summary>
    public sealed class FireAreaChildFactory
    {
        private readonly FireAreaAdvanced _source;

        public FireAreaChildFactory(FireAreaAdvanced source)
        {
            _source = source != null
                ? source
                : throw new ArgumentNullException(nameof(source));
        }

        public bool TryCreate(Vector3 position)
        {
            if (_source.firePrefab == null)
                return false;

            FireAreaAdvanced child = UnityEngine.Object.Instantiate(
                _source.firePrefab,
                position,
                Quaternion.identity);

            if (child == null)
                return false;

            CopyGrowthSettings(child);
            CopyContactSettings(child);
            CopyPropagationSettings(child);
            CopyVisualSettings(child);
            child.SetEffectBundles(
                _source.ContactEffects,
                _source.BurnEffects);
            child.Construct(_source.EffectService);
            child.RefreshRuntimeConfiguration();
            return true;
        }

        private void CopyGrowthSettings(FireAreaAdvanced child)
        {
            child.initialRadius = Mathf.Max(
                0.4f,
                _source.initialRadius * _source.childRadiusFactor);
            child.maxRadius = Mathf.Max(
                child.initialRadius,
                _source.maxRadius * _source.childRadiusFactor);
            child.radiusGrowPerSec = _source.radiusGrowPerSec * 0.9f;

            child.intensity = Mathf.Max(
                0.1f,
                _source.intensity * _source.childIntensityFactor);
            child.intensityGrowPerSec =
                _source.intensityGrowPerSec * 0.9f;
            child.mediumThreshold = _source.mediumThreshold;
            child.strongThreshold = _source.strongThreshold;
            child.peakIntensity = Mathf.Max(
                child.intensity + 0.1f,
                _source.peakIntensity * 0.9f);
            child.sustainIntensity = Mathf.Min(
                child.strongThreshold - 0.01f,
                _source.sustainIntensity);
            child.burnoutDecayPerSec = _source.burnoutDecayPerSec;
        }

        private void CopyContactSettings(FireAreaAdvanced child)
        {
            child.targetMask = _source.targetMask;
            child.tickInterval = _source.tickInterval;
            child.maxTargetsPerTick = Mathf.Max(
                8,
                (int)(_source.maxTargetsPerTick * 0.7f));
            child.dotDpsPerIntensity = _source.dotDpsPerIntensity;
            child.mediumDotIntensity = Mathf.Max(
                0.1f,
                _source.mediumDotIntensity * 0.9f);
            child.strongDotIntensity = Mathf.Max(
                0.1f,
                _source.strongDotIntensity * 0.9f);
        }

        private void CopyPropagationSettings(FireAreaAdvanced child)
        {
            child.enableSpreading = _source.enableSpreading;
            child.firePrefab = _source.firePrefab;
            child.spreadEvery = _source.spreadEvery *
                UnityEngine.Random.Range(0.9f, 1.2f);
            child.spreadChance = _source.spreadChance * 0.9f;
            child.maxChildren = Math.Max(0, _source.maxChildren - 1);
            child.spreadDistance = _source.spreadDistance;
            child.childIntensityFactor = _source.childIntensityFactor;
            child.childRadiusFactor = _source.childRadiusFactor;
        }

        private void CopyVisualSettings(FireAreaAdvanced child)
        {
            child.fxPrefab = _source.fxPrefab;
            child.fxSmoothTime = _source.fxSmoothTime;
            child.fxPrewarmTime = _source.fxPrewarmTime;
            child.scaleCurve = _source.scaleCurve;
            child.emissionCurve = _source.emissionCurve;
        }
    }
}
