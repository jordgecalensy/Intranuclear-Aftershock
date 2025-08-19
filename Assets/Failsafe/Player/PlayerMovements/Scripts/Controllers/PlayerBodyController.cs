using System.Collections;
using UnityEngine;

namespace Failsafe.PlayerMovements.Controllers
{
    /// <summary>
    /// Контроллер тела персонажа
    /// </summary>
    public class PlayerBodyController
    {
        private readonly CharacterController _characterController;

        private float _standingCCCenterHeight = 1f;
        private float _standingCCHeight = 2f;
        private float _standingCCStepOffset = 1f;

        private float _crouchingCCCenterHeight = 0.5f;
        private float _crouchingCCHeight = 1f;
        private float _crouchingCCStepOffset = 0.2f;

        private int _ignoreLedgeLayer;

        public PlayerBodyController(CharacterController characterController)
        {
            _characterController = characterController;
            _ignoreLedgeLayer = LayerMask.NameToLayer("Ledge");
        }

        public bool CanStand()
        {
            // Точка слегка выше обычной капсулы, чтобы случайно не задеть пол
            var point = _characterController.transform.position + Vector3.up * 0.51f;
            var sphereRadius = _characterController.radius - _characterController.skinWidth;
            var distance = _standingCCHeight - sphereRadius * 2;
            if (Physics.SphereCast(point, sphereRadius, Vector3.up, out var hitInfo, distance, _ignoreLedgeLayer))
            {
                Debug.Log("Cant stand :" + hitInfo.transform.name);
                return false;
            }
            return true;
        }

        public void Stand()
        {
            _characterController.center = _standingCCCenterHeight * Vector3.up;
            _characterController.height = _standingCCHeight;
            _characterController.stepOffset = _standingCCStepOffset;
        }

        public void Crouch()
        {
            _characterController.center = _crouchingCCCenterHeight * Vector3.up;
            _characterController.height = _crouchingCCHeight;
            _characterController.stepOffset = _crouchingCCStepOffset;
        }

        public void Slide()
        {
            // Высота модели в скольжении такая же как в присяди, 
            // чтобы игрок не мог куда то проскользить и там застрять без возможности присесть
            _characterController.center = _crouchingCCCenterHeight * Vector3.up;
            _characterController.height = _crouchingCCHeight;
            _characterController.stepOffset = _crouchingCCStepOffset;
        }
    }
}