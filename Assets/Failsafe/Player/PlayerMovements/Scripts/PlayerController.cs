using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.PlayerMovements.States;
using Failsafe.Scripts.Health;
using TMPro;
using VContainer;
using Failsafe.Player.View;
using VContainer.Unity;
using Failsafe.Player.Model;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Items;

namespace Failsafe.PlayerMovements
{
    public class PlayerController : IInitializable, ITickable, IFixedTickable, IDisposable
    {
        private readonly PlayerMovementParameters _movementParametrs;
        private readonly PlayerNoiseParameters _noiseParametrs;
        private readonly SignalManager _signalManager;
        private readonly InputHandler _inputHandler;
        private readonly PlayerView _playerView;
        private readonly IRestorableHealth _health;
        private readonly IStamina _stamina;
        private readonly PlayerStaminaController _playerStaminaController;
        private readonly IEffectManager _effectManager;
        private readonly PlayerMovementController _movementController;
        private readonly PlayerControlBlocker _controlBlocker;
        private readonly PlayerRuntimeParameters _runtimeParameters;
        
        private PlayerRotationController _playerRotationController;
        private PlayerBodyController _playerBodyController;
        private BehaviorStateMachine _behaviorStateMachine;
        private PlayerLedgeController _ledgeController;
        private PlayerNoiseController _noiseController;
        private StepController _stepController;
        private MovementCameraShakeProvider _movementShakeProvider;

        private bool _isLowHpEffectActive = false;
        private bool _isVisorEffectActive = false;
        private float _prevHealth;

        private readonly HashSet<int> _controlLocks = new();

        public BehaviorStateMachine StateMachine => _behaviorStateMachine;
        public PlayerMovementController PlayerMovementController => _movementController;
        public PlayerRotationController PlayerRotationController => _playerRotationController;
        public bool IsControlLocked => _controlLocks.Count > 0;

        public PlayerController(
            PlayerMovementParameters movementParametrs,
            PlayerNoiseParameters noiseParametrs,
            SignalManager signalManager,
            InputHandler inputHandler,
            PlayerView playerView,
            IRestorableHealth health,
            IStamina stamina,
            PlayerStaminaController playerStaminaController,
            IEffectManager effectManager,
            PlayerMovementController movementController,
            PlayerControlBlocker controlBlocker,
            PlayerRuntimeParameters runtimeParameters)
        {
            _movementParametrs = movementParametrs;
            _noiseParametrs = noiseParametrs;
            _signalManager = signalManager;
            _inputHandler = inputHandler;
            _playerView = playerView;
            _health = health;
            _stamina = stamina;
            _playerStaminaController = playerStaminaController;
            _effectManager = effectManager;
            _movementController = movementController;
            _controlBlocker = controlBlocker;
            _runtimeParameters = runtimeParameters;
        }

        public void SetControlLock(int lockId, bool locked)
        {
            if (locked)
                _controlLocks.Add(lockId);
            else
                _controlLocks.Remove(lockId);
        }

        public void Initialize()
        {
            _playerRotationController = new PlayerRotationController(
                _playerView.PlayerTransform,
                _playerView.PlayerRigHead,
                _inputHandler);

            _playerBodyController = new PlayerBodyController(_playerView.CharacterController);

            _ledgeController = new PlayerLedgeController(
                _playerView.PlayerTransform,
                _playerView.PlayerCamera,
                _playerView.PlayerGrabPoint,
                _movementParametrs);

            _noiseController = new PlayerNoiseController(
                _playerView.PlayerTransform,
                _noiseParametrs,
                _signalManager,
                _runtimeParameters);

            _stepController = new StepController(
                _playerView.CharacterController,
                _movementParametrs,
                _playerView.FootstepEvent);

            _prevHealth = _health.CurrentHealth;

            _movementShakeProvider = new MovementCameraShakeProvider(
                _inputHandler,
                _effectManager,
                _playerRotationController);

            _health.OnHealthChanged += HandleHealthChanged;
            _health.OnDeath += HandleDeath;
            _health.OnStateRestored += HandleHealthStateRestored;
            EarthquakeTrigger.OnEarthquakeStarted += HandleEarthquake;

            InitializeStateMachine();
        }

