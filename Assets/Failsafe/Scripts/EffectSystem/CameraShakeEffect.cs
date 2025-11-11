using UnityEngine;
using Failsafe.PlayerMovements.Controllers;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Эффект тряски камеры через микровращение головы.
    /// Отдельные параметры для ходьбы и спринта.
    /// </summary>
    public class CameraShakeEffect : Effect
    {
        private readonly PlayerRotationController _rotationController;
        private readonly InputHandler _input;

        // === Ходьба ===
        [Header("Walking Shake Settings")]
        private readonly float _walkIntensity = 0f;   // амплитуда тряски при ходьбе (в градусах)
        private readonly float _walkSpeed = 0f;        // частота тряски при ходьбе

        // === Спринт ===
        [Header("Sprinting Shake Settings")]
        private readonly float _sprintIntensity = 0.3f; // амплитуда тряски при спринте
        private readonly float _sprintSpeed = 11f;      // частота тряски при спринте

        private float _shakeTime = 0f;

        public CameraShakeEffect(PlayerRotationController rotationController, InputHandler input)
        {
            _rotationController = rotationController;
            _input = input;

            _duration = Mathf.Infinity;
            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            _shakeTime = 0f;
        }

        public override void ClearEffect()
        {
            // вращение головы вернётся к базовому в контроллере
        }

        public override void Update()
        {
            if (_rotationController == null || _input == null)
                return;

            bool isMoving = _input.MovementInput.x != 0 || _input.MovementInput.y != 0;
            bool isSprinting = _input.SprintTriggered;

            if (!isMoving)
                return;

            // выбираем параметры в зависимости от состояния
            float intensity = isSprinting ? _sprintIntensity : _walkIntensity;
            float shakeSpeed = isSprinting ? _sprintSpeed : _walkSpeed;

            _shakeTime += Time.deltaTime * shakeSpeed;

            // синусоида даёт мягкое естественное покачивание
            float verticalShake = Mathf.Sin(_shakeTime * 1.3f) * intensity;
            float horizontalShake = Mathf.Cos(_shakeTime * 1.7f) * intensity * 0.8f;

            // применяем поверх текущего вращения головы
            float shakenVertical = _rotationController.HeadLocalRotation.x + verticalShake;
            float shakenHorizontal = _rotationController.HeadLocalRotation.y + horizontalShake;

            _rotationController.HeadTransform.localRotation =
                Quaternion.Euler(shakenVertical, shakenHorizontal, 0f);
        }
    }
}
