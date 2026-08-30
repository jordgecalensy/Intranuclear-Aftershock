using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Scripts.SaveSystem.Tests
{
    [TestFixture]
    public sealed class RunPersistentObjectRegistryTests
    {
        private readonly List<GameObject> _gameObjects = new();
        private readonly List<RunPersistentObject> _results = new();

        private RunPersistentObjectRegistry _registry;

        [TearDown]
        public void TearDown()
        {
            _registry?.Dispose();
            _registry = null;

            for (int i = _gameObjects.Count - 1; i >= 0; i--)
            {
                if (_gameObjects[i] != null)
                    Object.DestroyImmediate(_gameObjects[i]);
            }

            _gameObjects.Clear();
            _results.Clear();
        }

        [Test]
        public void FirstQuery_FindsObjectThatIsInactiveInHierarchy()
        {
            RunPersistentObject persistentObject =
                CreatePersistentObject("Initially Inactive", isActive: false);
            _registry = new RunPersistentObjectRegistry();

            _registry.GetObjects(_results);

            Assert.That(_results, Does.Contain(persistentObject));
        }

        [Test]
        public void RuntimeObjectCreatedAfterFirstQuery_IsRegistered()
        {
            _registry = new RunPersistentObjectRegistry();
            _registry.GetObjects(_results);

            RunPersistentObject persistentObject =
                CreatePersistentObject("Runtime Active", isActive: true);
            InvokeUnityMessage(persistentObject, "Awake");
            _registry.GetObjects(_results);

            Assert.That(_results, Does.Contain(persistentObject));
        }

        [Test]
        public void DisabledObject_RemainsRegistered()
        {
            _registry = new RunPersistentObjectRegistry();
            _registry.GetObjects(_results);
            RunPersistentObject persistentObject =
                CreatePersistentObject("Disabled Later", isActive: true);
            InvokeUnityMessage(persistentObject, "Awake");

            persistentObject.gameObject.SetActive(false);
            _registry.GetObjects(_results);

            Assert.That(_results, Does.Contain(persistentObject));
        }

        [Test]
        public void InactiveRuntimeObject_IsRegisteredWhenRuntimeIdIsAssigned()
        {
            _registry = new RunPersistentObjectRegistry();
            _registry.GetObjects(_results);
            RunPersistentObject persistentObject =
                CreatePersistentObject("Runtime Inactive", isActive: false);

            persistentObject.AssignRuntimeId("test-runtime-object");
            _registry.GetObjects(_results);

            Assert.That(_results, Does.Contain(persistentObject));
        }

        [Test]
        public void DestroyedObject_IsRemoved()
        {
            _registry = new RunPersistentObjectRegistry();
            _registry.GetObjects(_results);
            RunPersistentObject persistentObject =
                CreatePersistentObject("Destroyed", isActive: true);
            InvokeUnityMessage(persistentObject, "Awake");
            _registry.GetObjects(_results);
            Assert.That(_results, Does.Contain(persistentObject));

            GameObject destroyedObject = persistentObject.gameObject;
            _gameObjects.Remove(destroyedObject);
            InvokeUnityMessage(persistentObject, "OnDestroy");
            Object.DestroyImmediate(destroyedObject);
            _registry.GetObjects(_results);

            Assert.That(
                _results.Exists(
                    result => ReferenceEquals(result, persistentObject)),
                Is.False);
        }

        private RunPersistentObject CreatePersistentObject(
            string name,
            bool isActive)
        {
            var gameObject = new GameObject(name);
            _gameObjects.Add(gameObject);
            gameObject.SetActive(isActive);
            return gameObject.AddComponent<RunPersistentObject>();
        }

        private static void InvokeUnityMessage(
            RunPersistentObject persistentObject,
            string methodName)
        {
            MethodInfo method = typeof(RunPersistentObject).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                method,
                Is.Not.Null,
                $"RunPersistentObject.{methodName} was not found.");

            method.Invoke(persistentObject, null);
        }
    }
}
