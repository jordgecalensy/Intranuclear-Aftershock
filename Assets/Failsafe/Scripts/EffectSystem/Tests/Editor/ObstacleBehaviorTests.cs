using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Tests
{
    [TestFixture]
    public sealed class ObstacleBehaviorTests
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
        public void Motion_WhenWaypointIsReached_WaitsBeforeAdvancing()
        {
            GameObject obstacle = CreateGameObject("Obstacle");
            Rigidbody rigidbody = obstacle.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;

            GameObject firstWaypoint = CreateGameObject("First Waypoint");
            firstWaypoint.transform.position = obstacle.transform.position;
            GameObject secondWaypoint = CreateGameObject("Second Waypoint");
            secondWaypoint.transform.position = Vector3.right * 5f;

            var waypoints = new List<Transform>
            {
                firstWaypoint.transform,
                secondWaypoint.transform
            };
            var motion = new ObstacleMotion(rigidbody, obstacle.transform);

            motion.Tick(
                0.02f,
                false,
                Vector3.up,
                90f,
                true,
                3f,
                0.1f,
                waypoints);

            Assert.That(motion.CurrentWaypointIndex, Is.Zero);

            motion.Tick(
                0.11f,
                false,
                Vector3.up,
                90f,
                true,
                3f,
                0.1f,
                waypoints);

            Assert.That(motion.CurrentWaypointIndex, Is.EqualTo(1));
        }

        [Test]
        public void Motion_WhenCurrentWaypointIsMissing_SkipsIt()
        {
            GameObject obstacle = CreateGameObject("Obstacle");
            Rigidbody rigidbody = obstacle.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            GameObject validWaypoint = CreateGameObject("Valid Waypoint");

            var waypoints = new List<Transform>
            {
                null,
                validWaypoint.transform
            };
            var motion = new ObstacleMotion(rigidbody, obstacle.transform);

            motion.Tick(
                0.02f,
                false,
                Vector3.up,
                90f,
                true,
                3f,
                0f,
                waypoints);

            Assert.That(motion.CurrentWaypointIndex, Is.EqualTo(1));
        }

        [Test]
        public void ActivityCycle_WhenDurationsExpire_TogglesTriggerAndVisual()
        {
            GameObject obstacle = CreateGameObject("Obstacle");
            Collider trigger = obstacle.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            GameObject visual = CreateGameObject("Visual");
            visual.transform.SetParent(obstacle.transform);
            var cycle = new ObstacleActivityCycle(
                obstacle,
                trigger,
                visual);
            cycle.Initialize(0.5f);

            bool becameInactive = cycle.Tick(0.5f, true, 0.5f, 0.25f);

            Assert.That(becameInactive, Is.True);
            Assert.That(cycle.IsActive, Is.False);
            Assert.That(trigger.enabled, Is.False);
            Assert.That(visual.activeSelf, Is.False);

            cycle.Tick(0.25f, true, 0.5f, 0.25f);

            Assert.That(cycle.IsActive, Is.True);
            Assert.That(trigger.enabled, Is.True);
            Assert.That(visual.activeSelf, Is.True);
        }

        [Test]
        public void ActivityCycle_WhenOwnerIsVisual_DoesNotDisableOwner()
        {
            GameObject obstacle = CreateGameObject("Obstacle");
            Collider trigger = obstacle.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            Renderer renderer = obstacle.AddComponent<MeshRenderer>();
            var cycle = new ObstacleActivityCycle(
                obstacle,
                trigger,
                obstacle);
            cycle.Initialize(0.5f);

            cycle.Tick(0.5f, true, 0.5f, 0.25f);

            Assert.That(obstacle.activeSelf, Is.True);
            Assert.That(renderer.enabled, Is.False);

            cycle.Tick(0.25f, true, 0.5f, 0.25f);

            Assert.That(renderer.enabled, Is.True);
        }

        [Test]
        public void Passenger_MultipleColliders_RestoreParentAfterLastExit()
        {
            PassengerSetup setup = CreatePassengerSetup();
            var attachment = new ObstaclePassengerAttachment(
                setup.Obstacle.transform,
                setup.Trigger,
                _ => true);

            attachment.Enter(setup.First, true, 0.15f);
            attachment.Enter(setup.Second, true, 0.15f);

            Assert.That(
                setup.Passenger.transform.parent,
                Is.EqualTo(setup.Obstacle.transform));

            attachment.Exit(setup.Second);

            Assert.That(
                setup.Passenger.transform.parent,
                Is.EqualTo(setup.Obstacle.transform));

            attachment.Exit(setup.First);

            Assert.That(
                setup.Passenger.transform.parent,
                Is.EqualTo(setup.OriginalParent.transform));
        }

        [Test]
        public void Passenger_Clear_RestoresOriginalParent()
        {
            PassengerSetup setup = CreatePassengerSetup();
            var attachment = new ObstaclePassengerAttachment(
                setup.Obstacle.transform,
                setup.Trigger,
                _ => true);

            attachment.Enter(setup.First, true, 0.15f);
            attachment.Clear();

            Assert.That(
                setup.Passenger.transform.parent,
                Is.EqualTo(setup.OriginalParent.transform));
        }

        [Test]
        public void TargetFilter_UsesConfiguredPlayerLayer()
        {
            GameObject target = CreateGameObject("Target");
            target.layer = 6;
            var filter = new ObstacleTargetFilter(
                true,
                false,
                1 << 6,
                0,
                "Player",
                "Enemy");

            Assert.That(filter.IsAllowed(target), Is.True);

            target.layer = 7;

            Assert.That(filter.IsAllowed(target), Is.False);
        }

        [Test]
        public void PhysicsSetup_ConfiguresExistingTriggerAndRigidbody()
        {
            GameObject obstacle = CreateGameObject("Obstacle");
            Collider configuredTrigger = obstacle.AddComponent<BoxCollider>();
            Rigidbody configuredRigidbody = obstacle.AddComponent<Rigidbody>();
            configuredRigidbody.isKinematic = false;
            configuredRigidbody.useGravity = true;

            Collider trigger = ObstaclePhysicsSetup.PrepareContactTrigger(
                obstacle,
                configuredTrigger);
            Rigidbody rigidbody = ObstaclePhysicsSetup.PrepareRigidbody(
                obstacle,
                false);

            Assert.That(trigger, Is.SameAs(configuredTrigger));
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(rigidbody, Is.SameAs(configuredRigidbody));
            Assert.That(rigidbody.isKinematic, Is.True);
            Assert.That(rigidbody.useGravity, Is.False);
        }

        [Test]
        public void Stasis_TimedStateExpiresAtConfiguredTime()
        {
            var stasis = new ObstacleStasis();

            stasis.Apply(2f, 10f);
            stasis.Tick(11.9f);

            Assert.That(stasis.IsFrozen, Is.True);

            stasis.Tick(12f);

            Assert.That(stasis.IsFrozen, Is.False);
        }

        [Test]
        public void Stasis_ManualStateCancelsTimedExpiration()
        {
            var stasis = new ObstacleStasis();

            stasis.Apply(1f, 0f);
            stasis.Set(true);
            stasis.Tick(10f);

            Assert.That(stasis.IsFrozen, Is.True);

            stasis.Set(false);

            Assert.That(stasis.IsFrozen, Is.False);
        }

        private PassengerSetup CreatePassengerSetup()
        {
            GameObject obstacle = CreateGameObject("Obstacle");
            Collider trigger = obstacle.AddComponent<BoxCollider>();
            trigger.isTrigger = true;

            GameObject originalParent = CreateGameObject("Original Parent");
            GameObject passenger = CreateGameObject("Passenger");
            passenger.transform.SetParent(originalParent.transform);
            passenger.transform.position = Vector3.up;

            Rigidbody rigidbody = passenger.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;

            var firstObject = new GameObject("First Collider");
            firstObject.transform.SetParent(passenger.transform);
            Collider first = firstObject.AddComponent<BoxCollider>();

            var secondObject = new GameObject("Second Collider");
            secondObject.transform.SetParent(passenger.transform);
            Collider second = secondObject.AddComponent<BoxCollider>();

            return new PassengerSetup(
                obstacle,
                trigger,
                originalParent,
                passenger,
                first,
                second);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _gameObjects.Add(gameObject);
            return gameObject;
        }

        private readonly struct PassengerSetup
        {
            public GameObject Obstacle { get; }
            public Collider Trigger { get; }
            public GameObject OriginalParent { get; }
            public GameObject Passenger { get; }
            public Collider First { get; }
            public Collider Second { get; }

            public PassengerSetup(
                GameObject obstacle,
                Collider trigger,
                GameObject originalParent,
                GameObject passenger,
                Collider first,
                Collider second)
            {
                Obstacle = obstacle;
                Trigger = trigger;
                OriginalParent = originalParent;
                Passenger = passenger;
                First = first;
                Second = second;
            }
        }
    }
}
