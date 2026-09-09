using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Tests
{
    [TestFixture]
    public sealed class ObstacleContactEffectsTests
    {
        private readonly List<GameObject> _gameObjects = new();
        private readonly List<ScriptableObject> _scriptableObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _gameObjects.Count - 1; i >= 0; i--)
            {
                if (_gameObjects[i] != null)
                    Object.DestroyImmediate(_gameObjects[i]);
            }

            for (int i = _scriptableObjects.Count - 1; i >= 0; i--)
            {
                if (_scriptableObjects[i] != null)
                    Object.DestroyImmediate(_scriptableObjects[i]);
            }

            _gameObjects.Clear();
            _scriptableObjects.Clear();
        }

        [Test]
        public void MultipleColliders_KeepTargetUntilLastExit()
        {
            GameObject source = CreateGameObject("Source");
            TargetColliders target = CreateTarget("Target");
            EffectBundle bundle = CreateBundle();
            var service = new RecordingEffectService();
            var tracker = new ObstacleContactEffects(source, _ => true);

            tracker.Enter(target.First);
            tracker.Enter(target.Second);
            tracker.Tick(0f, service, bundle, 10f, 1f);

            tracker.Exit(target.Second);
            tracker.Tick(1f, service, bundle, 10f, 1f);

            tracker.Exit(target.First);
            tracker.Tick(1f, service, bundle, 10f, 1f);

            Assert.That(service.ApplyCount, Is.EqualTo(2));
        }

        [Test]
        public void Clear_StopsTrackedContactEffects()
        {
            GameObject source = CreateGameObject("Source");
            TargetColliders target = CreateTarget("Target");
            EffectBundle bundle = CreateBundle();
            var service = new RecordingEffectService();
            var tracker = new ObstacleContactEffects(source, _ => true);

            tracker.Enter(target.First);
            tracker.Clear();
            tracker.Tick(1f, service, bundle, 10f, 1f);

            Assert.That(service.ApplyCount, Is.Zero);
        }

        [Test]
        public void RejectedTarget_IsNotTracked()
        {
            GameObject source = CreateGameObject("Source");
            TargetColliders target = CreateTarget("Target");
            EffectBundle bundle = CreateBundle();
            var service = new RecordingEffectService();
            var tracker = new ObstacleContactEffects(source, _ => false);

            tracker.Enter(target.First);
            tracker.Tick(1f, service, bundle, 10f, 1f);

            Assert.That(service.ApplyCount, Is.Zero);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _gameObjects.Add(gameObject);
            return gameObject;
        }

        private TargetColliders CreateTarget(string name)
        {
            GameObject root = CreateGameObject(name);
            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;

            var firstChild = new GameObject("First Collider");
            firstChild.transform.SetParent(root.transform);
            Collider first = firstChild.AddComponent<BoxCollider>();

            var secondChild = new GameObject("Second Collider");
            secondChild.transform.SetParent(root.transform);
            Collider second = secondChild.AddComponent<BoxCollider>();

            return new TargetColliders(first, second);
        }

        private EffectBundle CreateBundle()
        {
            EffectBundle bundle = ScriptableObject.CreateInstance<EffectBundle>();
            _scriptableObjects.Add(bundle);
            return bundle;
        }

        private sealed class RecordingEffectService : IEffectApplicationService
        {
            public int ApplyCount { get; private set; }

            public void Apply(EffectBundle bundle, EffectContext context)
            {
                ApplyCount++;
            }

            public void Remove(EffectBundle bundle, EffectContext context)
            {
            }
        }

        private readonly struct TargetColliders
        {
            public Collider First { get; }
            public Collider Second { get; }

            public TargetColliders(Collider first, Collider second)
            {
                First = first;
                Second = second;
            }
        }
    }
}
