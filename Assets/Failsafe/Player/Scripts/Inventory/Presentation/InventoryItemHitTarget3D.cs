using System;
using Failsafe.Inventory.Core;
using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class InventoryItemHitTarget3D : MonoBehaviour
    {
        public string InstanceId { get; private set; }
        public InventoryGridSize Footprint { get; private set; }
        public BoxCollider Collider { get; private set; }

        private float _cellSize;

        public void Initialize(
            string instanceId,
            InventoryGridSize footprint,
            float cellSize)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("Instance ID cannot be empty.", nameof(instanceId));

            if (cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize));

            if (Collider != null)
                throw new InvalidOperationException("Inventory item hit target is already initialized.");

            InstanceId = instanceId;
            _cellSize = cellSize;
            Collider = GetComponent<BoxCollider>();
            Collider.isTrigger = true;
            ApplyFootprint(footprint);
        }

        public void ApplyFootprint(InventoryGridSize footprint)
        {
            if (Collider == null)
                throw new InvalidOperationException("Inventory item hit target is not initialized.");

            Footprint = footprint;
            Collider.center = Vector3.zero;
            Collider.size = new Vector3(
                footprint.Width * _cellSize,
                _cellSize,
                footprint.Height * _cellSize);
        }
    }
}
