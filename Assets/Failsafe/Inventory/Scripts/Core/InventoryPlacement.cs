namespace Failsafe.Inventory.Core
{
    public sealed class InventoryPlacement
    {
        public InventoryItemModel Item { get; }
        public InventoryGridPosition Origin { get; internal set; }
        public InventoryGridSize Footprint => Item.Footprint;

        internal InventoryPlacement(
            InventoryItemModel item,
            InventoryGridPosition origin)
        {
            Item = item;
            Origin = origin;
        }

        public bool Contains(InventoryGridPosition position)
        {
            return position.Column >= Origin.Column &&
                   position.Column < Origin.Column + Footprint.Width &&
                   position.Row >= Origin.Row &&
                   position.Row < Origin.Row + Footprint.Height;
        }
    }
}
