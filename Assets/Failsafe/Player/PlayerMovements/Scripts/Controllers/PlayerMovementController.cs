using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem.Targets;
using UnityEngine;

namespace Failsafe.PlayerMovements.Controllers
{
    public class PlayerMovementController : IMovementSpeedModifierTarget
    {
        public Vector3 Velocity { get; private set; }

        private readonly CharacterController _characterController;
        private readonly PlayerMovementParameters _playerMovementParameters;

        private Vector3 _movement;
        private Vector3 _gravity;

        private float _coyoteTime = 0.1f;
        private float _coyoteTimeProgress = 0f;
        private float _groundedAt;

        private readonly Dictionary<int, float> _speedModifiers = new();

        private PlayerNoiseSignal _playerNoiseSignal;

        public float CurrentSpeedMultiplier
        {
            get
            {
                float multiplier = 1f;

                foreach (var speedModifier in _speedModifiers)
                    multiplier *= speedModifier.Value;

                return multiplier;
            }
        }

        public bool IsGrounded => _coyoteTimeProgress <= 0f;

        public bool IsGroundedFor(float duration)
        {
            return _groundedAt + duration <= Time.time;
        }

        public bool IsFalling => _coyoteTimeProgress > _coyoteTime;

        public PlayerMovementController(
            CharacterController characterController,
            PlayerMovementParameters playerMovementParameters)
        {
            _characterController = characterController;
            _playerMovementParameters = playerMovementParameters;
        }

        public void SetSpeedModifier(int modifierId, float multiplier)
        {
            if (multiplier <= 0f)
                multiplier = 0.0001f;

            _speedModifiers[modifierId] = multiplier;
        }

        public void RemoveSpeedModifier(int modifierId)
        {
            _speedModifiers.Remove(modifierId);
        }

        public void SetPlayerNoiseSignal(PlayerNoiseSignal signal)
        {
            _playerNoiseSignal = signal;
        }

        public Vector3 GetRelativeMovement(Vector2 inputMovement)
        {
            return Vector3.ClampMagnitude(
                inputMovement.x * _characterController.transform.right +
                inputMovement.y * _characterController.transform.forward,
                1f);
        }

        public void Move(Vector3 motion)
        {
            _movement = motion;
        }

        public void SetGravity(Vector3 gravity)
        {
            _gravity = gravity;
        }

        public void SetGravityDefault()
        {
            SetGravity(_playerMovementParameters.GravityForce * Vector3.down);
        }

        public void HandleMovement()
        {
            Vector3 motion = (_movement * CurrentSpeedMultiplier) + _gravity;

            _characterController.Move(motion * Time.deltaTime);

            Velocity = _characterController.velocity;

            UpdateNoiseSignal();
        }

        public void CheckGrounded()
        {
            if (_characterController.isGrounded)
            {
                if (_coyoteTimeProgress > 0f)
                {
                    _coyoteTimeProgress = 0f;
                    _groundedAt = Time.time;
                }
            }
            else
            {
                _coyoteTimeProgress += Time.deltaTime;
            }
        }

        private void UpdateNoiseSignal()
        {
            if (_playerNoiseSignal == null)
                return;

            float horizontalSpeed = new Vector3(
                Velocity.x,
                0f,
                Velocity.z).magnitude;

            float noiseStrength = IsGrounded
                ? horizontalSpeed
                : 0f;

            _playerNoiseSignal.UpdateStrength(noiseStrength);
        }
    }
}