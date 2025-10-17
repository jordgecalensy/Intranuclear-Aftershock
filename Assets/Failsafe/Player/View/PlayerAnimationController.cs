using Cysharp.Threading.Tasks;
using Failsafe.Player.View;
using Failsafe.PlayerMovements;
using Failsafe.PlayerMovements.States;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Player
{
    public class PlayerAnimationController : IInitializable, ITickable, IDisposable
    {
        private readonly PlayerController _playerController;
        private readonly PlayerHandsSystem _playerHandsSystem;
        private readonly Animator _animator;
        private readonly Transform _payerTransform;
        private readonly int _upperBodyLayerId;
        private int _upperBodyActive;

        private float _movementDumpTime = 0.2f;
        private int _xMovementId = Animator.StringToHash("XMovement");
        private int _zMovementId = Animator.StringToHash("ZMovement");
        private int _standingId = Animator.StringToHash("Standing");
        private int _walkingId = Animator.StringToHash("Walking");
        private int _runningId = Animator.StringToHash("Running");
        private int _crouchingId = Animator.StringToHash("Crouching");
        private int _fallingId = Animator.StringToHash("Falling");
        private int _disabledId = Animator.StringToHash("Disabled");
        private int _groundedId = Animator.StringToHash("Grounded");
        private int _jumpId = Animator.StringToHash("Jump");
        private int _slidingId = Animator.StringToHash("Sliding");
        private int _healId = Animator.StringToHash("Heal");
        private int _deadId = Animator.StringToHash("Dead");

        // --- НОВОЕ: настройки синхронизации скорости анимации ---
        private const bool  AnimFollowMoveMultiplier = true; // включить/выключить привязку
        private const float AnimMinSpeed = 0.20f;            // не давать анимации «умирать»
        private const float AnimMaxSpeed = 1.00f;            // верхний предел для клипов
        private const float AnimSmooth   = 12f;              // эксп. сглаживание (чем больше — быстрее)
        private float _defaultAnimSpeed = 1f;                // чтобы вернуть при Dispose
        // --------------------------------------------------------

        public PlayerAnimationController(PlayerController playerController, PlayerView playerView, PlayerHandsSystem playerHandsSystem)
        {
            _playerController = playerController;
            _animator = playerView.Animator;
            _payerTransform = playerView.PlayerTransform;
            _playerHandsSystem = playerHandsSystem;

            _upperBodyLayerId = _animator.GetLayerIndex("UpperBody");
            if (_animator != null) _defaultAnimSpeed = _animator.speed;
        }

        public void Tick()
        {
            var playerVelocity = _payerTransform.InverseTransformVector(_playerController.PlayerMovementController.Velocity);
            var velocityXZ = new Vector3(playerVelocity.x, 0, playerVelocity.z);
            if (velocityXZ.Equals(Vector3.zero))
            {
                _animator.SetFloat(_xMovementId, 0, _movementDumpTime, Time.deltaTime);
                _animator.SetFloat(_zMovementId, 0, _movementDumpTime, Time.deltaTime);
            }
            else
            {
                velocityXZ.Normalize();
                _animator.SetFloat(_xMovementId, velocityXZ.x, _movementDumpTime, Time.deltaTime);
                _animator.SetFloat(_zMovementId, velocityXZ.z, _movementDumpTime, Time.deltaTime);
            }

            _animator.SetBool(_standingId, _playerController.StateMachine.CurrentState is StandingState);
            _animator.SetBool(_walkingId, _playerController.StateMachine.CurrentState is WalkState || _playerController.StateMachine.CurrentState is StandingState);
            _animator.SetBool(_runningId, _playerController.StateMachine.CurrentState is SprintState);
            _animator.SetBool(_crouchingId, _playerController.StateMachine.CurrentState is CrouchState || _playerController.StateMachine.CurrentState is CrouchIdle);
            _animator.SetBool(_fallingId, _playerController.StateMachine.CurrentState is FallState);
            _animator.SetBool(_groundedId, _playerController.PlayerMovementController.IsGrounded);
            _animator.SetBool(_slidingId, _playerController.StateMachine.CurrentState is SlideState);
            _animator.SetBool(_deadId, _playerController.StateMachine.CurrentState is DeathState );

            // --- НОВОЕ: синхронизация Animator.speed c множителем движения ---
            if (AnimFollowMoveMultiplier && _animator != null)
            {
                float mul = _playerController.PlayerMovementController.CurrentSpeedMultiplier;
                // можно дополнительно вырубать ускорение в не-локомоушн состояниях, если нужно:
                // bool isLocomotion = _playerController.StateMachine.CurrentState is WalkState || _playerController.StateMachine.CurrentState is SprintState || _playerController.StateMachine.CurrentState is CrouchState || _playerController.StateMachine.CurrentState is CrouchIdle;
                // if (!isLocomotion) mul = 1f;

                float target = Mathf.Clamp(mul, AnimMinSpeed, AnimMaxSpeed);
                float k = 1f - Mathf.Exp(-AnimSmooth * Time.deltaTime); // эксп. сглаживание
                _animator.speed = Mathf.Lerp(_animator.speed, target, k);
            }
            // ---------------------------------------------------------------
        }

        public void Initialize()
        {
            _playerController.StateMachine.GetState<JumpState>().OnEnter += OnStartJumping;
            _playerController.StateMachine.GetState<JumpState>().OnExit += OnFinishJumping;
            _playerHandsSystem.OnItemStartUsing += OnUseItem;
        }

        public void Dispose()
        {
            _playerController.StateMachine.GetState<JumpState>().OnEnter -= OnStartJumping;
            _playerController.StateMachine.GetState<JumpState>().OnExit -= OnFinishJumping;
            _playerHandsSystem.OnItemStartUsing -= OnUseItem;

            // вернуть стандартную скорость анимации
            if (_animator != null) _animator.speed = _defaultAnimSpeed;
        }

        public void OnStartJumping()
        {
            _animator.SetTrigger(_jumpId);
        }

        public void OnFinishJumping()
        {
            _animator.ResetTrigger(_jumpId);
        }

        public void OnUseItem(ItemType itemType)
        {
            _animator.SetTrigger(_healId);
            ActivateLayerForSeconds(_upperBodyLayerId, 2.5f).Forget();
        }

        private async UniTask ActivateLayerForSeconds(int layerId, float seconds)
        {
            _upperBodyActive++;
            _animator.SetLayerWeight(layerId, 1);
            await UniTask.Delay(TimeSpan.FromSeconds(seconds));
            _upperBodyActive--;
            if (_upperBodyActive == 0)
            {
                _animator.SetLayerWeight(layerId, 0);
            }
        }
    }
}