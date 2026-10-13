using System;
using Failsafe.Inventory.Core;
using UnityEngine;

namespace Failsafe.Inventory.Integration
{
    public interface IInventoryHeldItemLifecycle
    {
        bool TryReleaseToWorld(Item item, out string error);
        bool TryConsume(Item item, out string error);
    }

    public sealed class InventoryHeldItemLifecycleService :
        IInventoryHeldItemLifecycle
    {
        private readonly InventoryRuntimeController _inventory;

        public InventoryHeldItemLifecycleService(
            InventoryRuntimeController inventory)
        {
            _inventory = inventory ??
                throw new ArgumentNullException(nameof(inventory));
        }

        public bool TryReleaseToWorld(Item item, out string error)
        {
            if (item == null)
            {
                error = "Held item is not assigned.";
                return false;
            }

            if (!_inventory.IsInitialized)
            {
                error = "Inventory runtime is not initialized.";
                return false;
            }

            if (!_inventory.TryGetWorldItemInstanceId(
                    item,
                    out string instanceId))
            {
                error = null;
                return true;
            }

            InventoryOperationResult result =
                _inventory.DetachWorldItem(
                    instanceId,
                    out Item detachedItem,
                    out error);

            if (!result.IsSuccess)
                return false;

            if (detachedItem == item)
                return true;

            error =
                $"Inventory detached a different world item for " +
                $"instance '{instanceId}'.";

            return false;
        }

        public bool TryConsume(Item item, out string error)
        {
            if (item == null)
            {
                error = "Consumed item is not assigned.";
                return false;
            }

            if (!_inventory.IsInitialized)
            {
                DestroyItem(item);
                error =
                    "Inventory runtime is not initialized. The consumed " +
                    "world item was destroyed without inventory cleanup.";

                return false;
            }

            if (!_inventory.TryGetWorldItemInstanceId(
                    item,
                    out string instanceId))
            {
                DestroyItem(item);
                error = null;
                return true;
            }

            InventoryOperationResult result =
                _inventory.ConsumeRegisteredWorldItem(
                    instanceId,
                    out error);

            if (result.IsSuccess)
                return true;

            DestroyItem(item);

            error =
                $"{error ?? "Inventory consumption failed."} " +
                "The consumed world item was destroyed to prevent reuse.";

            return false;
        }

        private static void DestroyItem(Item item)
        {
            if (item == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(item.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(item.gameObject);
        }
    }

    public sealed class PassthroughInventoryHeldItemLifecycleService :
        IInventoryHeldItemLifecycle
    {
        public bool TryReleaseToWorld(Item item, out string error)
        {
            error = item == null
                ? "Held item is not assigned."
                : null;

            return item != null;
        }

        public bool TryConsume(Item item, out string error)
        {
            if (item == null)
            {
                error = "Consumed item is not assigned.";
                return false;
            }

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(item.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(item.gameObject);

            error = null;
            return true;
        }
    }
}
