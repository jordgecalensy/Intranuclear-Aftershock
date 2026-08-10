using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class SpeedCurveEffect : Effect, IReapplicableEffect
    {
        private readonly PlayerMovementController _controller;
        private readonly int _modifierId;

        private float _curveDuration;
        private float _startMultiplier;
        private float _endMultiplier;
        private AnimationCurve _curve;

        private float _curveStartedAt;
        private bool _cleared;

        public SpeedCurveEffect(
            PlayerMovementController controller,
            float duration,
            float startMultiplier,
            float endMultiplier,
            AnimationCurve curve,
            int modifierId,
            bool unique)
        {
            _controller = controller;
            _duration = Mathf.Max(0f, duration);
            _curveDuration = Mathf.Max(0.01f, duration);

            _startMultiplier = Mathf.Max(0.01f, startMultiplier);
            _endMultiplier = Mathf.Max(0.01f, endMultiplier);
            _curve = curve;

            _modifierId = modifierId;
            IsUniqueEffect = unique;
        }

        public override void ApplyEffect()
        {
            if (_controller == null)
                return;

            _curveStartedAt = Time.time;
            _controller.SetSpeedModifier(_modifierId, EvaluateCurrentMultiplier());
        }

        public override void Update()
        {
            if (_controller == null)
                return;

            _controller.SetSpeedModifier(_modifierId, EvaluateCurrentMultiplier());
        }

        public override void ClearEffect()
        {
            if (_cleared)
                return;

            _cleared = true;

            if (_controller == null)
                return;

            _controller.RemoveSpeedModifier(_modifierId);
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not SpeedCurveEffect reapplied)
                return;

            _startMultiplier = reapplied._startMultiplier;
            _endMultiplier = reapplied._endMultiplier;
            _curve = reapplied._curve;
            _curveDuration = reapplied._curveDuration;

            _curveStartedAt = Time.time;

            _duration = (Time.time - StarteAt) + reapplied._curveDuration;

            if (_controller != null)
                _controller.SetSpeedModifier(_modifierId, EvaluateCurrentMultiplier());
        }

        private float EvaluateCurrentMultiplier()
        {
            float elapsed = Mathf.Max(0f, Time.time - _curveStartedAt);
            float normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _curveDuration));

            float curveValue = EvaluateCurve(normalizedTime);

            return Mathf.Lerp(
                _startMultiplier,
                _endMultiplier,
                curveValue);
        }

        private float EvaluateCurve(float normalizedTime)
        {
            if (_curve == null || _curve.length == 0)
                return normalizedTime;

            return Mathf.Clamp01(_curve.Evaluate(normalizedTime));
        }
    }
}