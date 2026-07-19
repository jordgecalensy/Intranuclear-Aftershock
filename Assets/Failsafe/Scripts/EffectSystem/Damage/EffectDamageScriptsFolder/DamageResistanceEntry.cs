using System;
using Failsafe.Scripts.Damage;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public class DamageResistanceEntry
    {
        [SerializeField] private DamageType _damageType = DamageType.Physical;

        [Tooltip("1 = обычный урон, 0.5 = половина, 0 = иммунитет, 1.5 = уязвимость.")]
        [SerializeField, Min(0f)] private float _multiplier = 1f;

        public DamageType DamageType => _damageType;
        public float Multiplier => Mathf.Max(0f, _multiplier);
    }
}