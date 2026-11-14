using System;

namespace Failsafe.Inventory.Presentation
{
    public enum InventoryRobotReaction
    {
        Random,
        Open1,
        Open2,
        Open3,
        FuckOff
    }

    public static class InventoryRobotReactionSelector
    {
        public const int RollCount = 10000;

        // Integer ranges give exact weights: 50%, 22.5%, 22.5%, 5%.
        public static InventoryRobotReaction Select(int roll)
        {
            if (roll < 0 || roll >= RollCount)
                throw new ArgumentOutOfRangeException(nameof(roll));
            if (roll < 5000) return InventoryRobotReaction.Open1;
            if (roll < 7250) return InventoryRobotReaction.Open2;
            if (roll < 9500) return InventoryRobotReaction.Open3;
            return InventoryRobotReaction.FuckOff;
        }
    }
}
