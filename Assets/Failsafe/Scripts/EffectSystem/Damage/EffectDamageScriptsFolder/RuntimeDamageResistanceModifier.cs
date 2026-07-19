using System;
using Failsafe.Scripts.Damage;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public class RuntimeDamageResistanceModifier
    {
        [SerializeField] private string _sourceId;
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [SerializeField, Min(0f)] private float _multiplier = 1f;

        public string SourceId => _sourceId;
        public DamageType DamageType => _damageType;
        public float Multiplier => Mathf.Max(0f, _multiplier);

        public RuntimeDamageResistanceModifier(
            string sourceId,
            DamageType damageType,
            float multiplier)
        {
            _sourceId = sourceId;
            _damageType = damageType;
            _multiplier = Mathf.Max(0f, multiplier);
        }

        public void SetMultiplier(float multiplier)
        {
            _multiplier = Mathf.Max(0f, multiplier);
        }
    }
}