        public void Dispose()
        {
            _health.OnHealthChanged -= HandleHealthChanged;
            _health.OnDeath -= HandleDeath;
            _health.OnStateRestored -= HandleHealthStateRestored;
            EarthquakeTrigger.OnEarthquakeStarted -= HandleEarthquake;
        }

        private void HandleHealthChanged(float newValue)
        {
            float intensity = 0;
            float duration = 0;
            float frequency = 0;

            float damage = _prevHealth - newValue;

            if (damage <= 0f)
            {
                _prevHealth = newValue;
                return;
            }

            switch (damage)
            {
                case >= 30f:
                    intensity = 3.5f;
                    duration = 0.6f;
                    frequency = 8f;
                    break;

                case >= 15f:
                    intensity = 1.1f;
                    duration = 0.4f;
                    frequency = 18f;
                    break;

                case >= 1f:
                    intensity = 0.45f;
                    duration = 0.25f;
                    frequency = 20f;
                    break;

                default:
                    intensity = 0.3f;
                    duration = 0.2f;
                    frequency = 20f;
                    break;
            }

            _effectManager.ApplyEffect(new CameraShakeEffect(
                _playerRotationController,
                intensity,
                duration,
                frequency));

            _effectManager.ApplyEffect(new DamageHitEffect(0.25f));

            _prevHealth = newValue;
        }

        private void HandleHealthStateRestored(float restoredHealth)
        {
            _prevHealth = restoredHealth;
        }

        private void HandleDeath()
        {
            _behaviorStateMachine?.ForseChangeState<DeathState>();
        }

        private void HandleEarthquake(float strength, float duration)
        {
            float intensity = 0;
            float shakeDuration = 0;
            float frequency = 0;
            float shakeFadeInDuration = 0;
            float shakeFadeOutDuration = 0;

            float slowMultiplier = 1f;
            float slowDuration = 0;
            float slowFadeInDuration = 0;
            float slowFadeOutDuration = 0;

            if (strength > 0)
                switch (strength)
                {
                    case >= 3:
                        intensity = 4.0f;
                        shakeDuration = 3.5f;
                        frequency = 6f;
                        slowMultiplier = 0.45f;
                        slowDuration = 3f;
                        break;

                    case >= 2:
                        intensity = 2.5f;
                        shakeDuration = 3f;
                        frequency = 8f;
                        slowMultiplier = 0.60f;
                        slowDuration = 2.5f;
                        break;

                    case >= 1:
                        intensity = 1.2f;
                        shakeDuration = 5f;
                        frequency = 10f;
                        slowMultiplier = 0.75f;
                        slowDuration = 5f;
                        break;

                    default:
                        intensity = 0.6f;
                        shakeDuration = 0.5f;
                        frequency = 12f;
                        slowMultiplier = 0.90f;
                        slowDuration = 1.5f;
                        break;
                }

            shakeDuration = Mathf.Max(shakeDuration, duration);
            slowDuration = Mathf.Max(slowDuration, duration);

            shakeFadeInDuration = Mathf.Min(0.45f, shakeDuration * 0.2f);
            shakeFadeOutDuration = Mathf.Min(1.2f, shakeDuration * 0.35f);

            slowFadeInDuration = Mathf.Min(0.6f, slowDuration * 0.2f);
            slowFadeOutDuration = Mathf.Min(1.4f, slowDuration * 0.35f);

            _effectManager.ApplyEffect(
                new CameraShakeEffect(
                    _playerRotationController,
                    intensity,
                    shakeDuration,
                    frequency,
                    shakeFadeInDuration,
                    shakeFadeOutDuration));

            _effectManager.ApplyEffect(
                new EarthquakeMovementSlowEffect(
                    _movementController,
                    slowMultiplier,
                    slowDuration,
                    slowFadeInDuration,
                    slowFadeOutDuration));
        }

