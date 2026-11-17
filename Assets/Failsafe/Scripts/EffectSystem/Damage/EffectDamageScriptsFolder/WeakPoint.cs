using System;
using UnityEngine;

namespace Failsafe.Scripts.Damage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Failsafe/Damage/Weak Point")]
    public sealed class WeakPoint : MonoBehaviour
    {
        [Tooltip("Множитель урона попадания. Продолжает действовать после достижения порога.")]
        [SerializeField, Min(0f)] private float _damageMultiplier = 1.5f;

        [Tooltip("Сколько фактически снятого общего здоровья нужно накопить для однократного срабатывания.")]
        [SerializeField, Min(0.01f)] private float _damageThreshold = 50f;

        private float _accumulatedDamage;
        private bool _thresholdReached;

        public float DamageMultiplier => Mathf.Max(0f, _damageMultiplier);
        public float DamageThreshold => Mathf.Max(0.01f, _damageThreshold);
        public float AccumulatedDamage => _accumulatedDamage;
        public bool IsThresholdReached => _thresholdReached;

        public event Action ThresholdReached;

        internal void RegisterDamage(float amount)
        {
            if (_thresholdReached || amount <= 0f) return;

            _accumulatedDamage += amount;
            if (_accumulatedDamage < DamageThreshold) return;

            _thresholdReached = true;
            ThresholdReached?.Invoke();
        }
    }
}
