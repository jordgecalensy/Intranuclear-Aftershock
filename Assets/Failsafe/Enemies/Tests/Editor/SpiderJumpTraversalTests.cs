using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Enemies.Tests
{
    [TestFixture]
    public sealed class SpiderJumpTraversalTests
    {
        [Test]
        public void EvaluateTrajectory_PreservesEndpointsAndRaisesMidpoint()
        {
            var start = new Vector3(1f, 2f, 3f);
            var end = new Vector3(5f, 4f, 7f);

            Vector3 atStart = SpiderJumpPlanner.EvaluateTrajectory(start, end, 2f, 0f);
            Vector3 atMiddle = SpiderJumpPlanner.EvaluateTrajectory(start, end, 2f, 0.5f);
            Vector3 atEnd = SpiderJumpPlanner.EvaluateTrajectory(start, end, 2f, 1f);

            Assert.That(atStart, Is.EqualTo(start));
            Assert.That(atEnd, Is.EqualTo(end));
            Assert.That(atMiddle, Is.EqualTo(Vector3.Lerp(start, end, 0.5f) + Vector3.up * 2f));
        }

        [Test]
        public void EstimateJumpRouteTime_IncludesWalkingAndJumpPhases()
        {
            float time = SpiderJumpPlanner.EstimateJumpRouteTime(
                approachLength: 6f,
                remainingLength: 3f,
                moveSpeed: 3f,
                preparationDuration: 0.1f,
                flightDuration: 0.8f,
                landingRecoveryDuration: 0.2f);

            Assert.That(time, Is.EqualTo(4.1f).Within(0.0001f));
        }

        [Test]
        public void SavesEnoughTime_RequiresAbsoluteAndRelativeBenefit()
        {
            Assert.That(SpiderJumpPlanner.SavesEnoughTime(10f, 8f, 0.6f, 0.1f), Is.True);
            Assert.That(SpiderJumpPlanner.SavesEnoughTime(10f, 9.5f, 0.6f, 0.01f), Is.False);
            Assert.That(SpiderJumpPlanner.SavesEnoughTime(100f, 99f, 0.5f, 0.1f), Is.False);
            Assert.That(SpiderJumpPlanner.SavesEnoughTime(float.PositiveInfinity, 5f, 1f, 0.5f), Is.True);
        }

        [Test]
        public void SavesEnoughTimeForRoute_AcceptsAnyFasterVerticalShortcut()
        {
            Assert.That(
                SpiderJumpPlanner.SavesEnoughTimeForRoute(10f, 9.8f, 0.6f, 0.1f, true),
                Is.True);
            Assert.That(
                SpiderJumpPlanner.SavesEnoughTimeForRoute(10f, 9.8f, 0.6f, 0.1f, false),
                Is.False);
            Assert.That(
                SpiderJumpPlanner.SavesEnoughTimeForRoute(10f, 10.1f, 0.6f, 0.1f, true),
                Is.False);
        }

        [Test]
        public void CalculateDistanceSampleCount_UsesDenseHalfMeterSteps()
        {
            Assert.That(SpiderJumpPlanner.CalculateDistanceSampleCount(1f, 8f, 0.5f), Is.EqualTo(15));
            Assert.That(SpiderJumpPlanner.CalculateDistanceSampleCount(2f, 2f, 0.5f), Is.EqualTo(1));
        }

        [Test]
        public void CalculateProbeDistance_AccountsForTakeoffInset()
        {
            Assert.That(SpiderJumpPlanner.CalculateProbeDistance(1f, 0.65f), Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(SpiderJumpPlanner.CalculateProbeDistance(8f, 0.65f), Is.EqualTo(7.35f).Within(0.0001f));
        }

        [Test]
        public void IsWithinJumpLimits_AllowsSceneRiseWithSixMeterLimit()
        {
            Assert.That(SpiderJumpPlanner.IsWithinJumpLimits(6f, 5.35f, 1f, 8f, 6f, 8f), Is.True);
            Assert.That(SpiderJumpPlanner.IsWithinJumpLimits(6f, 5.35f, 1f, 8f, 5f, 8f), Is.False);
            Assert.That(SpiderJumpPlanner.IsWithinJumpLimits(8f, -8f, 1f, 8f, 6f, 8f), Is.True);
        }

        [Test]
        public void IsTopologyShortcut_RejectsFlatDirectMovement()
        {
            Assert.That(
                SpiderJumpPlanner.IsTopologyShortcut(0f, 5f, true, 5.5f, 0.75f, 1.4f),
                Is.False);
            Assert.That(
                SpiderJumpPlanner.IsTopologyShortcut(2f, 5f, true, 5.5f, 0.75f, 1.4f),
                Is.True);
            Assert.That(
                SpiderJumpPlanner.IsTopologyShortcut(0f, 5f, false, 0f, 0.75f, 1.4f),
                Is.True);
            Assert.That(
                SpiderJumpPlanner.IsTopologyShortcut(0f, 5f, true, 8f, 0.75f, 1.4f),
                Is.True);
        }

        [Test]
        public void RemoveAreaFromMask_OnlyRemovesValidArea()
        {
            int fullMask = -1;
            int withoutAreaThree = SpiderJumpPlanner.RemoveAreaFromMask(fullMask, 3);

            Assert.That(withoutAreaThree & (1 << 3), Is.Zero);
            Assert.That(SpiderJumpPlanner.RemoveAreaFromMask(fullMask, -1), Is.EqualTo(fullMask));
            Assert.That(SpiderJumpPlanner.RemoveAreaFromMask(fullMask, 32), Is.EqualTo(fullMask));
        }

        [Test]
        public void SelectApexHeight_UsesLowerArcForDrop()
        {
            Assert.That(SpiderJumpPlanner.SelectApexHeight(-3f, 2f, 0.75f), Is.EqualTo(0.75f));
            Assert.That(SpiderJumpPlanner.SelectApexHeight(3f, 2f, 0.75f), Is.EqualTo(2f));
        }

        [Test]
        public void CalculateMinimumDropApexHeight_KeepsDeepDropAboveTakeoffUntilEdge()
        {
            const float verticalDelta = -5f;
            const float horizontalDistance = 4f;
            const float takeoffInset = 0.65f;
            const float bodyRadius = 0.5f;
            const float collisionSkin = 0.08f;

            float apexHeight = SpiderJumpPlanner.CalculateMinimumDropApexHeight(
                verticalDelta,
                horizontalDistance,
                takeoffInset,
                bodyRadius,
                collisionSkin);
            float edgeClearTime = (takeoffInset + bodyRadius) / horizontalDistance;
            float heightAtClearance =
                SpiderJumpPlanner.EvaluateTrajectory(
                    Vector3.zero,
                    Vector3.up * verticalDelta,
                    apexHeight,
                    edgeClearTime).y;

            Assert.That(heightAtClearance, Is.GreaterThanOrEqualTo(collisionSkin * 2f - 0.0001f));
            Assert.That(apexHeight, Is.GreaterThan(0.75f));
        }

        [Test]
        public void CalculateMinimumDropApexHeight_DoesNotRaiseFlatOrUpwardJump()
        {
            Assert.That(
                SpiderJumpPlanner.CalculateMinimumDropApexHeight(0f, 4f, 0.65f, 0.5f, 0.08f),
                Is.Zero);
            Assert.That(
                SpiderJumpPlanner.CalculateMinimumDropApexHeight(2f, 4f, 0.65f, 0.5f, 0.08f),
                Is.Zero);
        }

        [Test]
        public void ShouldPrioritizeCurrentSide_WhenTargetIsNotHigher()
        {
            Assert.That(SpiderJumpPlanner.ShouldPrioritizeCurrentSide(5f, 1f), Is.True);
            Assert.That(SpiderJumpPlanner.ShouldPrioritizeCurrentSide(1f, 5f), Is.False);
            Assert.That(SpiderJumpPlanner.ShouldPrioritizeCurrentSide(2f, 2f), Is.True);
        }
    }
}
