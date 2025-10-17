using Failsafe.Player;
using UnityEngine;

namespace Failsafe.PlayerMovements.States
{
	public class DeathState : BehaviorForcedState
	{
        private Animator _animationController;
        private int _deadId = Animator.StringToHash("Dead");
        private BehaviorStateMachine _stateMachine;
        public DeathState(Animator animationController, BehaviorStateMachine stateMachine)
        {
            _animationController = animationController;
            _stateMachine = stateMachine;
        }
		public override void Enter()
		{
			Debug.Log("You are dead");
		}
        
        public override void Update()
        {
            if (_animationController != null)
            {
                _animationController.SetBool(_deadId, true);
            }
        }
	}
}