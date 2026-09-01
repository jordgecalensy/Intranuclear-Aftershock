using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
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
        private const string IntegrityFileSuffix = ".integrity";
        private const string IntegrityFormatVersion = "1";
        private const string RunEndMarkerFileName = "run.ended.json";
        private const string RunEndMarkerTemporaryFileName = "run.ended.json.tmp";

        private readonly string _saveDirectory;
        private readonly string _temporaryPath;
        private readonly string _saveIntegrityPath;
        private readonly string _backupIntegrityPath;
        private readonly string _temporaryIntegrityPath;
        private readonly string _runEndMarkerPath;
        private readonly string _runEndMarkerTemporaryPath;

        public string SavePath { get; }
        public string BackupPath { get; }
        public bool Exists => File.Exists(SavePath) || File.Exists(BackupPath);

        public RunSaveRepository()
            : this(Path.Combine(Application.persistentDataPath, SaveDirectoryName))
        {
        }

        internal RunSaveRepository(string saveDirectory)
        {
            if (string.IsNullOrWhiteSpace(saveDirectory))
                throw new ArgumentException("Save directory cannot be empty.", nameof(saveDirectory));

            _saveDirectory = saveDirectory;
            SavePath = Path.Combine(_saveDirectory, SaveFileName);
            BackupPath = Path.Combine(_saveDirectory, BackupFileName);
            _temporaryPath = Path.Combine(_saveDirectory, TemporaryFileName);
            _saveIntegrityPath = SavePath + IntegrityFileSuffix;
            _backupIntegrityPath = BackupPath + IntegrityFileSuffix;
            _temporaryIntegrityPath = _temporaryPath + IntegrityFileSuffix;
            _runEndMarkerPath = Path.Combine(_saveDirectory, RunEndMarkerFileName);
            _runEndMarkerTemporaryPath =
                Path.Combine(_saveDirectory, RunEndMarkerTemporaryFileName);
        }

        public bool TryLoad(out RunSaveFile saveFile, out bool loadedFromBackup, out string error)
        {
            saveFile = null;
            loadedFromBackup = false;

            string primaryError;
            if (TryReadFile(SavePath, out saveFile, out primaryError))
            {
                if (TryApplyRunEndMarker(saveFile, out string markerError))
                {
                    error = null;
                    return true;
                }

                primaryError = markerError;
                saveFile = null;
            }

            string backupError;
            if (TryReadFile(BackupPath, out saveFile, out backupError))
            {
                if (TryApplyRunEndMarker(saveFile, out string markerError))
                {
                    loadedFromBackup = true;
                    error = null;
                    return true;
                }

                backupError = markerError;
                saveFile = null;
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
                    ReplaceFile(_temporaryIntegrityPath, _saveIntegrityPath);
                }
                else if (IsPrimarySafeForBackup())
                {
                    ReplacePrimaryWithBackup();
                    RotateIntegrityFiles();
                }
                else
                {
                    // Preserve the last valid backup instead of replacing it with a corrupt primary.
                    File.Delete(SavePath);
                    DeleteIfExists(_saveIntegrityPath);
                    File.Move(_temporaryPath, SavePath);
                    ReplaceFile(_temporaryIntegrityPath, _saveIntegrityPath);
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

        public bool TryMarkRunEnded(
            string runId,
            long endedAtUnixMilliseconds,
            string endReason,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                error = "Cannot mark a run as ended without a run id.";
                return false;
            }

            if (endedAtUnixMilliseconds <= 0)
            {
                error = "Cannot mark a run as ended without a valid end timestamp.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(endReason))
            {
                error = "Cannot mark a run as ended without an end reason.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(_saveDirectory);

                var marker = new RunEndMarkerData
                {
                    runId = runId.Trim(),
                    endedAtUnixMilliseconds = endedAtUnixMilliseconds,
                    endReason = endReason.Trim()
                };

                string json = JsonUtility.ToJson(marker, true);
                WriteFileWithDurability(_runEndMarkerTemporaryPath, json);
                ReplaceFile(_runEndMarkerTemporaryPath, _runEndMarkerPath);

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to write the run end marker: {exception.Message}";
                return false;
            }
            finally
            {
                TryDeleteFile(_runEndMarkerTemporaryPath);
            }
        }

        public bool TryDelete(out string error)
        {
            try
            {
                DeleteIfExists(SavePath);
                DeleteIfExists(BackupPath);
                DeleteIfExists(_temporaryPath);
                DeleteIfExists(_saveIntegrityPath);
                DeleteIfExists(_backupIntegrityPath);
                DeleteIfExists(_temporaryIntegrityPath);
                DeleteIfExists(_runEndMarkerPath);
                DeleteIfExists(_runEndMarkerTemporaryPath);
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

            if (!string.IsNullOrWhiteSpace(saveFile.lifecycleState) &&
                !string.Equals(
                    saveFile.lifecycleState,
                    RunLifecycleStates.Active,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    saveFile.lifecycleState,
                    RunLifecycleStates.Ended,
                    StringComparison.Ordinal))
            {
                error = $"Run lifecycle state '{saveFile.lifecycleState}' is not supported.";
                return false;
            }

            if (string.Equals(
                    saveFile.lifecycleState,
                    RunLifecycleStates.Ended,
                    StringComparison.Ordinal))
            {
                if (saveFile.endedAtUnixMilliseconds <= 0)
                {
                    error = "Ended run is missing a valid end timestamp.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(saveFile.endReason))
                {
                    error = "Ended run is missing an end reason.";
                    return false;
                }
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
            WriteFileWithDurability(_temporaryPath, bytes);

            FileIntegrity integrity = CalculateIntegrity(bytes);
            WriteFileWithDurability(
                _temporaryIntegrityPath,
                integrity.Serialize());
        }

        private static void WriteFileWithDurability(string path, string json)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            WriteFileWithDurability(path, bytes);
        }

        private static void WriteFileWithDurability(string path, byte[] bytes)
        {
            using (FileStream stream = new FileStream(
                       path,
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

        private static void ReplaceFile(string temporaryPath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(temporaryPath, destinationPath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, destinationPath, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, destinationPath, true);
                File.Delete(temporaryPath);
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

        private bool IsPrimarySafeForBackup()
        {
            if (TryReadIntegrity(_saveIntegrityPath, out FileIntegrity expected))
            {
                return TryCalculateIntegrity(SavePath, out FileIntegrity actual) &&
                       expected.Equals(actual);
            }

            // Saves created before integrity receipts existed still use the
            // original, slower validation path once.
            return TryReadFile(SavePath, out _, out _);
        }

        private void RotateIntegrityFiles()
        {
            if (File.Exists(_saveIntegrityPath))
                ReplaceFile(_saveIntegrityPath, _backupIntegrityPath);
            else
                DeleteIfExists(_backupIntegrityPath);

            ReplaceFile(_temporaryIntegrityPath, _saveIntegrityPath);
        }

        private static FileIntegrity CalculateIntegrity(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                return new FileIntegrity(
                    bytes.LongLength,
                    Convert.ToBase64String(hash));
            }
        }

        private static bool TryCalculateIntegrity(
            string path,
            out FileIntegrity integrity)
        {
            integrity = default;

            try
            {
                using (FileStream stream = new FileStream(
                           path,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           4096,
                           FileOptions.SequentialScan))
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    integrity = new FileIntegrity(
                        stream.Length,
                        Convert.ToBase64String(hash));
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryReadIntegrity(
            string path,
            out FileIntegrity integrity)
        {
            integrity = default;

            if (!File.Exists(path))
                return false;

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);

                if (lines.Length != 3 ||
                    !string.Equals(
                        lines[0],
                        IntegrityFormatVersion,
                        StringComparison.Ordinal) ||
                    !long.TryParse(
                        lines[1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long length) ||
                    length < 0 ||
                    string.IsNullOrWhiteSpace(lines[2]))
                {
                    return false;
                }

                integrity = new FileIntegrity(length, lines[2].Trim());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void TryDeleteTemporaryFile()
        {
            TryDeleteFile(_temporaryPath);
            TryDeleteFile(_temporaryIntegrityPath);
        }

        private bool TryApplyRunEndMarker(RunSaveFile saveFile, out string error)
        {
            if (saveFile == null)
            {
                error = "Cannot apply a run end marker to an empty save.";
                return false;
            }

            if (!File.Exists(_runEndMarkerPath))
            {
                error = null;
                return true;
            }

            try
            {
                string json = File.ReadAllText(_runEndMarkerPath, Encoding.UTF8);
                RunEndMarkerData marker = JsonUtility.FromJson<RunEndMarkerData>(json);

                if (marker == null ||
                    string.IsNullOrWhiteSpace(marker.runId) ||
                    marker.endedAtUnixMilliseconds <= 0 ||
                    string.IsNullOrWhiteSpace(marker.endReason))
                {
                    error = "Run end marker is empty or invalid.";
                    return false;
                }

                if (string.Equals(marker.runId, saveFile.runId, StringComparison.Ordinal))
                {
                    saveFile.lifecycleState = RunLifecycleStates.Ended;
                    saveFile.endedAtUnixMilliseconds = marker.endedAtUnixMilliseconds;
                    saveFile.endReason = marker.endReason;
                }

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to read the run end marker: {exception.Message}";
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                DeleteIfExists(path);
            }
            catch (Exception)
            {
                // A stale temporary marker is overwritten by the next end-run attempt.
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private readonly struct FileIntegrity : IEquatable<FileIntegrity>
        {
            private readonly long _length;
            private readonly string _hash;

            public FileIntegrity(long length, string hash)
            {
                _length = length;
                _hash = hash;
            }

            public bool Equals(FileIntegrity other)
            {
                return _length == other._length &&
                       string.Equals(_hash, other._hash, StringComparison.Ordinal);
            }

            public string Serialize()
            {
                return IntegrityFormatVersion + "\n" +
                       _length.ToString(CultureInfo.InvariantCulture) + "\n" +
                       _hash;
            }
        }

        [Serializable]
        private sealed class RunEndMarkerData
        {
            public string runId;
            public long endedAtUnixMilliseconds;
            public string endReason;
        }
    }
}
