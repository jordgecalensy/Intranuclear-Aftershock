using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Tests
{
    [TestFixture]
    public sealed class SharedEffectMechanicsTests
    {
        private GameObject _owner;
        private GameObject _target;

        [TearDown]
        public void TearDown()
        {
            if (_owner != null)
                Object.DestroyImmediate(_owner);

            if (_target != null)
                Object.DestroyImmediate(_target);
        }

        [Test]
        public void ColliderTargetResolver_MultipleChildCollidersShareRigidbodyRoot()
        {
            _target = new GameObject("Target");
            Rigidbody rigidbody = _target.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;

            GameObject firstChild = new GameObject("First Collider");
            firstChild.transform.SetParent(_target.transform);
            Collider first = firstChild.AddComponent<BoxCollider>();

            GameObject secondChild = new GameObject("Second Collider");
            secondChild.transform.SetParent(_target.transform);
            Collider second = secondChild.AddComponent<BoxCollider>();

            Assert.That(
                ColliderTargetResolver.ResolveRoot(first),
                Is.SameAs(_target.transform));
            Assert.That(
                ColliderTargetResolver.ResolveRoot(second),
                Is.SameAs(_target.transform));
        }

        [Test]
        public void ContactContext_UsesResolvedDirectionAndPower()
        {
            _owner = new GameObject("Source");
            _target = new GameObject("Target");
            _target.transform.position = Vector3.right * 2f;
            Collider collider = _target.AddComponent<BoxCollider>();

            EffectContext context = ContactEffectContextFactory.Create(
                _owner,
                collider,
                _target.transform,
                7f);

            Assert.That(context.Source, Is.SameAs(_owner));
            Assert.That(context.HitCollider, Is.SameAs(collider));
            Assert.That(context.Power, Is.EqualTo(7f));
            Assert.That(context.Direction, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void SpatialPropagation_RespectsIntervalAndChildLimit()
        {
            int spawnCount = 0;
            var propagation = new SpatialPropagation(
                _ =>
                {
                    spawnCount++;
                    return true;
                });
            propagation.Initialize(10f, 2f);

            bool beforeInterval = propagation.Tick(
                11.9f,
                true,
                2f,
                1f,
                1,
                Vector3.zero,
                Vector3.forward,
                1f,
                2f,
                0,
                0f,
                0f);
            bool firstAttempt = propagation.Tick(
                12f,
                true,
                2f,
                1f,
                1,
                Vector3.zero,
                Vector3.forward,
                1f,
                2f,
                0,
                0f,
                0f);
            bool afterLimit = propagation.Tick(
                20f,
                true,
                2f,
                1f,
                1,
                Vector3.zero,
                Vector3.forward,
                1f,
                2f,
                0,
                0f,
                0f);

            Assert.That(beforeInterval, Is.False);
            Assert.That(firstAttempt, Is.True);
            Assert.That(afterLimit, Is.False);
            Assert.That(spawnCount, Is.EqualTo(1));
            Assert.That(propagation.SpawnedCount, Is.EqualTo(1));
        }

        [Test]
        public void EffectServiceResolver_SetServiceAvoidsSceneSearch()
        {
            _owner = new GameObject("Owner");
            var service = new RecordingEffectService();
            var resolver = new EffectApplicationServiceResolver(_owner);

            resolver.Set(service);
            resolver.TryResolve(0f, true);

            Assert.That(resolver.Service, Is.SameAs(service));
        }

        private sealed class RecordingEffectService :
            IEffectApplicationService
        {
            public void Apply(EffectBundle bundle, EffectContext context)
            {
            }

            public void Remove(EffectBundle bundle, EffectContext context)
            {
            }
        }
    }
}
