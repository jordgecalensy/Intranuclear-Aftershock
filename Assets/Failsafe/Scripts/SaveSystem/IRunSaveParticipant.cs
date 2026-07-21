using Cysharp.Threading.Tasks;

namespace Failsafe.Scripts.SaveSystem
{
    public interface IRunSaveParticipant
    {
        string ParticipantId { get; }
        int RestoreOrder { get; }

        void Capture(RunCheckpointData checkpoint);
        UniTask RestoreAsync(RunCheckpointData checkpoint, RunLoadContext context);
    }
}
