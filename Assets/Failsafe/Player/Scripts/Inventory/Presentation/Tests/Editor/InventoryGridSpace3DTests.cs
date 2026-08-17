using System;
using Failsafe.Inventory.Core;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Inventory.Presentation.Tests
{
    [TestFixture]
    public sealed class InventoryGridSpace3DTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void GetPlacementCenter_PlacesTopLeftCellOnCenteredGrid()
        {
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);

            Vector3 center = gridSpace.GetPlacementCenter(
                new InventoryGridPosition(0, 0),
                new InventoryGridSize(1, 1));

            AssertVector(center, new Vector3(-2.5f, 0f, 2f));
        }

        [Test]
        public void GetPlacementCenter_UsesEntireOccupiedFootprint()
        {
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);

            Vector3 center = gridSpace.GetPlacementCenter(
                new InventoryGridPosition(1, 2),
                new InventoryGridSize(3, 2));

            AssertVector(center, new Vector3(-0.5f, 0f, -0.5f));
        }

        [Test]
        public void GetGridRotation_ReturnsAbsoluteClockwiseQuarterTurn()
        {
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);

            Quaternion first = gridSpace.GetGridRotation(
                InventoryItemRotation.Clockwise90);

            Quaternion second = gridSpace.GetGridRotation(
                InventoryItemRotation.Clockwise90);

            Assert.That(
                Quaternion.Angle(first, Quaternion.AngleAxis(90f, Vector3.up)),
                Is.LessThan(Tolerance));

            Assert.That(Quaternion.Angle(first, second), Is.LessThan(Tolerance));
        }

        [Test]
        public void GetPlacementCenter_RejectsPlacementOutsideGrid()
        {
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                gridSpace.GetPlacementCenter(
                    new InventoryGridPosition(5, 4),
                    new InventoryGridSize(2, 1)));
        }

        [Test]
        public void TryGetGridPosition_MapsLocalPointToCell()
        {
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);

            bool topLeftFound = gridSpace.TryGetGridPosition(
                new Vector3(-2.5f, 7f, 2f),
                out InventoryGridPosition topLeft);

            bool bottomRightFound = gridSpace.TryGetGridPosition(
                new Vector3(2.5f, -3f, -2f),
                out InventoryGridPosition bottomRight);

            Assert.That(topLeftFound, Is.True);
            Assert.That(topLeft, Is.EqualTo(new InventoryGridPosition(0, 0)));
            Assert.That(bottomRightFound, Is.True);
            Assert.That(bottomRight, Is.EqualTo(new InventoryGridPosition(5, 4)));
        }

        [Test]
        public void TryGetGridPosition_UsesInclusiveTopLeftAndExclusiveBottomRightEdges()
        {
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);

            Assert.That(
                gridSpace.TryGetGridPosition(
                    new Vector3(-3f, 0f, 2.5f),
                    out InventoryGridPosition topLeft),
                Is.True);
            Assert.That(topLeft, Is.EqualTo(new InventoryGridPosition(0, 0)));

            Assert.That(
                gridSpace.TryGetGridPosition(
                    new Vector3(3f, 0f, 0f),
                    out _),
                Is.False);
            Assert.That(
                gridSpace.TryGetGridPosition(
                    new Vector3(0f, 0f, -2.5f),
                    out _),
                Is.False);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
