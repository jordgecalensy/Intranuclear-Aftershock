// InventoryCore.cs
using System;
using System.Collections.Generic;

namespace Failsafe.Inventory
{
    public readonly struct GridCoord
    {
        public readonly int X, Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public enum Rotation { R0, R90, R180, R270 }

    public sealed class ItemInstance
    {
        public readonly string Id;
        public readonly ItemDefinition Def;
        public int Stack;
        public float Durability = 1f;

        public ItemInstance(ItemDefinition def, int stack = 1)
        {
            Id = Guid.NewGuid().ToString("N");
            Def = def;
            Stack = Math.Max(1, stack);
        }

        public int AddToStack(int d)
        {
            int can = Math.Max(0, Def.maxStack - Stack);
            int add = Math.Min(can, d);
            Stack += add;
            return add;
        }

        public int RemoveFromStack(int d)
        {
            int rem = Math.Min(Stack, d);
            Stack -= rem;
            return rem;
        }
    }

    public sealed class InventoryGrid
    {
        public readonly string Id;
        public readonly int Width, Height;

        private readonly string[,] _cells;

        // Если у тебя старая версия C# — замени new() на new Dictionary<string,(GridCoord pos, Rotation rot)>()
        private readonly Dictionary<string, (GridCoord pos, Rotation rot)> _placements
            = new Dictionary<string, (GridCoord pos, Rotation rot)>();

        public InventoryGrid(string id, int w, int h)
        {
            Id = id;
            Width = w;
            Height = h;
            _cells = new string[w, h];
        }

        public bool IsInside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsFree(int x, int y) => IsInside(x, y) && _cells[x, y] == null;

        public (GridCoord pos, Rotation rot)? GetPlacement(string instanceId)
            => _placements.TryGetValue(instanceId, out var p) ? p : ((GridCoord, Rotation)?)null;

        internal void Reserve(string instanceId, IEnumerable<(int x, int y)> cells, GridCoord pos, Rotation rot)
        {
            foreach (var c in cells)
                _cells[c.x, c.y] = instanceId;

            _placements[instanceId] = (pos, rot);
        }

        internal void FreeByInstance(string instanceId)
        {
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_cells[x, y] == instanceId)
                    _cells[x, y] = null;

            _placements.Remove(instanceId);
        }
    }

    public sealed class InventoryModel
    {
        public readonly Dictionary<string, InventoryGrid> Grids = new();
        public readonly Dictionary<string, ItemInstance> Instances = new();
        public readonly string[] QuickbarSlots;
        public int QuickbarRevolverIndex = 0;
        public int HeavyLimit = 2;
        public InventoryModel(int quickbarSize = 5) { QuickbarSlots = new string[Math.Max(1, quickbarSize)]; }
    }

    public sealed class PlacementService
    {
        public IEnumerable<(int x, int y)> EnumerateCells(ItemDefinition def, GridCoord pos, Rotation rot)
        {
            int w = def.shapeWidth, h = def.shapeHeight;
            for (int sy = 0; sy < h; sy++)
            for (int sx = 0; sx < w; sx++)
            {
                if (!def.Occupies(sx, sy)) continue;
                var (rx, ry) = RotatePoint(sx, sy, w, h, rot);
                yield return (pos.X + rx, pos.Y + ry);
            }
        }

        public bool CanPlace(InventoryGrid g, ItemDefinition d, GridCoord p, Rotation r)
        {
            foreach (var c in EnumerateCells(d, p, r))
                if (!g.IsInside(c.x, c.y) || !g.IsFree(c.x, c.y))
                    return false;
            return true;
        }

        public bool TryFindSpot(InventoryGrid g, ItemDefinition d, out GridCoord p, out Rotation r)
        {
            foreach (Rotation rr in Enum.GetValues(typeof(Rotation)))
            {
                if (!d.canRotate && rr != Rotation.R0) continue;
                for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    var pp = new GridCoord(x, y);
                    if (CanPlace(g, d, pp, rr))
                    {
                        p = pp;
                        r = rr;
                        return true;
                    }
                }
            }

            p = default;
            r = Rotation.R0;
            return false;
        }

        private (int x, int y) RotatePoint(int x, int y, int w, int h, Rotation r) => r switch
        {
            Rotation.R0 => (x, y),
            Rotation.R90 => (h - 1 - y, x),
            Rotation.R180 => (w - 1 - x, h - 1 - y),
            Rotation.R270 => (y, w - 1 - x),
            _ => (x, y)
        };

        public sealed class InventoryService
        {
            public readonly InventoryModel Model;
            public readonly PlacementService Placement;
            public event Action<ItemInstance, string> OnItemAdded, OnItemRemoved, OnItemMoved;
            public event Action<ItemInstance, int> OnQuickbarAssigned, OnItemStacked;
            public event Action<int, int> OnQuickbarSwapped;

            public InventoryService(InventoryModel m, PlacementService p)
            {
                Model = m;
                Placement = p;
            }

            public ItemInstance Create(ItemDefinition d, int count = 1)
            {
                var i = new ItemInstance(d, count);
                Model.Instances[i.Id] = i;
                return i;
            }

