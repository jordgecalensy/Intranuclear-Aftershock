using System;
using System.Collections.Generic;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Presentation;

namespace Failsafe.Inventory.Integration
{
    public sealed class ItemDataInventoryViewResolver : IInventoryItemViewDefinitionResolver
    {
        public int Count => _itemDataByInstanceId.Count;

        private readonly Dictionary<string, ItemData> _itemDataByInstanceId =
            new Dictionary<string, ItemData>(StringComparer.Ordinal);

        public bool TryRegister(
            string instanceId,
            ItemData itemData,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                error = "Inventory item instance ID cannot be empty.";
                return false;
            }

            if (_itemDataByInstanceId.ContainsKey(instanceId))
            {
                error = $"Inventory item instance ID '{instanceId}' is already registered.";
                return false;
            }

            if (!ItemDataInventoryAdapter.TryValidateView(itemData, out error))
                return false;

            _itemDataByInstanceId.Add(instanceId, itemData);
            error = null;
            return true;
        }

        public bool Unregister(string instanceId)
        {
            return !string.IsNullOrWhiteSpace(instanceId) &&
                   _itemDataByInstanceId.Remove(instanceId);
        }

        public bool TryGetItemData(
            string instanceId,
            out ItemData itemData)
        {
            itemData = null;

            return !string.IsNullOrWhiteSpace(instanceId) &&
                   _itemDataByInstanceId.TryGetValue(instanceId, out itemData) &&
                   itemData != null;
        }

        public bool TryResolve(
            InventoryItemModel item,
            out InventoryModelViewDefinition definition,
            out string error)
        {
            definition = null;

            if (item == null)
            {
                error = "Inventory item model is not assigned.";
                return false;
            }

            if (!TryGetItemData(item.InstanceId, out ItemData itemData))
            {
                error = $"No ItemData is registered for inventory item '{item.InstanceId}'.";
                return false;
            }

            if (!string.Equals(
                    item.DefinitionId,
                    itemData.InventoryDefinitionId,
                    StringComparison.Ordinal))
            {
                error =
                    $"Inventory item '{item.InstanceId}' definition ID does not match " +
                    $"ItemData '{itemData.name}'.";

                return false;
            }

            return ItemDataInventoryAdapter.TryCreateViewDefinition(
                itemData,
                out definition,
                out error);
        }

        public void Clear()
        {
            _itemDataByInstanceId.Clear();
        }
    }
}
