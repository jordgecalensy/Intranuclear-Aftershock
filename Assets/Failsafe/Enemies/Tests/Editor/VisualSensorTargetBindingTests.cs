using System.Collections.Generic;
using System.Reflection;
using Failsafe.Player.View;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Enemies.Tests
{
    [TestFixture]
    public sealed class VisualSensorTargetBindingTests
    {
        private readonly List<GameObject> _gameObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _gameObjects.Count - 1; i >= 0; i--)
            {
                if (_gameObjects[i] != null)
                    Object.DestroyImmediate(_gameObjects[i]);
            }

            _gameObjects.Clear();
        }

        [Test]
        public void PlayerView_ReturnsChestBeforeOtherSensorPoints()
        {
            GameObject player = CreateGameObject("Player");
            PlayerView playerView = player.AddComponent<PlayerView>();
            playerView.PlayerTransform = player.transform;

            Transform head = CreateChild(player.transform, "Sensor_Point_Head");
            Transform chest = CreateChild(player.transform, "Sensor_Point_Chest");
            Transform leg = CreateChild(player.transform, "Sensor_Point_LegL");

            bool resolved = playerView.TryGetEnemySensorTargets(
                out Transform targetRoot,
                out Transform chestTarget,
                out IReadOnlyList<Transform> targets);

            Assert.That(resolved, Is.True);
            Assert.That(targetRoot, Is.SameAs(player.transform));
            Assert.That(chestTarget, Is.SameAs(chest));
            Assert.That(targets.Count, Is.EqualTo(3));
            Assert.That(targets[0], Is.SameAs(chest));
            CollectionAssert.Contains(targets, head);
            CollectionAssert.Contains(targets, leg);
        }

        [Test]
        public void VisualSensor_BindsPlayerTargetsAndKeepsChestFirst()
        {
            GameObject player = CreateGameObject("Player");
            PlayerView playerView = player.AddComponent<PlayerView>();
            playerView.PlayerTransform = player.transform;

            CreateChild(player.transform, "Sensor_Point_Head");
            Transform chest = CreateChild(player.transform, "Sensor_Point_Chest");

            GameObject enemy = CreateGameObject("Enemy");
            VisualSensor sensor = enemy.AddComponent<VisualSensor>();

            MethodInfo bindMethod = typeof(VisualSensor).GetMethod(
                "TryBindPlayerTargets",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(bindMethod, Is.Not.Null);

            bool bound = (bool)bindMethod.Invoke(
                sensor,
                new object[] { playerView });
            Transform[] targets = GetTargets(sensor);

            Assert.That(bound, Is.True);
            Assert.That(sensor.Target, Is.SameAs(player.transform));
            Assert.That(targets[0], Is.SameAs(chest));
        }

        [Test]
        public void VisualSensor_IgnoresNullSerializedTargets()
        {
            GameObject enemy = CreateGameObject("Enemy");
            VisualSensor sensor = enemy.AddComponent<VisualSensor>();

            SetTargets(sensor, new Transform[] { null, null });

            Assert.DoesNotThrow(
                () => sensor.GetBestVisiblePointWithChestOverride());
            Assert.That(
                sensor.GetBestVisiblePointWithChestOverride(),
                Is.Null);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _gameObjects.Add(gameObject);
            return gameObject;
        }

        private Transform CreateChild(Transform parent, string name)
        {
            GameObject child = CreateGameObject(name);
            child.transform.SetParent(parent);
            return child.transform;
        }

        private static Transform[] GetTargets(VisualSensor sensor)
        {
            return (Transform[])GetTargetsField().GetValue(sensor);
        }

        private static void SetTargets(
            VisualSensor sensor,
            Transform[] targets)
        {
            GetTargetsField().SetValue(sensor, targets);
        }

        private static FieldInfo GetTargetsField()
        {
            return typeof(VisualSensor).GetField(
                "_targets",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