            public bool TryAdd(string gridId, ItemInstance i, GridCoord? pos = null, Rotation rot = Rotation.R0)
            {
                if (!Model.Grids.TryGetValue(gridId, out var g)) return false;
                GridCoord placePos;
                Rotation placeRot;
                if (pos.HasValue)
                {
                    placePos = pos.Value;
                    placeRot = rot;
                    if (!Placement.CanPlace(g, i.Def, placePos, placeRot)) return false;
                }
                else
                {
                    if (!Placement.TryFindSpot(g, i.Def, out placePos, out placeRot)) return false;
                }

                var cells = Placement.EnumerateCells(i.Def, placePos, placeRot);
                g.Reserve(i.Id, cells, placePos, placeRot);
                OnItemAdded?.Invoke(i, gridId);
                return true;
            }

            public bool TryMove(string gridId, ItemInstance i, GridCoord newPos, Rotation newRot)
            {
                if (!Model.Grids.TryGetValue(gridId, out var g)) return false;
                var old = g.GetPlacement(i.Id);
                g.FreeByInstance(i.Id);
                if (!Placement.CanPlace(g, i.Def, newPos, newRot))
                {
                    if (old.HasValue)
                    {
                        var cellsOld = Placement.EnumerateCells(i.Def, old.Value.pos, old.Value.rot);
                        g.Reserve(i.Id, cellsOld, old.Value.pos, old.Value.rot);
                    }

                    return false;
                }

                var cells = Placement.EnumerateCells(i.Def, newPos, newRot);
                g.Reserve(i.Id, cells, newPos, newRot);
                OnItemMoved?.Invoke(i, gridId);
                return true;
            }

            public bool Remove(string gridId, ItemInstance i)
            {
                if (!Model.Grids.TryGetValue(gridId, out var g)) return false;
                g.FreeByInstance(i.Id);
                OnItemRemoved?.Invoke(i, gridId);
                return true;
            }

            public bool TryStack(ItemInstance from, ItemInstance to)
            {
                if (from.Def != to.Def) return false;
                int moved = to.AddToStack(from.Stack);
                if (moved <= 0) return false;
                from.RemoveFromStack(moved);
                OnItemStacked?.Invoke(to, moved);
                return true;
            }

            // Quickbar
            public bool TryAssignQuickbarNext(ItemInstance i)
            {
                for (int step = 0; step < Model.QuickbarSlots.Length; step++)
                {
                    int slot = Model.QuickbarRevolverIndex;
                    Model.QuickbarRevolverIndex = (Model.QuickbarRevolverIndex + 1) % Model.QuickbarSlots.Length;
                    if (CanAssignToSlot(slot, i)) return AssignQuickbarSlot(slot, i, true);
                }

                return false;
            }

            public bool AssignQuickbarSlot(int slot, ItemInstance i, bool allowSwap)
            {
                if (slot < 0 || slot >= Model.QuickbarSlots.Length) return false;
                if (!CanAssignToSlot(slot, i)) return false;
                var existing = Model.QuickbarSlots[slot];
                var cur = FindQuickbarSlotOf(i.Id);
                if (existing != null && allowSwap)
                {
                    if (cur.HasValue)
                    {
                        Model.QuickbarSlots[cur.Value] = existing;
                        OnQuickbarAssigned?.Invoke(Model.Instances[existing], cur.Value);
                    }
                    else { Model.QuickbarSlots[slot] = null; }
                }
                else if (existing != null && !allowSwap) return false;

                if (cur.HasValue) Model.QuickbarSlots[cur.Value] = null;

                Model.QuickbarSlots[slot] = i.Id;
                OnQuickbarAssigned?.Invoke(i, slot);
                return true;
            }

            public bool SwapQuickbar(int a, int b)
            {
                if (a < 0 || b < 0 || a >= Model.QuickbarSlots.Length || b >= Model.QuickbarSlots.Length) return false;
                var t = Model.QuickbarSlots[a];
                Model.QuickbarSlots[a] = Model.QuickbarSlots[b];
                Model.QuickbarSlots[b] = t;
                OnQuickbarSwapped?.Invoke(a, b);
                return true;
            }

            public int? FindQuickbarSlotOf(string iid)
            {
                for (int i = 0; i < Model.QuickbarSlots.Length; i++)
                    if (Model.QuickbarSlots[i] == iid)
                        return i;
                return null;
            }

            private bool CanAssignToSlot(int slot, ItemInstance i)
            {
                if (i == null) return false;
                if (i.Def.isHeavy)
                {
                    int heavy = 0;
                    for (int k = 0; k < Model.QuickbarSlots.Length; k++)
                    {
                        var id = Model.QuickbarSlots[k];
                        if (id != null && Model.Instances.TryGetValue(id, out var q) && q.Def.isHeavy) heavy++;
                    }

                    var already = FindQuickbarSlotOf(i.Id).HasValue;
                    if (!already && heavy >= Model.HeavyLimit) return false;
                }

                return true;
            }
        }
    }
}