        private void InitializeStateMachine()
        {
            var deathState = new DeathState(
                _playerView.Animator,
                _controlBlocker,
                _inputHandler,
                _movementController);

            var forcedStates = new List<BehaviorForcedState>
            {
                deathState
            };

            _behaviorStateMachine = new BehaviorStateMachine(forcedStates);

            var standingState = new StandingState(_inputHandler, _movementController, _playerRotationController);
            var walkState = new WalkState(_inputHandler, _movementController, _movementParametrs, _noiseController, _stepController);
            var runState = new SprintState(_inputHandler, _movementController, _movementParametrs, _noiseController, _stepController, _playerStaminaController);
            var slideState = new SlideState(_inputHandler, _movementController, _movementParametrs, _playerBodyController, _playerRotationController);
            var crouchState = new CrouchState(_inputHandler, _movementController, _movementParametrs, _playerBodyController, _noiseController, _stepController);
            var slantState = new SlantState(_inputHandler, _movementController, _movementParametrs, _playerBodyController, _playerRotationController, _noiseController, _stepController, _playerView.CharacterController);
            var jumpState = new JumpState(_inputHandler, _playerView.CharacterController, _movementController, _movementParametrs, _playerStaminaController);
            var fallState = new FallState(_inputHandler, _playerView.CharacterController, _movementController, _movementParametrs, _noiseController, _effectManager, _health);
            var grabLedgeState = new GrabLedgeState(_inputHandler, _playerView.CharacterController, _movementController, _movementParametrs, _playerRotationController, _ledgeController);
            var climbingUpState = new ClimbingUpState(_inputHandler, _playerView.CharacterController, _movementController, _movementParametrs, _ledgeController);
            var climbingOnState = new ClimbingOnState(_inputHandler, _playerView.CharacterController, _movementController, _movementParametrs, _ledgeController);
            var climbingOverState = new ClimbingOverState(_inputHandler, _playerView.CharacterController, _movementController, _movementParametrs, _ledgeController);
            var ledgeJumpState = new LedgeJumpState(_inputHandler, _playerView.CharacterController, _movementParametrs, _playerView.PlayerCamera);
            var crouchIdleState = new CrouchIdle(_playerBodyController, _movementController, _movementParametrs, _noiseController, _stepController, _playerRotationController);
            var recoverState = new RecoverFromJumpState(_playerView.Animator, _movementController, _movementParametrs, _effectManager);
            var blockState = new BlockState(_movementController);

            Func<bool> runStatePrecondition = () => _inputHandler.MoveForward && _inputHandler.SprintTriggered && !_stamina.IsEmpty;
            Func<bool> jumpStatePrecondition = () => _inputHandler.JumpTriggered && !_stamina.IsEmpty && _movementController.IsGroundedFor(0.1f);
            Func<bool> slantStatePrecondition = () => _inputHandler.SlantLeftTrigger || _inputHandler.SlantRightTrigger;

            standingState.AddTransition(walkState, () => !_inputHandler.MovementInput.Equals(Vector2.zero));
            standingState.AddTransition(slantState, () => slantStatePrecondition());
            standingState.AddTransition(blockState, () => PlayerScreenScript.IsCameraFullScreen);
            standingState.AddTransition(crouchIdleState, () => _inputHandler.CrouchTrigger.IsTriggered, _inputHandler.CrouchTrigger.ReleaseTrigger);
            standingState.AddTransition(climbingOverState, () => _inputHandler.JumpTriggered && _ledgeController.CanClimbOverLedge());
            standingState.AddTransition(climbingOnState, () => _inputHandler.JumpTriggered && _ledgeController.CanClimbOnLedge());
            standingState.AddTransition(jumpState, () => jumpStatePrecondition());

            walkState.AddTransition(runState, () => runStatePrecondition());
            walkState.AddTransition(climbingOverState, () => _inputHandler.JumpTriggered && _ledgeController.CanClimbOverLedge());
            walkState.AddTransition(climbingOnState, () => _inputHandler.JumpTriggered && _ledgeController.CanClimbOnLedge());
            walkState.AddTransition(jumpState, () => jumpStatePrecondition());
            walkState.AddTransition(crouchState, () => _inputHandler.CrouchTrigger.IsTriggered, _inputHandler.CrouchTrigger.ReleaseTrigger);
            walkState.AddTransition(slantState, () => slantStatePrecondition());
            walkState.AddTransition(fallState, () => _movementController.IsFalling);
            walkState.AddTransition(standingState, () => _inputHandler.MovementInput.Equals(Vector2.zero));
            walkState.AddTransition(blockState, () => PlayerScreenScript.IsCameraFullScreen);

            runState.AddTransition(walkState, () => !runStatePrecondition());
            runState.AddTransition(blockState, () => PlayerScreenScript.IsCameraFullScreen);
            runState.AddTransition(climbingOverState, () => _inputHandler.JumpTriggered && _ledgeController.CanClimbOverLedge());
            runState.AddTransition(climbingOnState, () => _inputHandler.JumpTriggered && _ledgeController.CanClimbOnLedge());
            runState.AddTransition(jumpState, () => jumpStatePrecondition());
            runState.AddTransition(slideState, () => _inputHandler.CrouchTrigger.IsTriggered && runState.CanSlide(), _inputHandler.CrouchTrigger.ReleaseTrigger);
            runState.AddTransition(fallState, () => _movementController.IsFalling);

            slideState.AddTransition(crouchState, () => slideState.SlideFinished() || (slideState.CanFinish() && _inputHandler.MoveBack));
            slideState.AddTransition(walkState, () => _inputHandler.CrouchTrigger.IsTriggered && slideState.CanFinish() && _playerBodyController.CanStand(), _inputHandler.CrouchTrigger.ReleaseTrigger);
            slideState.AddTransition(fallState, () => _movementController.IsFalling);

            crouchState.AddTransition(runState, () => runStatePrecondition() && _playerBodyController.CanStand());
            crouchState.AddTransition(walkState, () => _inputHandler.CrouchTrigger.IsTriggered && _playerBodyController.CanStand(), _inputHandler.CrouchTrigger.ReleaseTrigger);
            crouchState.AddTransition(slantState, () => slantStatePrecondition());
            crouchState.AddTransition(blockState, () => PlayerScreenScript.IsCameraFullScreen);
            crouchState.AddTransition(fallState, () => _movementController.IsFalling);
            crouchState.AddTransition(crouchIdleState, () => _inputHandler.MovementInput.Equals(Vector2.zero));
            crouchState.AddTransition(jumpState, () => jumpStatePrecondition());

            slantState.AddTransition(runState, () => slantState.IsWalkSlant && runStatePrecondition());
            slantState.AddTransition(climbingOverState, () => slantState.IsWalkSlant && _inputHandler.JumpTriggered && _ledgeController.CanClimbOverLedge());
            slantState.AddTransition(climbingOnState, () => slantState.IsWalkSlant && _inputHandler.JumpTriggered && _ledgeController.CanClimbOnLedge());
            slantState.AddTransition(jumpState, () => jumpStatePrecondition());
            slantState.AddTransition(crouchState, () => _inputHandler.CrouchTrigger.IsTriggered, _inputHandler.CrouchTrigger.ReleaseTrigger);
            slantState.AddTransition(fallState, () => _movementController.IsFalling);
            slantState.AddTransition(crouchIdleState, () => slantState.IsCrouchedSlant && _inputHandler.MovementInput.Equals(Vector2.zero) && !slantStatePrecondition() && slantState.CanExitSlant());
            slantState.AddTransition(standingState, () => slantState.IsWalkSlant && _inputHandler.MovementInput.Equals(Vector2.zero) && !slantStatePrecondition() && slantState.CanExitSlant());
            slantState.AddTransition(crouchState, () => slantState.IsCrouchedSlant && !slantStatePrecondition() && slantState.CanExitSlant());
            slantState.AddTransition(walkState, () => slantState.IsWalkSlant && !slantStatePrecondition() && slantState.CanExitSlant());
            slantState.AddTransition(blockState, () => PlayerScreenScript.IsCameraFullScreen);

            crouchIdleState.AddTransition(crouchState, () => !_inputHandler.MovementInput.Equals(Vector2.zero));
            crouchIdleState.AddTransition(slantState, () => slantStatePrecondition());
            crouchIdleState.AddTransition(blockState, () => PlayerScreenScript.IsCameraFullScreen);
            crouchIdleState.AddTransition(standingState, () => _inputHandler.CrouchTrigger.IsTriggered && _playerBodyController.CanStand(), _inputHandler.CrouchTrigger.ReleaseTrigger);
            crouchIdleState.AddTransition(jumpState, () => jumpStatePrecondition());

            blockState.AddTransition(walkState, () => !PlayerScreenScript.IsCameraFullScreen);

            jumpState.AddTransition(runState, () => runStatePrecondition() && jumpState.CanGround() && _movementController.IsGrounded);
            jumpState.AddTransition(walkState, () => jumpState.CanGround() && _movementController.IsGrounded);
            jumpState.AddTransition(fallState, jumpState.InHightPoint);
            jumpState.AddTransition(grabLedgeState, () => _inputHandler.GrabLedgeTrigger.IsTriggered && _ledgeController.CanGrabToLedgeGrabPointInView());

            fallState.AddTransition(walkState, () => _movementController.IsGrounded && !fallState.ShouldRecover);
            fallState.AddTransition(grabLedgeState, () => _inputHandler.GrabLedgeTrigger.IsTriggered && _ledgeController.CanGrabToLedgeGrabPointInView());

            grabLedgeState.AddTransition(fallState, () => _inputHandler.MoveBack && grabLedgeState.CanFinish());
            grabLedgeState.AddTransition(climbingUpState, () => _inputHandler.MoveForward && grabLedgeState.CanFinish() && climbingUpState.CanClimb());
            grabLedgeState.AddTransition(ledgeJumpState, () => _inputHandler.JumpTriggered && grabLedgeState.CanFinish());

            ledgeJumpState.AddTransition(grabLedgeState, () => _ledgeController.CanGrabToLedgeGrabPointInView());
            ledgeJumpState.AddTransition(fallState, ledgeJumpState.InHightPoint);

            climbingUpState.AddTransition(walkState, () => climbingUpState.ClimbFinish());
            climbingOnState.AddTransition(walkState, () => climbingOnState.ClimbFinish());
            climbingOverState.AddTransition(fallState, () => climbingOverState.ClimbFinish());

            fallState.AddTransition(recoverState, () => fallState.ShouldRecover);
            recoverState.AddTransition(standingState, () => recoverState.IsFinished);

            _behaviorStateMachine.SetInitState(walkState);
        }

