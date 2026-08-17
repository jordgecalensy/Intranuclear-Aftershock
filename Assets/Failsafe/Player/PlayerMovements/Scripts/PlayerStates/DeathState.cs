using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.PlayerMovements.States
{
	public class DeathState : BehaviorForcedState
	{
        private readonly Animator _animationController;
        private readonly PlayerControlBlocker _controlBlocker;
        private readonly InputHandler _inputHandler;
        private readonly PlayerMovementController _movementController;
        private readonly int _deadId = Animator.StringToHash("Dead");

        public DeathState(
            Animator animationController,
            PlayerControlBlocker controlBlocker,
            InputHandler inputHandler,
            PlayerMovementController movementController)
        {
            _animationController = animationController;
            _controlBlocker = controlBlocker;
            _inputHandler = inputHandler;
            _movementController = movementController;
        }

		public override void Enter()
		{
			base.Enter();

            _controlBlocker?.AddLock(PlayerControlLockIds.Death, PlayerControlBlock.All);
            _movementController?.ResetTransientState();
            _inputHandler?.SetGameplayInputEnabled(false);

            if (_animationController != null)
                _animationController.SetBool(_deadId, true);

			Debug.Log("You are dead");
		}
	}
}
