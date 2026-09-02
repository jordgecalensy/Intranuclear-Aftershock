using System;
using System.Reflection;
using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Destruction;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Tests
{
    [TestFixture]
    public sealed class BreakableObjectTests
    {
        private GameObject _owner;
        private GameObject _intactRoot;
        private GameObject _fragmentsRoot;
        private BreakableObject _breakable;

        [TearDown]
        public void TearDown()
        {
            if (_owner != null)
                UnityEngine.Object.DestroyImmediate(_owner);
        }

        [Test]
        public void DamageInfo_ReducesHealthAndBreaksAtZero()
        {
            CreateBreakable(maxHealth: 10f);
            Rigidbody fragment = CreateFragmentRigidbody();

            _breakable.TakeDamage(new DamageInfo(4f, DamageType.Physical));

            Assert.That(_breakable.CurrentHealth, Is.EqualTo(6f));
            Assert.That(_breakable.IsBroken, Is.False);
            Assert.That(_intactRoot.activeSelf, Is.True);
            Assert.That(_fragmentsRoot.activeSelf, Is.False);

            _breakable.TakeDamage(new DamageInfo(6f, DamageType.Physical));

            Assert.That(_breakable.CurrentHealth, Is.Zero);
            Assert.That(_breakable.IsBroken, Is.True);
            Assert.That(_intactRoot.activeSelf, Is.False);
            Assert.That(_fragmentsRoot.activeSelf, Is.True);
            Assert.That(fragment.isKinematic, Is.False);
        }

        [Test]
        public void LegacyDamageTypes_AllReduceLocalHealth()
        {
            CreateBreakable(maxHealth: 20f);

            _breakable.TakeDamage(new FlatDamage(2f));
            _breakable.TakeDamage(new FireContactDamage(3f));
            _breakable.TakeDamage(new FireDotTickDamage(4f, 1f));
            _breakable.TakeDamage(new FireDamage(5f));

            Assert.That(_breakable.CurrentHealth, Is.EqualTo(6f));
            Assert.That(_breakable.IsBroken, Is.False);
        }

        [Test]
        public void Break_WhenCalledRepeatedly_RaisesEventOnce()
        {
            CreateBreakable(maxHealth: 10f);
            int brokenCount = 0;
            _breakable.OnBroken += () => brokenCount++;

            _breakable.Break();
            _breakable.Break();
            _breakable.TakeDamage(new FlatDamage(10f));

            Assert.That(brokenCount, Is.EqualTo(1));
            Assert.That(_breakable.IsBroken, Is.True);
        }

        [Test]
        public void DamageTargetResolver_FindsBreakableOnColliderParent()
        {
            CreateBreakable(maxHealth: 10f);
            GameObject colliderObject = new GameObject("Collider");
            colliderObject.transform.SetParent(_intactRoot.transform);
            Collider collider = colliderObject.AddComponent<BoxCollider>();

            bool resolved = DamageTargetResolver.TryResolve(
                collider,
                out DamageTarget target);

            Assert.That(resolved, Is.True);

            target.TakeDamage(new DamageInfo(3f, DamageType.Physical));

            Assert.That(_breakable.CurrentHealth, Is.EqualTo(7f));
        }

        private void CreateBreakable(float maxHealth)
        {
            _owner = new GameObject("Breakable Owner");
            _owner.SetActive(false);

            _intactRoot = new GameObject("Intact Root");
            _intactRoot.transform.SetParent(_owner.transform);

            _fragmentsRoot = new GameObject("Fragments Root");
            _fragmentsRoot.transform.SetParent(_owner.transform);

            _breakable = _owner.AddComponent<BreakableObject>();
            SetField("_maxHealth", maxHealth);
            SetField("_intactRoot", _intactRoot);
            SetField("_fragmentsRoot", _fragmentsRoot);
            SetField("_destroyAfterLifetime", false);

            _owner.SetActive(true);
        }

        private Rigidbody CreateFragmentRigidbody()
        {
            GameObject fragmentObject = new GameObject("Fragment");
            fragmentObject.transform.SetParent(_fragmentsRoot.transform);
            Rigidbody rigidbody = fragmentObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            return rigidbody;
        }

        private void SetField(string fieldName, object value)
        {
            FieldInfo field = typeof(BreakableObject).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"{nameof(BreakableObject)}.{fieldName} was not found.");

            field.SetValue(_breakable, value);
        }
    }
}