       public void Tick()
{
    if (_health.IsDead)
    {
        HandleDeath();
        return;
    }

    bool lookBlocked =
        _controlBlocker != null &&
        _controlBlocker.IsBlocked(PlayerControlBlock.Look);

    bool movementBlocked =
        _controlBlocker != null &&
        _controlBlocker.IsBlocked(PlayerControlBlock.Movement);

    bool visorBlocked =
        _controlBlocker != null &&
        _controlBlocker.IsBlocked(PlayerControlBlock.Visor);

    bool canProcessLook =
        !PlayerScreenScript.IsCameraFullScreen &&
        !lookBlocked;

    bool canProcessMovementState =
        !movementBlocked;

    if (canProcessLook)
    {
        _movementShakeProvider.Tick();
        _ledgeController.HandleFindingLedge();
        _playerRotationController.HandlePlayerRotation();
    }

    if (canProcessMovementState)
    {
        _behaviorStateMachine.Update();
    }
    else
    {
        _movementController.Move(Vector3.zero);
        _movementController.SetGravity(Vector3.zero);
    }

    _stepController.Update();

    float currentHpPercent = _health.CurrentHealth / _health.MaxHealth;

    if (currentHpPercent <= 0.2f && !_isLowHpEffectActive)
    {
        _effectManager.ApplyEffect(new LowHealthEffect());
        _isLowHpEffectActive = true;
    }
    else if (currentHpPercent > 0.2f && _isLowHpEffectActive)
    {
        _effectManager.RemoveEffect<LowHealthEffect>();
        _isLowHpEffectActive = false;
    }

    if (!visorBlocked && _inputHandler.VisorTrigger.IsTriggered)
    {
        if (!_isVisorEffectActive)
        {
            Debug.Log("Visor включен");
            _effectManager.ApplyEffect(new VisorEffect(_playerView.PlayerTransform));
            _isVisorEffectActive = true;
        }
        else
        {
            Debug.Log("Visor выключен");
            _effectManager.RemoveEffect<VisorEffect>();
            _isVisorEffectActive = false;
        }

        _inputHandler.VisorTrigger.ReleaseTrigger();
    }
}

        public void FixedTick()
        {
            _movementController.HandleMovement();
            _movementController.CheckGrounded();
        }
    }
}
