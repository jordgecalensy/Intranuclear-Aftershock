using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public class StatusResistanceEntry
    {
        [SerializeField] private StatusEffectType _statusType = StatusEffectType.None;

        [Tooltip("Если true, статус вообще не может быть наложен.")]
        [SerializeField] private bool _immune;

        [Tooltip("Множитель длительности. 1 = обычная длительность, 0.5 = в два раза короче, 0 = не висит.")]
        [SerializeField, Min(0f)] private float _durationMultiplier = 1f;

        [Tooltip("Множитель накопления стадии. Для Cold/Poison. 1 = обычное накопление, 0.5 = медленнее, 0 = не накапливается.")]
        [SerializeField, Min(0f)] private float _buildUpMultiplier = 1f;

        public StatusEffectType StatusType => _statusType;
        public bool Immune => _immune;
        public float DurationMultiplier => Mathf.Max(0f, _durationMultiplier);
        public float BuildUpMultiplier => Mathf.Max(0f, _buildUpMultiplier);
    }
}