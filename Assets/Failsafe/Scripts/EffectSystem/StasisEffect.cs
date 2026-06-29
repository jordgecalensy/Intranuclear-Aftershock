using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class StasisEffect : Effect, IReapplicableEffect
    {
        private readonly Stasisable _target;
        private readonly GameObject _source;

        private bool _restoreVelocityOnExit;

        public StasisEffect(
            Stasisable target,
            float duration,
            bool restoreVelocityOnExit,
            GameObject source)
        {
            _target = target;
            _duration = Mathf.Max(0f, duration);
            _restoreVelocityOnExit = restoreVelocityOnExit;
            _source = source;

            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            if (_target == null)
                return;

            _target.ApplyStasis(
                _duration,
                _restoreVelocityOnExit,
                _source);
        }

        public override void ClearEffect()
        {
            if (_target == null)
                return;

            _target.ClearStasis(
                _restoreVelocityOnExit,
                _source);
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not StasisEffect reapplied)
                return;

            _restoreVelocityOnExit = _restoreVelocityOnExit || reapplied._restoreVelocityOnExit;

            float remaining = Mathf.Max(ElapsedAt - Time.time, 0f);
            float newRemaining = Mathf.Max(remaining, reapplied._duration);

            _duration = (Time.time - StarteAt) + newRemaining;

            if (_target != null)
            {
                _target.ApplyStasis(
                    newRemaining,
                    _restoreVelocityOnExit,
                    _source);
            }
        }
    }
}