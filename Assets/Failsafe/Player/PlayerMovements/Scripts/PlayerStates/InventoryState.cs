using UnityEngine;

namespace Failsafe.PlayerMovements.States
{
    /// <summary>
    /// Открыт инвентарь
    /// </summary>
    public class InventoryState : BehaviorState
    {
        public override void Enter()
        {
            global::Failsafe.Debugging.GameplayLog.Trace("Enter " + nameof(InventoryState));
        }
        // TODO вызывается когда игрок открывает инвентарь
    }
}
