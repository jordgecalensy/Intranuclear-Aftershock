using Failsafe.Player.Model;
using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.PlayerMovements.States
{
    /// <summary>
    /// Стояние на месте (без движения)
    /// </summary>
    public class FullScreenState : BehaviorState
    {
        private InputHandler _inputHandler;
        private CharacterController _characterController;
        private PlayerMovementController _movementController;
        private readonly PlayerMovementParameters _movementParameters;
        private readonly PlayerStaminaController _playerStaminaController;

        private bool IsCollidedAbove() => (_characterController.collisionFlags & CollisionFlags.CollidedAbove) != 0;

        public FullScreenState(InputHandler inputHandler, CharacterController characterController, PlayerMovementController movementController, PlayerMovementParameters movementParametrs, PlayerStaminaController playerStaminaController)
        {
            _inputHandler = inputHandler;
            _characterController = characterController;
            _movementController = movementController;
            _movementParameters = movementParametrs;
            _playerStaminaController = playerStaminaController;
        }

        public override void Enter()
        {
            base.Enter();
            Debug.Log("Enter " + nameof(FullScreenState));
        }

        public override void Update()
        {
            _movementController.Move(Vector3.zero);
        }
    }
}
