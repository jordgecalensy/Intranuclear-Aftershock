using Failsafe.Player.Model;
using Failsafe.PlayerMovements.Controllers;
using System.Collections;
using UnityEngine;

namespace Failsafe.PlayerMovements.States
{
    /// <summary>
    /// Режим, при котором камера занимает весь экран и игрок может свободно вращать её, не влияя на положение персонажа.
    /// </summary>
    public class BlockState : BehaviorState
    {
        private PlayerMovementController _movementController;

        public BlockState(PlayerMovementController movementController)
        {
            _movementController = movementController;
        }

        public override void Enter()
        {
            base.Enter();
            Debug.Log("Enter " + nameof(BlockState));
        }

        public override void Update()
        {
            _movementController.Move(Vector3.zero);
        }
    }
}
