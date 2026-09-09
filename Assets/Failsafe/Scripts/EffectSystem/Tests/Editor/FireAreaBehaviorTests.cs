using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Tests
{
    [TestFixture]
    public sealed class FireAreaBehaviorTests
    {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                Object target = _objects[i];

                if (target != null)
                    Object.DestroyImmediate(target);
            }

            _objects.Clear();
        }

        [Test]
        public void Lifecycle_GrowsThenBurnsDownToSustainIntensity()
        {
            var lifecycle = new FireAreaLifecycle();
            lifecycle.Initialize(1f, 3f, 1f);

            lifecycle.Tick(1f, 3f, 1f, 1f, 2f, 1f, 0.5f);

            Assert.That(lifecycle.Radius, Is.EqualTo(2f));
            Assert.That(lifecycle.Intensity, Is.EqualTo(2f));
            Assert.That(lifecycle.IsBurningOut, Is.True);

            lifecycle.Tick(3f, 3f, 1f, 1f, 2f, 1f, 0.5f);

            Assert.That(lifecycle.Radius, Is.EqualTo(3f));
            Assert.That(lifecycle.Intensity, Is.EqualTo(1f));
        }

        [Test]
        public void Lifecycle_ExtinguishImpulseClampsAtZeroAndStopsGrowth()
        {
            var lifecycle = new FireAreaLifecycle();
            lifecycle.Initialize(1f, 3f, 0.5f);

            lifecycle.AddExtinguishImpulse(2f);
            lifecycle.Tick(1f, 3f, 0f, 5f, 3f, 0f, 0f);

            Assert.That(lifecycle.Intensity, Is.Zero);
            Assert.That(lifecycle.IsBurningOut, Is.True);
        }

        [TestCase(0.5f, FireAreaAdvanced.Tier.Weak)]
        [TestCase(1.5f, FireAreaAdvanced.Tier.Medium)]
        [TestCase(2.5f, FireAreaAdvanced.Tier.Strong)]
        [TestCase(2.9f, FireAreaAdvanced.Tier.Big)]
        public void Lifecycle_UsesConfiguredIntensityTiers(
            float intensity,
            FireAreaAdvanced.Tier expected)
        {
            var lifecycle = new FireAreaLifecycle();
            lifecycle.Initialize(1f, 3f, intensity);

            FireAreaAdvanced.Tier tier = lifecycle.GetTier(1f, 2f, 3f);

            Assert.That(tier, Is.EqualTo(expected));
        }

        [Test]
        public void ContactEffects_MultipleCollidersApplyOnceToOneRoot()
        {
            GameObject source = Track(new GameObject("Fire"));
            GameObject target = Track(new GameObject("Target"));
            target.layer = 31;

            GameObject firstChild = new GameObject("First Collider");
            firstChild.layer = 31;
            firstChild.transform.SetParent(target.transform);
            firstChild.transform.localPosition = Vector3.zero;
            firstChild.AddComponent<BoxCollider>();

            GameObject secondChild = new GameObject("Second Collider");
            secondChild.layer = 31;
            secondChild.transform.SetParent(target.transform);
            secondChild.transform.localPosition = Vector3.zero;
            secondChild.AddComponent<BoxCollider>();

            EffectBundle contactBundle = Track(
                ScriptableObject.CreateInstance<EffectBundle>());
            var service = new RecordingEffectService();
            var contactEffects = new FireAreaContactEffects(source);
            contactEffects.Initialize(0f, 0.1f);
            Physics.SyncTransforms();

            contactEffects.Tick(
                0.1f,
                target.transform.position,
                2f,
                1 << 31,
                64,
                0.1f,
                FireAreaAdvanced.Tier.Weak,
                5f,
                10f,
                20f,
                2f,
                1f,
                2f,
                service,
                contactBundle,
                null);

            Assert.That(service.AppliedContexts.Count, Is.EqualTo(1));
            Assert.That(service.AppliedContexts[0].Power, Is.EqualTo(0.5f));
        }

        private T Track<T>(T target) where T : Object
        {
            _objects.Add(target);
            return target;
        }

        private sealed class RecordingEffectService :
            IEffectApplicationService
        {
            public List<EffectContext> AppliedContexts { get; } = new();

            public void Apply(EffectBundle bundle, EffectContext context)
            {
                AppliedContexts.Add(context);
            }

            public void Remove(EffectBundle bundle, EffectContext context)
            {
            }
        }
    }
}
