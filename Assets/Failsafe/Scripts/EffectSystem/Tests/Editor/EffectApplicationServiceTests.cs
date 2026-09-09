using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Failsafe.Scripts.EffectSystem.Tests
{
    [TestFixture]
    public sealed class EffectApplicationServiceTests
    {
        private static readonly FieldInfo BundleEffectsField =
            typeof(EffectBundle).GetField(
                "_effects",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<EffectApplicationService> _services = new();
        private readonly List<UnityEngine.Object> _unityObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _services.Count - 1; i >= 0; i--)
                _services[i].Dispose();

            _services.Clear();

            for (int i = _unityObjects.Count - 1; i >= 0; i--)
            {
                if (_unityObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_unityObjects[i]);
            }

            _unityObjects.Clear();
        }

        [Test]
        public void Apply_StartsEffectAndPublishesItAsActive()
        {
            EffectApplicationService service = CreateService();
            TestEffectDefinition definition = CreateDefinition();
            EffectBundle bundle = CreateBundle(definition);
            GameObject target = CreateTarget("Target");
            EffectContext context = CreateContext(target);
            EffectPresentation addedPresentation = null;

            service.EffectAdded += presentation =>
                addedPresentation = presentation;

            service.Apply(bundle, context);

            List<EffectPresentation> activeEffects = GetActiveEffects(service);

            Assert.That(definition.CreateCount, Is.EqualTo(1));
            Assert.That(definition.CreatedEffects[0].ApplyCount, Is.EqualTo(1));
            Assert.That(activeEffects, Has.Count.EqualTo(1));
            Assert.That(activeEffects[0], Is.SameAs(addedPresentation));
            Assert.That(activeEffects[0].Definition, Is.SameAs(definition));
            Assert.That(activeEffects[0].Target, Is.SameAs(target));
        }

        [Test]
        public void Apply_WhenUniqueEffectIsReapplied_RefreshesWithoutDuplicate()
        {
            EffectApplicationService service = CreateService();
            TestEffectDefinition definition = CreateDefinition(isUnique: true);
            EffectBundle bundle = CreateBundle(definition);
            GameObject target = CreateTarget("Target");
            EffectContext context = CreateContext(target);
            int addedCount = 0;
            int refreshedCount = 0;

            service.EffectAdded += _ => addedCount++;
            service.EffectRefreshed += _ => refreshedCount++;

            service.Apply(bundle, context);
            service.Apply(bundle, context);

            List<EffectPresentation> activeEffects = GetActiveEffects(service);
            TestEffect activeEffect = definition.CreatedEffects[0];
            TestEffect incomingEffect = definition.CreatedEffects[1];

            Assert.That(definition.CreateCount, Is.EqualTo(2));
            Assert.That(activeEffects, Has.Count.EqualTo(1));
            Assert.That(addedCount, Is.EqualTo(1));
            Assert.That(refreshedCount, Is.EqualTo(1));
            Assert.That(activeEffect.ApplyCount, Is.EqualTo(1));
            Assert.That(activeEffect.ReapplyCount, Is.EqualTo(1));
            Assert.That(incomingEffect.ApplyCount, Is.Zero);
        }

        [Test]
        public void Remove_RemovesOnlyMatchingDefinitionFromMatchingTarget()
        {
            EffectApplicationService service = CreateService();
            TestEffectDefinition firstDefinition = CreateDefinition();
            TestEffectDefinition secondDefinition = CreateDefinition();
            EffectBundle allEffects = CreateBundle(
                firstDefinition,
                secondDefinition);
            EffectBundle firstEffectOnly = CreateBundle(firstDefinition);
            GameObject firstTarget = CreateTarget("First Target");
            GameObject secondTarget = CreateTarget("Second Target");
            EffectContext firstContext = CreateContext(firstTarget);
            EffectContext secondContext = CreateContext(secondTarget);
            EffectPresentation removedPresentation = null;

            service.EffectRemoved += presentation =>
                removedPresentation = presentation;

            service.Apply(allEffects, firstContext);
            service.Apply(allEffects, secondContext);
            service.Remove(firstEffectOnly, firstContext);

            List<EffectPresentation> activeEffects = GetActiveEffects(service);

            Assert.That(activeEffects, Has.Count.EqualTo(3));
            Assert.That(
                activeEffects.Exists(
                    effect =>
                        effect.Target == firstTarget &&
                        effect.Definition == firstDefinition),
                Is.False);
            Assert.That(
                activeEffects.Exists(
                    effect =>
                        effect.Target == firstTarget &&
                        effect.Definition == secondDefinition),
                Is.True);
            Assert.That(
                activeEffects.FindAll(effect => effect.Target == secondTarget),
                Has.Count.EqualTo(2));
            Assert.That(removedPresentation.Target, Is.SameAs(firstTarget));
            Assert.That(
                removedPresentation.Definition,
                Is.SameAs(firstDefinition));
            Assert.That(firstDefinition.CreatedEffects[0].ClearCount, Is.EqualTo(1));
            Assert.That(firstDefinition.CreatedEffects[1].ClearCount, Is.Zero);
        }

        [Test]
        public void Apply_WhenSubscriberThrows_NotifiesRemainingSubscribers()
        {
            EffectApplicationService service = CreateService();
            TestEffectDefinition definition = CreateDefinition();
            EffectBundle bundle = CreateBundle(definition);
            GameObject target = CreateTarget("Target");
            int successfulSubscriberCalls = 0;

            service.EffectAdded += _ =>
                throw new InvalidOperationException(
                    "Expected subscriber failure.");
            service.EffectAdded += _ => successfulSubscriberCalls++;

            LogAssert.Expect(
                LogType.Exception,
                new Regex("InvalidOperationException: Expected subscriber failure\\."));

            service.Apply(bundle, CreateContext(target));

            Assert.That(successfulSubscriberCalls, Is.EqualTo(1));
            Assert.That(GetActiveEffects(service), Has.Count.EqualTo(1));
        }

        [Test]
        public void Dispose_ClearsAllActiveEffectsAndPublishesRemovals()
        {
            EffectApplicationService service = CreateService();
            TestEffectDefinition firstDefinition = CreateDefinition();
            TestEffectDefinition secondDefinition = CreateDefinition();
            EffectBundle bundle = CreateBundle(
                firstDefinition,
                secondDefinition);
            GameObject target = CreateTarget("Target");
            int removedCount = 0;

            service.EffectRemoved += _ => removedCount++;
            service.Apply(bundle, CreateContext(target));

            service.Dispose();

            Assert.That(GetActiveEffects(service), Is.Empty);
            Assert.That(removedCount, Is.EqualTo(2));
            Assert.That(firstDefinition.CreatedEffects[0].ClearCount, Is.EqualTo(1));
            Assert.That(secondDefinition.CreatedEffects[0].ClearCount, Is.EqualTo(1));
        }

        [Test]
        public void Apply_WhenDefinitionRejectsContext_DoesNotCreateEffect()
        {
            EffectApplicationService service = CreateService();
            TestEffectDefinition definition = CreateDefinition();
            definition.CanApplyResult = false;
            EffectBundle bundle = CreateBundle(definition);
            GameObject target = CreateTarget("Target");
            int addedCount = 0;

            service.EffectAdded += _ => addedCount++;

            service.Apply(bundle, CreateContext(target));

            Assert.That(definition.CreateCount, Is.Zero);
            Assert.That(addedCount, Is.Zero);
            Assert.That(GetActiveEffects(service), Is.Empty);
        }

        private EffectApplicationService CreateService()
        {
            var service = new EffectApplicationService(
                statusReactionService: null);

            _services.Add(service);
            return service;
        }

        private TestEffectDefinition CreateDefinition(
            bool isUnique = false)
        {
            TestEffectDefinition definition =
                ScriptableObject.CreateInstance<TestEffectDefinition>();

            definition.IsUnique = isUnique;
            _unityObjects.Add(definition);
            return definition;
        }

        private EffectBundle CreateBundle(
            params EffectDefinition[] definitions)
        {
            Assert.That(
                BundleEffectsField,
                Is.Not.Null,
                "EffectBundle._effects field was not found.");

            EffectBundle bundle =
                ScriptableObject.CreateInstance<EffectBundle>();

            BundleEffectsField.SetValue(bundle, definitions);
            _unityObjects.Add(bundle);
            return bundle;
        }

        private GameObject CreateTarget(string name)
        {
            var target = new GameObject(name);
            _unityObjects.Add(target);
            return target;
        }

        private static EffectContext CreateContext(GameObject target)
        {
            return new EffectContext(
                source: null,
                hitCollider: null,
                point: Vector3.zero,
                normal: Vector3.up,
                direction: Vector3.forward,
                targetOverride: target);
        }

        private static List<EffectPresentation> GetActiveEffects(
            EffectApplicationService service)
        {
            var activeEffects = new List<EffectPresentation>();
            service.GetActiveEffects(activeEffects);
            return activeEffects;
        }

        private sealed class TestEffectDefinition : EffectDefinition
        {
            public bool CanApplyResult { get; set; } = true;
            public bool IsUnique { get; set; }
            public int CreateCount { get; private set; }
            public List<TestEffect> CreatedEffects { get; } = new();

            public override bool CanApply(EffectContext context)
            {
                return CanApplyResult;
            }

            public override Effect CreateEffect(EffectContext context)
            {
                CreateCount++;

                var effect = new TestEffect(
                    isUnique: IsUnique,
                    duration: 60f);

                CreatedEffects.Add(effect);
                return effect;
            }

            public override string GetStackKey(EffectContext context)
            {
                return $"test-effect.{GetInstanceID()}";
            }
        }

        private sealed class TestEffect : Effect, IReapplicableEffect
        {
            public int ApplyCount { get; private set; }
            public int ClearCount { get; private set; }
            public int ReapplyCount { get; private set; }

            public TestEffect(bool isUnique, float duration)
            {
                IsUniqueEffect = isUnique;
                _duration = duration;
            }

            public override void ApplyEffect()
            {
                ApplyCount++;
            }

            public override void ClearEffect()
            {
                ClearCount++;
            }

            public void OnReapply(Effect newEffect)
            {
                ReapplyCount++;
            }
        }
    }
}
