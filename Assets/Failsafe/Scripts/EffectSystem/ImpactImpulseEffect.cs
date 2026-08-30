using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class ImpactImpulseEffect : Effect
    {
        private readonly IImpactImpulseReceiver _receiver;
        private readonly Rigidbody _rigidbody;

        private readonly Vector3 _impulse;
        private readonly Vector3 _impactPoint;
        private readonly GameObject _source;
        private readonly ForceMode _forceMode;
        private readonly bool _applyAtImpactPoint;
        private readonly bool _log;

        public ImpactImpulseEffect(
            IImpactImpulseReceiver receiver,
            Rigidbody rigidbody,
            Vector3 impulse,
            Vector3 impactPoint,
            GameObject source,
            ForceMode forceMode,
            bool applyAtImpactPoint,
            bool log)
        {
            _receiver = receiver;
            _rigidbody = rigidbody;
            _impulse = impulse;
            _impactPoint = impactPoint;
            _source = source;
            _forceMode = forceMode;
            _applyAtImpactPoint = applyAtImpactPoint;
            _log = log;

            _duration = 0f;
            IsUniqueEffect = false;
        }

        public override void ApplyEffect()
        {
            if (_receiver != null)
            {
                _receiver.AddImpactImpulse(
                    _impulse,
                    _impactPoint,
                    _source);

                if (_log)
                    EffectLog.Info(EffectLog.Physics, $"[ImpactImpulseEffect] Applied receiver impulse {_impulse}");

                return;
            }

            if (_rigidbody == null)
                return;

            _rigidbody.WakeUp();

            if (_applyAtImpactPoint)
            {
                _rigidbody.AddForceAtPosition(
                    _impulse,
                    _impactPoint,
                    _forceMode);
            }
            else
            {
                _rigidbody.AddForce(
                    _impulse,
                    _forceMode);
            }

            if (_log)
            {
                EffectLog.Info(EffectLog.Physics,
                    $"[ImpactImpulseEffect] Applied Rigidbody impulse {_impulse} at {_impactPoint}",
                    _rigidbody);
            }
        }

        public override void ClearEffect()
        {
        }
    }
}