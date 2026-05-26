using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.PlayerMovements.States
{
    /// <summary>
    /// Наклон камеры влево/вправо с сохранением обычного вращения головы.
    /// </summary>
    public class SlantState : BehaviorState
    {
        private const float CrouchHeightThreshold = 1.01f;

        private readonly InputHandler _inputHandler;
        private readonly PlayerMovementController _movementController;
        private readonly PlayerMovementParameters _movementParameters;
        private readonly PlayerBodyController _playerBodyController;
        private readonly PlayerRotationController _playerRotationController;
        private readonly PlayerNoiseController _playerNoiseController;
        private readonly StepController _stepController;
        private readonly CharacterController _characterController;

        private bool _isCrouchedSlant;

        private float Speed => _isCrouchedSlant
            ? _movementParameters.CrouchSpeed
            : _movementParameters.WalkSpeed;

        private PlayerNoiseVolume NoiseVolume => _isCrouchedSlant
            ? PlayerNoiseVolume.Reduced
            : PlayerNoiseVolume.Default;

        public bool IsCrouchedSlant => _isCrouchedSlant;
        public bool IsWalkSlant => !_isCrouchedSlant;
        public bool CanExitSlant() => Mathf.Abs(_playerRotationController.HeadLocalRotation.z) <= _movementParameters.SlantExitThreshold;

        public SlantState(
            InputHandler inputHandler,
            PlayerMovementController movementController,
            PlayerMovementParameters movementParameters,
            PlayerBodyController playerBodyController,
            PlayerRotationController playerRotationController,
            PlayerNoiseController playerNoiseController,
            StepController stepController,
            CharacterController characterController)
        {
            _inputHandler = inputHandler;
            _movementController = movementController;
            _movementParameters = movementParameters;
            _playerBodyController = playerBodyController;
            _playerRotationController = playerRotationController;
            _playerNoiseController = playerNoiseController;
            _stepController = stepController;
            _characterController = characterController;
        }

        public override void Enter()
        {
            _isCrouchedSlant = _characterController.height <= CrouchHeightThreshold;

            if (_isCrouchedSlant)
                _playerBodyController.Crouch();

            _playerRotationController.SetHeadRoll(GetTargetRoll(), _movementParameters.SlantSmoothTime);
            _playerNoiseController.SetNoiseStrength(NoiseVolume);
            _stepController.Enable(Speed);
        }

        public override void Update()
        {
            var movement = _movementController.GetRelativeMovement(_inputHandler.MovementInput) * Speed;
            _movementController.Move(movement);
            _playerNoiseController.SetNoiseStrength(movement == Vector3.zero ? PlayerNoiseVolume.Minimum : NoiseVolume);
            _playerRotationController.SetHeadRoll(GetTargetRoll(), _movementParameters.SlantSmoothTime);
        }

        public override void Exit()
        {
            _playerRotationController.SetHeadRoll(0f, _movementParameters.SlantSmoothTime);

            if (_isCrouchedSlant)
                _playerBodyController.Stand();

            _stepController.Disable();
        }

        private float GetTargetRoll()
        {
            if (_inputHandler.SlantLeftTrigger == _inputHandler.SlantRightTrigger)
                return 0f;

            return _inputHandler.SlantLeftTrigger
                ? _movementParameters.SlantAngle
                : -_movementParameters.SlantAngle;
        }
    }
}
