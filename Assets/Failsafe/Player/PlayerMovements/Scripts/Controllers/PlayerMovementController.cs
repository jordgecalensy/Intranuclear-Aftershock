using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Failsafe.PlayerMovements.Controllers
{
    /// <summary>
    /// Контроллер перемещения персонажа
    /// </summary>
    public class PlayerMovementController
    {
        public Vector3 Velocity { get; private set; }
        private readonly CharacterController _characterController;
        private readonly PlayerMovementParameters _playerMovementParameters;
        private Vector3 _movement;
        private Vector3 _gravity;

        private float _coyoteTime = 0.1f;
        private float _coyoteTimeProgress = 0f;
        private float _groundedAt;

        private readonly Dictionary<int, float> _speedModifiers = new Dictionary<int, float>();

        // --- НОВОЕ: Ссылка на сигнал ---
        private PlayerNoiseSignal _playerNoiseSignal;
        // ------------------------------

        public float CurrentSpeedMultiplier
        {
            get
            {
                float mul = 1f;
                foreach (var kv in _speedModifiers)
                    mul *= kv.Value;
                return mul;
            }
        }

        public void SetSpeedModifier(int id, float multiplier)
        {
            if (multiplier <= 0f) multiplier = 0.0001f;
            _speedModifiers[id] = multiplier;

            string mods = string.Join(", ", _speedModifiers.Select(kv => $"{kv.Key}:{kv.Value:0.00}"));
        }

        public void RemoveSpeedModifier(int id)
        {
            if (_speedModifiers.Remove(id))
            {
                string mods = string.Join(", ", _speedModifiers.Select(kv => $"{kv.Key}:{kv.Value:0.00}"));
            }
        }

        public void SetPlayerNoiseSignal(PlayerNoiseSignal signal)
        {
            _playerNoiseSignal = signal;
        }

        public bool IsGrounded => _coyoteTimeProgress <= 0;
        public bool IsGroundedFor(float duration) => _groundedAt + duration <= Time.time;
        public bool IsFalling => _coyoteTimeProgress > _coyoteTime;

        public PlayerMovementController(CharacterController characterController, PlayerMovementParameters playerMovementParameters)
        {
            _characterController = characterController;
            _playerMovementParameters = playerMovementParameters;
        }

        public Vector3 GetRelativeMovement(Vector2 inputMovement)
        {
            return Vector3.ClampMagnitude(
                inputMovement.x * _characterController.transform.right +
                inputMovement.y * _characterController.transform.forward, 1);
        }

        public void Move(Vector3 motion) => _movement = motion;
        public void SetGravity(Vector3 gravity) => _gravity = gravity;
        public void SetGravityDefault() => SetGravity(_playerMovementParameters.GravityForce * Vector3.down);

        public void HandleMovement()
        {
            var motion = (_movement * CurrentSpeedMultiplier) + _gravity;
            _characterController.Move(motion * Time.deltaTime);
            Velocity = _characterController.velocity;

            // --- НОВОЕ: Обновление сигнала шума ---
            if (_playerNoiseSignal != null)
            {
                Debug.Log("Нет системы!");
                // Считаем скорость по горизонтали
                float horizontalSpeed = new Vector3(Velocity.x, 0, Velocity.z).magnitude;
                // Если игрок на земле, он шумит пропорционально скорости. Если в полете — 0 (или можно оставить скорость)
                float noiseStrength = IsGrounded ? horizontalSpeed : 0f;
                
                _playerNoiseSignal.UpdateStrength(noiseStrength);
            }
        }

        public void CheckGrounded()
        {
            if (_characterController.isGrounded)
            {
                if (_coyoteTimeProgress > 0)
                {
                    _coyoteTimeProgress = 0;
                    _groundedAt = Time.time;
                }
            }
            else
            {
                _coyoteTimeProgress += Time.deltaTime;
            }
        }
    }
}