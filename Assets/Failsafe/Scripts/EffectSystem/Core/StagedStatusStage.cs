using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public class StagedStatusStage
    {
        [SerializeField] private int _stage = 1;
        [SerializeField] private float _minBuildUp = 1f;

        public int Stage => Mathf.Max(1, _stage);
        public float MinBuildUp => Mathf.Max(0f, _minBuildUp);
    }
}