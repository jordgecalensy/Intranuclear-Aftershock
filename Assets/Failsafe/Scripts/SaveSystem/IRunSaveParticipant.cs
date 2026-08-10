using Cysharp.Threading.Tasks;

namespace Failsafe.Scripts.SaveSystem
{
    public static class RunSaveParticipantIds
    {
        public const string World = "world";
        public const string Player = "player";
        public const string Enemies = "enemies";
    }

    public interface IRunSaveParticipant
    {
        string ParticipantId { get; }
        int RestoreOrder { get; }

        void Capture(RunCheckpointData checkpoint);
        UniTask RestoreAsync(RunCheckpointData checkpoint, RunLoadContext context);
    }
}
