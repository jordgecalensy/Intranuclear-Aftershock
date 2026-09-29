using System;

namespace Failsafe.Chests
{
    public readonly struct ChestOpenRequirementContext
    {
        public ChestInventoryController Chest { get; }
        public PlayerInteractionContext Player { get; }

        public ChestOpenRequirementContext(
            ChestInventoryController chest,
            PlayerInteractionContext player)
        {
            Chest = chest;
            Player = player;
        }
    }

    public readonly struct ChestOpenRequirementResult
    {
        public bool IsGranted { get; }
        public string Error { get; }

        private ChestOpenRequirementResult(bool isGranted, string error)
        {
            IsGranted = isGranted;
            Error = error;
        }

        public static ChestOpenRequirementResult Granted()
        {
            return new ChestOpenRequirementResult(true, null);
        }

        public static ChestOpenRequirementResult Rejected(string error)
        {
            return new ChestOpenRequirementResult(false, error);
        }
    }

    public interface IChestOpenRequirement
    {
        void BeginCheck(
            ChestOpenRequirementContext context,
            Action<ChestOpenRequirementResult> completion);

        void CancelCheck();
    }
}
