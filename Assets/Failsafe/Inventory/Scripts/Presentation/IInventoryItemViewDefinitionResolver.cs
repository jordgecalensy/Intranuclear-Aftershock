using Failsafe.Inventory.Core;

namespace Failsafe.Inventory.Presentation
{
    public interface IInventoryItemViewDefinitionResolver
    {
        bool TryResolve(
            InventoryItemModel item,
            out InventoryModelViewDefinition definition,
            out string error);
    }
}
