using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Failsafe.Scripts.SaveSystem
{
    public sealed class RunSaveRepository : IRunSaveRepository
    {
        private const string SaveDirectoryName = "Saves";
        private const string SaveFileName = "run.json";
        private const string BackupFileName = "run.json.bak";
        private const string TemporaryFileName = "run.json.tmp";

        private readonly string _saveDirectory;
        private readonly string _temporaryPath;

        public string SavePath { get; }
        public string BackupPath { get; }
        public bool Exists => File.Exists(SavePath) || File.Exists(BackupPath);

        public RunSaveRepository()
        {
            _saveDirectory = Path.Combine(Application.persistentDataPath, SaveDirectoryName);
            SavePath = Path.Combine(_saveDirectory, SaveFileName);
            BackupPath = Path.Combine(_saveDirectory, BackupFileName);
            _temporaryPath = Path.Combine(_saveDirectory, TemporaryFileName);
        }

        public bool TryLoad(out RunSaveFile saveFile, out bool loadedFromBackup, out string error)
        {
            saveFile = null;
            loadedFromBackup = false;

            if (TryReadFile(SavePath, out saveFile, out string primaryError))
            {
                error = null;
                return true;
            }

            if (TryReadFile(BackupPath, out saveFile, out string backupError))
            {
                loadedFromBackup = true;
                error = null;
                return true;
            }

            error = $"Primary save: {primaryError} Backup: {backupError}";
            return false;
        }

        public bool TrySave(RunSaveFile saveFile, out string error)
        {
            if (saveFile == null)
            {
                error = "Cannot save a null run.";
                return false;
            }

            saveFile.EnsureInitialized();
            if (!TryValidate(saveFile, out error))
                return false;

            try
            {
                Directory.CreateDirectory(_saveDirectory);
                string json = JsonUtility.ToJson(saveFile, true);
                WriteTemporaryFile(json);

                if (!File.Exists(SavePath))
                {
                    File.Move(_temporaryPath, SavePath);
                }
                else if (TryReadFile(SavePath, out _, out _))
                {
                    ReplacePrimaryWithBackup();
                }
                else
                {
                    // Preserve the last valid backup instead of replacing it with a corrupt primary.
                    File.Delete(SavePath);
                    File.Move(_temporaryPath, SavePath);
                }

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to write run save: {exception.Message}";
                return false;
            }
            finally
            {
                TryDeleteTemporaryFile();
            }
        }

        public bool TryDelete(out string error)
        {
            try
            {
                DeleteIfExists(SavePath);
                DeleteIfExists(BackupPath);
                DeleteIfExists(_temporaryPath);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to delete run save: {exception.Message}";
                return false;
            }
        }

        private bool TryReadFile(string path, out RunSaveFile saveFile, out string error)
        {
            saveFile = null;

            if (!File.Exists(path))
            {
                error = "File does not exist.";
                return false;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                saveFile = JsonUtility.FromJson<RunSaveFile>(json);

                if (!TryValidate(saveFile, out error))
                {
                    saveFile = null;
                    return false;
                }

                saveFile.EnsureInitialized();
                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to read '{path}': {exception.Message}";
                saveFile = null;
                return false;
            }
        }

        private static bool TryValidate(RunSaveFile saveFile, out string error)
        {
            if (saveFile == null)
            {
                error = "Save data is empty or invalid JSON.";
                return false;
            }

            if (saveFile.schemaVersion <= 0)
            {
                error = "Save schema version is missing.";
                return false;
            }

            if (saveFile.schemaVersion != RunSaveFile.CurrentSchemaVersion)
            {
                error = $"Save schema {saveFile.schemaVersion} is not supported. Expected schema " +
                        $"{RunSaveFile.CurrentSchemaVersion}; an explicit migration is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(saveFile.runId))
            {
                error = "Run id is missing.";
                return false;
            }

            error = null;
            return true;
        }

        private void WriteTemporaryFile(string json)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);

            using (FileStream stream = new FileStream(
                       _temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private void ReplacePrimaryWithBackup()
        {
            try
            {
                File.Replace(_temporaryPath, SavePath, BackupPath);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(SavePath, BackupPath, true);
                File.Copy(_temporaryPath, SavePath, true);
                File.Delete(_temporaryPath);
            }
        }

        private void TryDeleteTemporaryFile()
        {
            try
            {
                DeleteIfExists(_temporaryPath);
            }
            catch (Exception)
            {
                // A stale temp file is ignored and overwritten by the next save attempt.
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
