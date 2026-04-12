using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.PlayerMovements.States;
using Failsafe.Scripts.Health;
using FMODUnity;
using TMPro;
using VContainer;
using Failsafe.Player.View;
using VContainer.Unity;
using Failsafe.Player.Model;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Items; // ← добавь это



namespace Failsafe.PlayerMovements
{
    /// <summary>
    /// Движение персонажа
    /// </summary>
    public class PlayerController : IInitializable, ITickable, IFixedTickable
    {
        private readonly PlayerMovementParameters _movementParametrs;
        private readonly PlayerNoiseParameters _noiseParametrs;
        private readonly SignalManager _signalManager;
        private readonly InputHandler _inputHandler;
        private readonly PlayerView _playerView;
        private readonly IHealth _health;
        private readonly IStamina _stamina;
        private readonly PlayerStaminaController _playerStaminaController;
        private readonly IEffectManager _effectManager;
        private PlayerRotationController _playerRotationController;
        private PlayerBodyController _playerBodyController;
        private BehaviorStateMachine _behaviorStateMachine;
        private PlayerLedgeController _ledgeController;
        private PlayerNoiseController _noiseController;
        private StepController _stepController;
        private bool _isLowHpEffectActive = false;
        private bool _isVisorEffectActive = false;
        private MovementCameraShakeProvider _movementShakeProvider;
        private float _prevHealth;

        public BehaviorStateMachine StateMachine => _behaviorStateMachine;
        public PlayerMovementController PlayerMovementController => _movementController;
        public PlayerRotationController PlayerRotationController => _playerRotationController;

        [Inject] private readonly PlayerMovementController _movementController; // readonly и инжектим

        public PlayerController(
            PlayerMovementParameters movementParametrs,
            PlayerNoiseParameters noiseParametrs,
            SignalManager signalManager,
            InputHandler inputHandler,
            PlayerView playerView,
            IHealth health,
            IStamina stamina,
            PlayerStaminaController playerStaminaController,
            IEffectManager effectManager,
            PlayerMovementController movementController    // <-- ДОБАВЬ ЭТО
        )
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
            _movementController = movementController;      // <-- ИСПОЛЬЗУЕМ DI-ЭКЗЕМПЛЯР
        }

        public void Initialize()
        {
            _playerRotationController = new PlayerRotationController(_playerView.PlayerTransform, _playerView.PlayerRigHead, _inputHandler);
            _playerBodyController = new PlayerBodyController(_playerView.CharacterController);
            _ledgeController = new PlayerLedgeController(_playerView.PlayerTransform, _playerView.PlayerCamera, _playerView.PlayerGrabPoint, _movementParametrs);
            _noiseController = new PlayerNoiseController(_playerView.PlayerTransform, _noiseParametrs, _signalManager);
            _stepController = new StepController(_playerView.CharacterController, _movementParametrs, _playerView.FootstepEvent);
            _prevHealth = _health.CurrentHealth;
            _movementShakeProvider =
                new MovementCameraShakeProvider(_inputHandler, _effectManager, _playerRotationController);

            _health.OnHealthChanged += HandleHealthChanged;
            EarthquakeTrigger.OnEarthquakeStarted += HandleEarthquake;     


            InitializeStateMachine();

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
                    intensity = 3.5f; duration = 0.6f; frequency = 8f;
                    break;

                case >= 15f:
                    intensity = 1.1f; duration = 0.4f; frequency = 18f;
                    break;

                case >= 1f:
                    intensity = 0.45f; duration = 0.25f; frequency = 20f;
                    break;

                default:
                    // Любой периодический мелкий урон тоже должен заметно проигрывать shake.
                    intensity = 0.3f; duration = 0.2f; frequency = 20f;
                    break;
            }

            _effectManager.ApplyEffect(new CameraShakeEffect(_playerRotationController, intensity, duration, frequency));
            _effectManager.ApplyEffect(new DamageHitEffect(0.25f));

