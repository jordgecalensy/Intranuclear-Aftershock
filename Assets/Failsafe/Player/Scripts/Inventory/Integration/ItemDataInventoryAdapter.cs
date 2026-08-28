using Failsafe.Inventory.Core;
using Failsafe.Inventory.Presentation;
using UnityEngine;

namespace Failsafe.Inventory.Integration
{
    public static class ItemDataInventoryAdapter
    {
        public static bool TryCreateDefinition(
            ItemData itemData,
            out InventoryItemDefinition definition,
            out string error)
        {
            definition = null;

            if (!TryValidate(itemData, out error))
                return false;

            definition = new InventoryItemDefinition(
                itemData.InventoryDefinitionId,
                new InventoryGridSize(
                    itemData.InventoryWidth,
                    itemData.InventoryHeight),
                itemData.InventoryMaxStack,
                itemData.CanRotateInInventory,
                itemData.CanAssignQuickSlot);

            return true;
        }

        public static bool TryCreateModel(
            ItemData itemData,
            string instanceId,
            int quantity,
            out InventoryItemModel model,
            out string error)
        {
            model = null;

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                error = "Inventory item instance ID cannot be empty.";
                return false;
            }

            if (!TryCreateDefinition(itemData, out InventoryItemDefinition definition, out error))
                return false;

            if (quantity <= 0 || quantity > definition.MaxStack)
            {
                error = $"Inventory item quantity must be between 1 and {definition.MaxStack}.";
                return false;
            }

            model = new InventoryItemModel(instanceId, definition, quantity);
            error = null;

            return true;
        }

        public static bool TryCreateViewDefinition(
            ItemData itemData,
            out InventoryModelViewDefinition definition,
            out string error)
        {
            definition = null;

            if (!TryValidateView(itemData, out error))
                return false;

            definition = new InventoryModelViewDefinition(
                itemData.InventoryModelPrefab,
                Quaternion.Euler(itemData.InventoryBaseEulerAngles),
                itemData.InventoryModelOffsetInCells,
                itemData.InventoryModelScaleMultiplier,
                itemData.InventoryModelFitPadding,
                itemData.InventoryModelMaxDepthInCells);

            return true;
        }

        public static bool TryValidate(ItemData itemData, out string error)
        {
            if (itemData == null)
            {
                error = "ItemData is not assigned.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemData.InventoryDefinitionId))
            {
                error = $"ItemData '{itemData.name}' has no inventory definition ID.";
                return false;
            }

            if (itemData.InventoryWidth <= 0 || itemData.InventoryHeight <= 0)
            {
                error = $"ItemData '{itemData.name}' has an invalid inventory footprint.";
                return false;
            }

            if (itemData.InventoryMaxStack <= 0)
            {
                error = $"ItemData '{itemData.name}' has an invalid maximum stack size.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateView(ItemData itemData, out string error)
        {
            if (!TryValidate(itemData, out error))
                return false;

            return TryValidateModelView(itemData, out error);
        }

        public static bool TryValidateModelView(ItemData itemData, out string error)
        {
            if (itemData == null)
            {
                error = "ItemData is not assigned.";
                return false;
            }

            if (itemData.InventoryModelPrefab == null)
            {
                error = $"ItemData '{itemData.name}' has no 3D inventory model prefab.";
                return false;
            }

            if (itemData.InventoryModelScaleMultiplier <= 0f)
            {
                error = $"ItemData '{itemData.name}' has an invalid inventory model scale multiplier.";
                return false;
            }

            if (itemData.InventoryModelFitPadding < 0f ||
                itemData.InventoryModelFitPadding >= 0.5f)
            {
                error = $"ItemData '{itemData.name}' has invalid inventory model fit padding.";
                return false;
            }

            if (itemData.InventoryModelMaxDepthInCells <= 0f)
            {
                error = $"ItemData '{itemData.name}' has an invalid inventory model depth.";
                return false;
            }

            if (itemData.InventoryModelPrefab
                    .GetComponentsInChildren<Renderer>(true)
                    .Length == 0)
            {
                error = $"Inventory model '{itemData.InventoryModelPrefab.name}' has no renderers.";
                return false;
            }

            MonoBehaviour[] behaviours = itemData.InventoryModelPrefab
                .GetComponentsInChildren<MonoBehaviour>(true);

            if (behaviours.Length > 0)
            {
                error =
                    $"Inventory model '{itemData.InventoryModelPrefab.name}' contains gameplay scripts. " +
                    "Assign a render-only prefab or model asset.";

                return false;
            }

            error = null;
            return true;
        }
    }
}
