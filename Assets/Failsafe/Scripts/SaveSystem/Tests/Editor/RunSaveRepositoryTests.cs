using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Scripts.SaveSystem.Tests
{
    [TestFixture]
    public sealed class RunSaveRepositoryTests
    {
        private static readonly string TestRoot = Path.Combine(
            Path.GetTempPath(),
            "Failsafe.RunSaveRepositoryTests");

        private string _testDirectory;
        private RunSaveRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(
                TestRoot,
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_testDirectory);
            _repository = CreateRepository(_testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            _repository = null;

            if (!string.IsNullOrWhiteSpace(_testDirectory) &&
                Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }

            _testDirectory = null;
        }

        [Test]
        public void TrySave_WritesAndRotatesIntegrityReceipts()
        {
            SaveRevision(1);

            Assert.That(File.Exists(_repository.SavePath + ".integrity"), Is.True);

            SaveRevision(2);

            Assert.That(File.Exists(_repository.BackupPath), Is.True);
            Assert.That(File.Exists(_repository.BackupPath + ".integrity"), Is.True);
            Assert.That(ReadSave(_repository.SavePath).saveRevision, Is.EqualTo(2));
            Assert.That(ReadSave(_repository.BackupPath).saveRevision, Is.EqualTo(1));
        }

        [Test]
        public void TrySave_WhenPrimaryChecksumDoesNotMatch_PreservesBackup()
        {
            SaveRevision(1);
            SaveRevision(2);

            string primaryJson = File.ReadAllText(_repository.SavePath);
            string corruptedJson = primaryJson.Replace(
                "\"saveRevision\": 2",
                "\"saveRevision\": 999");

            Assert.That(corruptedJson, Is.Not.EqualTo(primaryJson));
            File.WriteAllText(_repository.SavePath, corruptedJson);

            SaveRevision(3);

            Assert.That(ReadSave(_repository.SavePath).saveRevision, Is.EqualTo(3));
            Assert.That(ReadSave(_repository.BackupPath).saveRevision, Is.EqualTo(1));
        }

        [Test]
        public void TrySave_WhenReceiptIsMissing_ValidatesLegacyPrimary()
        {
            SaveRevision(1);
            File.Delete(_repository.SavePath + ".integrity");

            SaveRevision(2);

            Assert.That(ReadSave(_repository.SavePath).saveRevision, Is.EqualTo(2));
            Assert.That(ReadSave(_repository.BackupPath).saveRevision, Is.EqualTo(1));
        }

        private void SaveRevision(long revision)
        {
            RunSaveFile saveFile = RunSaveFile.CreateNew();
            saveFile.saveRevision = revision;

            bool succeeded = _repository.TrySave(saveFile, out string error);

            Assert.That(succeeded, Is.True, error);
        }

        private static RunSaveFile ReadSave(string path)
        {
            return JsonUtility.FromJson<RunSaveFile>(File.ReadAllText(path));
        }

        private static RunSaveRepository CreateRepository(string directory)
        {
            ConstructorInfo constructor = typeof(RunSaveRepository).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.That(
                constructor,
                Is.Not.Null,
                "RunSaveRepository test constructor was not found.");

            return (RunSaveRepository)constructor.Invoke(
                new object[] { directory });
        }
    }
}
