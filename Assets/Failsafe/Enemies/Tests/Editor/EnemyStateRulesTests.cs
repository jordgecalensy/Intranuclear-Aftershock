using System;
using System.Reflection;
using Failsafe.Enemies.Sensors;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Enemies.Tests
{
    [TestFixture]
    public sealed class EnemyStateRulesTests
    {
        [Test]
        public void AwarenessMeter_PlayerIsLostAfterCrossingExitThreshold()
        {
            Enemy_ScriptableObject config = CreateConfig();

            try
            {
                var awarenessMeter = new AwarenessMeter(Array.Empty<Sensor>(), config);
                awarenessMeter.Initialize();

                SetField(awarenessMeter, "_hasEverChased", true);
                SetField(awarenessMeter, "_alertness", config.ChaseExitThreshold - 0.1f);

                Assert.That(awarenessMeter.IsPlayerLost(), Is.True);
                Assert.That(awarenessMeter.IsPlayerLost(), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CheckState_EndsAtConfiguredDuration()
        {
            Enemy_ScriptableObject config = CreateConfig();

            try
            {
                var checkState = new CheckState(
                    Array.Empty<Sensor>(),
                    null,
                    null,
                    null,
                    config,
                    null);

                SetField(checkState, "_checkTimer", config.CheckDuration);

                Assert.That(checkState.CheckEnd(), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void PatrolState_SkipsMissingManualPoints()
        {
            var validPointObject = new GameObject("Valid patrol point");

            try
            {
                var patrolState = new PatrolState(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    new[] { null, validPointObject.transform, null });

                MethodInfo method = typeof(PatrolState).GetMethod(
                    "TryGetNextManualPoint",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var arguments = new object[] { null };

                Assert.That(method, Is.Not.Null);
                Assert.That(method.Invoke(patrolState, arguments), Is.True);
                Assert.That(arguments[0], Is.SameAs(validPointObject.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(validPointObject);
            }
        }

        [Test]
        public void EnemyDeathState_UsesDeathAnimatorState()
        {
            var deathState = new EnemyDeathState(null, null, null, null);
            int stateHash = GetField<int>(deathState, "_deathStateHash");

            Assert.That(stateHash, Is.EqualTo(Animator.StringToHash("Death")));
        }

        private static Enemy_ScriptableObject CreateConfig()
        {
            Enemy_ScriptableObject config = ScriptableObject.CreateInstance<Enemy_ScriptableObject>();
            config.CheckDuration = 5f;
            config.ChaseExitThreshold = 30f;
            return config;
        }

        private static void SetField<T>(object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }
    }
}
