using UnityEngine;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.EffectSystem.Effects;

namespace Failsafe.PlayerMovements.States
{
    public class RecoverFromJumpState : BehaviorState
    {
        private readonly Animator _animator;
        private readonly PlayerMovementController _pmc;
        private readonly PlayerMovementParameters _p;
        private readonly IEffectManager _effects;

        private readonly int _landingRecoverId = Animator.StringToHash("LandingRecover");
        private readonly int _recoveringId = Animator.StringToHash("Recovering");

        private float _timer;
        private bool _slowApplied;

        public bool IsFinished => _timer >= _p.LandingRecoverDuration;

        public RecoverFromJumpState(
            Animator animator,
            PlayerMovementController pmc,
            PlayerMovementParameters parameters,
            IEffectManager effects)
        {
            _animator = animator;
            _pmc = pmc;
            _p = parameters;
            _effects = effects;
        }

        public override void Enter()
        {
            _timer = 0f;
            _slowApplied = false;

            _pmc.Move(Vector3.zero);
            _pmc.SetGravityDefault();

            if (_animator != null)
            {
                _animator.SetBool(_landingRecoverId, true);
                _animator.SetBool(_recoveringId, true);
            }
        }

        public override void Update()
        {
            _pmc.Move(Vector3.zero);
            _pmc.SetGravityDefault();

            _timer += Time.deltaTime;

            if (!_slowApplied && _timer >= _p.LandingRecoverDuration)
            {
                _slowApplied = true;

                var slow = new SpeedMultiplierEffect(
                    _pmc,
                    _p.MainSlowDuration,
                    _p.MainSlowMultiplier,
                    SpeedStackPolicy.Strongest);

                _effects.ApplyEffect(slow);
            }
        }

        public override void Exit()
        {
            if (_animator != null)
            {
                _animator.SetBool(_landingRecoverId, false);
                _animator.SetBool(_recoveringId, false);
            }
        }
    }
}