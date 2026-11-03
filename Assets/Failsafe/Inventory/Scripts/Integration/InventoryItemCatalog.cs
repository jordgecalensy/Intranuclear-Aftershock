using System;
using System.Collections.Generic;

namespace Failsafe.Inventory.Integration
{
    internal sealed class InventoryItemCatalog
    {
        private readonly Dictionary<string, ItemData> _itemsByDefinitionId =
            new Dictionary<string, ItemData>(StringComparer.Ordinal);

        public bool TryBuild(
            IReadOnlyList<ItemData> itemData,
            out string error)
        {
            _itemsByDefinitionId.Clear();

            if (itemData == null)
            {
                error = null;
                return true;
            }

            for (int index = 0; index < itemData.Count; index++)
            {
                if (TryRegister(itemData[index], out error))
                    continue;

                error = $"Item catalog entry {index}: {error}";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryRegister(ItemData itemData, out string error)
        {
            if (!ItemDataInventoryAdapter.TryValidateView(
                    itemData,
                    out error))
            {
                return false;
            }

            string definitionId = itemData.InventoryDefinitionId.Trim();

            if (_itemsByDefinitionId.TryGetValue(
                    definitionId,
                    out ItemData registeredItemData))
            {
                if (registeredItemData == itemData)
                {
                    error = null;
                    return true;
                }

                error =
                    $"Inventory definition ID '{definitionId}' is used by " +
                    $"both '{registeredItemData.name}' and '{itemData.name}'.";

                return false;
            }

            _itemsByDefinitionId.Add(definitionId, itemData);
            error = null;
            return true;
        }

        public bool TryResolve(
            string definitionId,
            out ItemData itemData,
            out string error)
        {
            itemData = null;
            string normalizedId = definitionId?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                error = "Inventory definition ID cannot be empty.";
                return false;
            }

            if (!_itemsByDefinitionId.TryGetValue(
                    normalizedId,
                    out itemData) ||
                itemData == null)
            {
                error =
                    $"Inventory ItemData catalog has no definition " +
                    $"'{normalizedId}'.";

                return false;
            }

            error = null;
            return true;
        }

        public void Clear()
        {
            _itemsByDefinitionId.Clear();
        }
    }
}
