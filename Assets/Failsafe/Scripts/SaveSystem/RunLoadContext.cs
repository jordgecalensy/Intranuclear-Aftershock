namespace Failsafe.Scripts.SaveSystem
{
    public sealed class RunLoadContext
    {
        public RunSaveFile SaveFile { get; }
        public RunCheckpointData Checkpoint => SaveFile.checkpoint;
        public string RunId => SaveFile.runId;

        public RunLoadContext(RunSaveFile saveFile)
        {
            SaveFile = saveFile;
        }
    }
}
