using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Holds manual or timed stasis without starting a coroutine.
    /// </summary>
    public sealed class ObstacleStasis
    {
        private bool _timed;
        private float _endTime;

        public bool IsFrozen { get; private set; }

        public void Set(bool active)
        {
            _timed = false;
            IsFrozen = active;
        }

        public void Apply(float duration, float currentTime)
        {
            _timed = true;
            _endTime = currentTime + Mathf.Max(0f, duration);
            IsFrozen = true;
        }

        public void Tick(float currentTime)
        {
            if (!_timed || currentTime < _endTime)
                return;

            _timed = false;
            IsFrozen = false;
        }

        public void Clear()
        {
            _timed = false;
            _endTime = 0f;
            IsFrozen = false;
        }
    }
}
