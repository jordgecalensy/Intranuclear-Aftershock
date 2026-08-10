using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public class RuntimeStatusResistanceModifier
    {
        [SerializeField] private string _sourceId;
        [SerializeField] private StatusEffectType _statusType = StatusEffectType.None;
        [SerializeField, Min(0f)] private float _durationMultiplier = 1f;
        [SerializeField, Min(0f)] private float _buildUpMultiplier = 1f;

        public string SourceId => _sourceId;
        public StatusEffectType StatusType => _statusType;
        public float DurationMultiplier => Mathf.Max(0f, _durationMultiplier);
        public float BuildUpMultiplier => Mathf.Max(0f, _buildUpMultiplier);

        public RuntimeStatusResistanceModifier(
            string sourceId,
            StatusEffectType statusType,
            float durationMultiplier,
            float buildUpMultiplier)
        {
            _sourceId = sourceId;
            _statusType = statusType;
            _durationMultiplier = Mathf.Max(0f, durationMultiplier);
            _buildUpMultiplier = Mathf.Max(0f, buildUpMultiplier);
        }

        public void Set(
            float durationMultiplier,
            float buildUpMultiplier)
        {
            _durationMultiplier = Mathf.Max(0f, durationMultiplier);
            _buildUpMultiplier = Mathf.Max(0f, buildUpMultiplier);
        }
    }
}