namespace Failsafe.Scripts.SaveSystem
{
    public enum RunCheckpointBlockReason
    {
        None,
        PlayerDead,
        Airborne,
        RestoreInProgress,
        Combat,
        DamageOverTime,
        CarryingObject
    }

    public readonly struct RunCheckpointSafetyDecision
    {
        public bool CanSave { get; }
        public RunCheckpointBlockReason Reason { get; }
        public string Message { get; }

        private RunCheckpointSafetyDecision(
            bool canSave,
            RunCheckpointBlockReason reason,
            string message)
        {
            CanSave = canSave;
            Reason = reason;
            Message = message;
        }

        public static RunCheckpointSafetyDecision Allowed()
        {
            return new RunCheckpointSafetyDecision(
                true,
                RunCheckpointBlockReason.None,
                null);
        }

        public static RunCheckpointSafetyDecision Blocked(
            RunCheckpointBlockReason reason,
            string message)
        {
            return new RunCheckpointSafetyDecision(false, reason, message);
        }
    }

    public interface IRunCheckpointSafetyPolicy
    {
        RunCheckpointSafetyDecision Evaluate();
    }
}
