using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Enemies.Tests
{
    [TestFixture]
    public sealed class LaserBeamRuntimeTests
    {
        [Test]
        public void SmoothAimPoint_LagsBehindMovingTargetAndConverges()
        {
            Vector3 current = Vector3.zero;
            Vector3 target = Vector3.right * 10f;
            Vector3 velocity = Vector3.zero;

            current = LaserBeamController.SmoothAimPoint(
                current,
                target,
                ref velocity,
                smoothTime: 0.3f,
                maximumSpeed: 20f,
                deltaTime: 0.02f);

            Assert.That(current.x, Is.GreaterThan(0f));
            Assert.That(current.x, Is.LessThan(target.x));

            for (int index = 0; index < 200; index++)
            {
                current = LaserBeamController.SmoothAimPoint(
                    current,
                    target,
                    ref velocity,
                    smoothTime: 0.3f,
                    maximumSpeed: 20f,
                    deltaTime: 0.02f);
            }

            Assert.That(Vector3.Distance(current, target), Is.LessThan(0.01f));
        }

        [Test]
        public void BuildEffectiveRaycastMask_AddsPhysicalAndDestructibleLayers()
        {
            int mask = LaserBeamController.BuildEffectiveRaycastMask(
                baseMask: 1 << 6,
                carryObjectsLayer: 18,
                destructibleLayer: 19);

            Assert.That(mask & (1 << 6), Is.Not.Zero);
            Assert.That(mask & (1 << 18), Is.Not.Zero);
            Assert.That(mask & (1 << 19), Is.Not.Zero);
        }

        [Test]
        public void CalculateTickPower_ConvertsRatesToPerTickValues()
        {
            Assert.That(
                Projectiles.LaserBeamStrategy.CalculateTickPower(10f, 0.1f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                Projectiles.LaserBeamStrategy.CalculateTickPower(5f, 0.1f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                Projectiles.LaserBeamStrategy.CalculateTickPower(-5f, 0.1f),
                Is.Zero);
        }

        [Test]
        public void ClosestPointOnSegment_ClampsToVisibleBeam()
        {
            Vector3 start = Vector3.zero;
            Vector3 end = Vector3.forward * 10f;

            Assert.That(
                Projectiles.LaserBeamStrategy.ClosestPointOnSegment(
                    start,
                    end,
                    new Vector3(2f, 0f, 4f)),
                Is.EqualTo(new Vector3(0f, 0f, 4f)));

            Assert.That(
                Projectiles.LaserBeamStrategy.ClosestPointOnSegment(
                    start,
                    end,
                    Vector3.forward * 12f),
                Is.EqualTo(end));
        }

        [Test]
        public void CalculateBeamFieldFalloff_DecreasesTowardRadiusEdge()
        {
            Assert.That(
                Projectiles.LaserBeamStrategy.CalculateBeamFieldFalloff(0f, 1.25f, 1f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                Projectiles.LaserBeamStrategy.CalculateBeamFieldFalloff(0.625f, 1.25f, 1f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                Projectiles.LaserBeamStrategy.CalculateBeamFieldFalloff(1.25f, 1.25f, 1f),
                Is.Zero);
            Assert.That(
                Projectiles.LaserBeamStrategy.CalculateBeamFieldFalloff(0f, 0f, 1f),
                Is.Zero);
        }
    }
}
