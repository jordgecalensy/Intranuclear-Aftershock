using System;
using Failsafe.Inventory.Core;
using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    public static class InventoryGridRaycaster3D
    {
        public static bool TryGetGridPosition(
            Ray worldRay,
            Transform gridTransform,
            InventoryGridSpace3D gridSpace,
            out InventoryGridPosition position)
        {
            if (!TryGetLocalPointOnGridPlane(
                    worldRay,
                    gridTransform,
                    out Vector3 localPoint))
            {
                position = default;
                return false;
            }

            return gridSpace.TryGetGridPosition(localPoint, out position);
        }

        public static bool TryGetLocalPointOnGridPlane(
            Ray worldRay,
            Transform gridTransform,
            out Vector3 localPoint)
        {
            if (gridTransform == null)
                throw new ArgumentNullException(nameof(gridTransform));

            Plane gridPlane = new Plane(
                gridTransform.up,
                gridTransform.position);

            if (!gridPlane.Raycast(worldRay, out float distance))
            {
                localPoint = default;
                return false;
            }

            Vector3 worldPoint = worldRay.GetPoint(distance);
            localPoint = gridTransform.InverseTransformPoint(worldPoint);
            return true;
        }
    }
}
