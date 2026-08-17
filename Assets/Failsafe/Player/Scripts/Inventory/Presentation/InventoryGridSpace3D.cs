using System;
using Failsafe.Inventory.Core;
using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    public readonly struct InventoryGridSpace3D
    {
        public int Columns { get; }
        public int Rows { get; }
        public float CellSize { get; }

        public InventoryGridSpace3D(int columns, int rows, float cellSize)
        {
            if (columns <= 0)
                throw new ArgumentOutOfRangeException(nameof(columns));

            if (rows <= 0)
                throw new ArgumentOutOfRangeException(nameof(rows));

            if (cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize));

            Columns = columns;
            Rows = rows;
            CellSize = cellSize;
        }

        public Vector3 GetPlacementCenter(
            InventoryGridPosition origin,
            InventoryGridSize footprint)
        {
            ValidatePlacement(origin, footprint);

            float centerColumn = origin.Column + footprint.Width * 0.5f;
            float centerRow = origin.Row + footprint.Height * 0.5f;

            return new Vector3(
                (centerColumn - Columns * 0.5f) * CellSize,
                0f,
                (Rows * 0.5f - centerRow) * CellSize);
        }

        public Quaternion GetGridRotation(InventoryItemRotation rotation)
        {
            switch (rotation)
            {
                case InventoryItemRotation.Default:
                    return Quaternion.identity;

                case InventoryItemRotation.Clockwise90:
                    return Quaternion.AngleAxis(90f, Vector3.up);

                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }

        public bool TryGetGridPosition(
            Vector3 localPoint,
            out InventoryGridPosition position)
        {
            float halfWidth = Columns * CellSize * 0.5f;
            float halfHeight = Rows * CellSize * 0.5f;

            float columnCoordinate = (localPoint.x + halfWidth) / CellSize;
            float rowCoordinate = (halfHeight - localPoint.z) / CellSize;

            if (columnCoordinate < 0f ||
                columnCoordinate >= Columns ||
                rowCoordinate < 0f ||
                rowCoordinate >= Rows)
            {
                position = default;
                return false;
            }

            position = new InventoryGridPosition(
                Mathf.FloorToInt(columnCoordinate),
                Mathf.FloorToInt(rowCoordinate));

            return true;
        }

        private void ValidatePlacement(
            InventoryGridPosition origin,
            InventoryGridSize footprint)
        {
            if (origin.Column < 0 ||
                origin.Row < 0 ||
                origin.Column + footprint.Width > Columns ||
                origin.Row + footprint.Height > Rows)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(origin),
                    "Placement must stay inside the configured grid space.");
            }
        }
    }
}