            _prevHealth = newValue;
        }

        private void HandleEarthquake(float strength, float duration)
        {
            float intensity = 0;
            float shakeDuration = 0;
            float frequency = 0;
            // Время, за которое тряска камеры плавно выйдет на полную силу.
            float shakeFadeInDuration = 0;
            // Время, за которое тряска камеры плавно затухнет к концу эффекта.
            float shakeFadeOutDuration = 0;

            float slowMultiplier = 1f;
            float slowDuration = 0;
            // Время, за которое замедление плавно включится.
            float slowFadeInDuration = 0;
            // Время, за которое замедление плавно отключится.
            float slowFadeOutDuration = 0;

            if (strength > 0)
                switch (strength)
                {
                    case >= 3:
                        intensity = 4.0f; shakeDuration = 3.5f; frequency = 6f;
                        slowMultiplier = 0.45f; slowDuration = 3f;
                        break;

                    case >= 2:
                        intensity = 2.5f; shakeDuration = 3f; frequency = 8f;
                        slowMultiplier = 0.60f; slowDuration = 2.5f;
                        break;

                    case >= 1:
                        intensity = 1.2f; shakeDuration = 5f; frequency = 10f;
                        slowMultiplier = 0.75f; slowDuration = 5f;
                        break;

                    default:
                        intensity = 0.6f; shakeDuration = 0.5f; frequency = 12f;
                        slowMultiplier = 0.90f; slowDuration = 1.5f;
                        break;
                }

            // Если в событии передали duration больше пресета, используем его как минимальную длительность эффекта.
            shakeDuration = Mathf.Max(shakeDuration, duration);
            slowDuration = Mathf.Max(slowDuration, duration);

            // Подбираем короткий fade-in, чтобы эффект быстро набирался, но не включался резко.
            shakeFadeInDuration = Mathf.Min(0.45f, shakeDuration * 0.2f);
            // Затухание делаем длиннее, чтобы землетрясение сходило мягко.
            shakeFadeOutDuration = Mathf.Min(1.2f, shakeDuration * 0.35f);
            // Замедление включается чуть плавнее, чем камера, чтобы ощущалось естественнее.
            slowFadeInDuration = Mathf.Min(0.6f, slowDuration * 0.2f);
            // И так же плавно отпускает игрока в конце.
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
            var deathState = new DeathState(_playerView.Animator, _behaviorStateMachine);
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

            standingState.AddTransition(walkState, () => !_inputHandler.MovementInput.Equals(Vector2.zero));
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
            crouchState.AddTransition(blockState, () => PlayerScreenScript.IsCameraFullScreen);
            crouchState.AddTransition(fallState, () => _movementController.IsFalling);
            crouchState.AddTransition(crouchIdleState, () => _inputHandler.MovementInput.Equals(Vector2.zero));
            crouchState.AddTransition(jumpState, () => jumpStatePrecondition());

            crouchIdleState.AddTransition(crouchState, () => !_inputHandler.MovementInput.Equals(Vector2.zero));
            crouchIdleState.AddTransition(blockState, () => PlayerScreenScript.IsCameraFullScreen);
            crouchIdleState.AddTransition(standingState, () => _inputHandler.CrouchTrigger.IsTriggered && _playerBodyController.CanStand(), _inputHandler.CrouchTrigger.ReleaseTrigger);
            crouchIdleState.AddTransition(jumpState, () => jumpStatePrecondition());

            blockState.AddTransition(walkState, () => !PlayerScreenScript.IsCameraFullScreen);
            // blockState.AddTransition(walkState, () => _inputHandler.UseTrigger.IsTriggered);

            jumpState.AddTransition(runState, () => runStatePrecondition() && jumpState.CanGround() && _movementController.IsGrounded);
            jumpState.AddTransition(walkState, () => jumpState.CanGround() && _movementController.IsGrounded);
            jumpState.AddTransition(fallState, jumpState.InHightPoint);
            jumpState.AddTransition(grabLedgeState, () => _inputHandler.GrabLedgeTrigger.IsTriggered && _ledgeController.CanGrabToLedgeGrabPointInView());

            fallState.AddTransition(walkState,
                () => _movementController.IsGrounded && !fallState.ShouldRecover);
            fallState.AddTransition(grabLedgeState, () => _inputHandler.GrabLedgeTrigger.IsTriggered && _ledgeController.CanGrabToLedgeGrabPointInView());

            grabLedgeState.AddTransition(fallState, () => _inputHandler.MoveBack && grabLedgeState.CanFinish());
            grabLedgeState.AddTransition(climbingUpState, () => _inputHandler.MoveForward && grabLedgeState.CanFinish() && climbingUpState.CanClimb());
            grabLedgeState.AddTransition(ledgeJumpState, () => _inputHandler.JumpTriggered && grabLedgeState.CanFinish());

            ledgeJumpState.AddTransition(grabLedgeState, () => _ledgeController.CanGrabToLedgeGrabPointInView());
            ledgeJumpState.AddTransition(fallState, ledgeJumpState.InHightPoint);

            climbingUpState.AddTransition(walkState, () => climbingUpState.ClimbFinish());
            climbingOnState.AddTransition(walkState, () => climbingOnState.ClimbFinish());
            climbingOverState.AddTransition(fallState, () => climbingOverState.ClimbFinish());
            fallState.AddTransition(recoverState,
                () => fallState.ShouldRecover);
            recoverState.AddTransition(standingState,
                () => recoverState.IsFinished);

            _behaviorStateMachine.SetInitState(walkState);

        }

        public void Tick()
        {
            if (!PlayerScreenScript.IsCameraFullScreen)
            {
                _movementShakeProvider.Tick();
                _ledgeController.HandleFindingLedge();
                _playerRotationController.HandlePlayerRotation();
            }
            _behaviorStateMachine.Update();
            _stepController.Update();
            if (_health.IsDead)
            {
                _behaviorStateMachine.ForseChangeState<DeathState>();
                return;
            }

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

            if (_inputHandler.VisorTrigger.IsTriggered)
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

                // Сбрасываем триггер после обработки
                _inputHandler.VisorTrigger.ReleaseTrigger();
            }
            
            // ============ DAMAGE SHAKE CHECK ============
        }


        public void FixedTick()
        {
            _movementController.HandleMovement();
            _movementController.CheckGrounded();
        }
    }
}
