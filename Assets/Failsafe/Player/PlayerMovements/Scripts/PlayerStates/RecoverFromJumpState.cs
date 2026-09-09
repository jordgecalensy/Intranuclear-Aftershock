using UnityEngine;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.EffectSystem;

namespace Failsafe.PlayerMovements.States
{
    public class RecoverFromJumpState : BehaviorState
    {
        private readonly Animator _animator;
        private readonly CharacterController _characterController;
        private readonly PlayerMovementController _pmc;
        private readonly PlayerMovementParameters _p;
        private readonly IEffectApplicationService _effects;
        private readonly GameplayEffectCatalog _effectCatalog;

        private readonly int _landingRecoverId = Animator.StringToHash("LandingRecover");
        private readonly int _recoveringId = Animator.StringToHash("Recovering");

        private float _timer;
        private bool _slowApplied;

        public bool IsFinished => _timer >= _p.LandingRecoverDuration;

        public RecoverFromJumpState(
            Animator animator,
            CharacterController characterController,
            PlayerMovementController pmc,
            PlayerMovementParameters parameters,
            IEffectApplicationService effects,
            GameplayEffectCatalog effectCatalog)
        {
            _animator = animator;
            _characterController = characterController;
            _pmc = pmc;
            _p = parameters;
            _effects = effects;
            _effectCatalog = effectCatalog;
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

                var context = new EffectContext(
                    _characterController.gameObject,
                    _characterController,
                    _characterController.bounds.center,
                    Vector3.up,
                    _characterController.transform.forward,
                    _p.MainSlowMultiplier,
                    _characterController.gameObject,
                    _p.MainSlowDuration);

                _effects.Apply(_effectCatalog.LandingSlow, context);
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
