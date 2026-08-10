namespace Failsafe.Scripts.SaveSystem
{
    public interface IRunSaveRepository
    {
        string SavePath { get; }
        string BackupPath { get; }
        bool Exists { get; }

        bool TryLoad(out RunSaveFile saveFile, out bool loadedFromBackup, out string error);
        bool TrySave(RunSaveFile saveFile, out string error);
        bool TryMarkRunEnded(
            string runId,
            long endedAtUnixMilliseconds,
            string endReason,
            out string error);
        bool TryDelete(out string error);
    }
}
