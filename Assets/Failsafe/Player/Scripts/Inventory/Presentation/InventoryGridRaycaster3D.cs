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
            if (gridTransform == null)
                throw new ArgumentNullException(nameof(gridTransform));

            Plane gridPlane = new Plane(
                gridTransform.up,
                gridTransform.position);

            if (!gridPlane.Raycast(worldRay, out float distance))
            {
                position = default;
                return false;
            }

            Vector3 worldPoint = worldRay.GetPoint(distance);
            Vector3 localPoint = gridTransform.InverseTransformPoint(worldPoint);

            return gridSpace.TryGetGridPosition(localPoint, out position);
        }
    }
}
