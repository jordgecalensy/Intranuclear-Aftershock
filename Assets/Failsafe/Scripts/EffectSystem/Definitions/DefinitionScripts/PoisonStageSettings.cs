using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public class PoisonStageSettings
    {
        [SerializeField] private int _stage = 1;
        [SerializeField] private float _minBuildUp = 1f;

        [Header("Stamina")]
        [Tooltip("Сколько выносливости тратить каждую секунду на этой стадии.")]
        [SerializeField] private float _staminaSpendPerSecond = 0f;

        [Header("Damage")]
        [Tooltip("Сколько урона наносить за один тик. 0 = урона нет.")]
        [SerializeField] private float _damagePerTick = 0f;

        [Tooltip("Интервал между тиками урона.")]
        [SerializeField] private float _damageTickInterval = 1f;

        public int Stage => Mathf.Max(1, _stage);
        public float MinBuildUp => Mathf.Max(0f, _minBuildUp);
        public float StaminaSpendPerSecond => Mathf.Max(0f, _staminaSpendPerSecond);
        public float DamagePerTick => Mathf.Max(0f, _damagePerTick);
        public float DamageTickInterval => Mathf.Max(0.01f, _damageTickInterval);
    }
}