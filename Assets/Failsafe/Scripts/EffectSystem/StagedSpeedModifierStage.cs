using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public class StagedSpeedModifierStage
    {
        [SerializeField] private int _stage = 1;

        [Tooltip("1 = обычная скорость, 0.7 = скорость 70%, 0.4 = сильное замедление.")]
        [SerializeField, Range(0.01f, 2f)] private float _speedMultiplier = 1f;

        public int Stage => Mathf.Max(1, _stage);
        public float SpeedMultiplier => Mathf.Max(0.01f, _speedMultiplier);
    }
}