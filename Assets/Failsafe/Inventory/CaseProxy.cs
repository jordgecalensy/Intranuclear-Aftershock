using System;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Inventory
{
    /// Визуальный "стол" (доска) инвентаря на верхней грани кейса.
    /// X вправо, Z вперёд, Y вверх. Центр boardRoot — центр всей сетки.
    public class CaseProxy : MonoBehaviour
    {
        [Header("Board Roots")]
        [Tooltip("Плоскость доски (ось X вправо, Z вперёд). Сам кейс-куб может быть родителем.")]
        public Transform boardRoot;

        [Header("Grid Units (meters)")]
        [Min(0.005f)] public float cellSize = 0.06f;
        [Min(0.0f)]   public float cellGap  = 0.004f;
        [Tooltip("Смещение по Y для посадки предметов над плоскостью доски.")]
        public float itemYOffset = 0.01f;

        [Header("Debug Tiles (optional)")]
        [Tooltip("Если указать, сгенерирует подложку-сетку из квадов.")]
        public GameObject cellTilePrefab;
        [Tooltip("Материал зелёной подсветки валидного ховера.")]
        public Material highlightValid;
        [Tooltip("Материал красной подсветки невалидного ховера.")]
        public Material highlightInvalid;

        // --- runtime ---
        private InventoryController _ctrl;
        private string _gridId;
        private int _w, _h;

        private readonly Dictionary<string, Item3DView> _views = new();
        private readonly List<GameObject> _gridTiles = new();     // декоративные квадраты
        private readonly List<GameObject> _hoverTiles = new();    // плитки ховера текущего предмета

        private bool _inited;

        // ========================== API ==========================
        public interface IInventoryUsable
        {
            bool CanUse(ItemInstance inst, InventoryController ctrl);
            void Use(ItemInstance inst, InventoryController ctrl);
        }

        /// Вызывать из InventoryController при открытии кейса.
        public void Initialize(InventoryController ctrl, string gridId, int gridWidth, int gridHeight)
        {
            _ctrl   = ctrl;
            _gridId = gridId;
            _w      = Mathf.Max(1, gridWidth);
            _h      = Mathf.Max(1, gridHeight);

            if (boardRoot == null) boardRoot = this.transform;
            _inited = true;

            SubscribeServiceEvents(true);
            BuildTilesVisual();
            RenderExistingItems();
        }

        private void OnDestroy()
        {
            SubscribeServiceEvents(false);
            ClearTiles(_gridTiles);
            ClearTiles(_hoverTiles);
            foreach (var v in _views.Values) if (v) Destroy(v.gameObject);
            _views.Clear();
        }

        // =================== Events From Service ===================

        private void SubscribeServiceEvents(bool on)
        {
            if (_ctrl == null || _ctrl.Service == null) return;

            if (on)
            {
                // NB: убедись, что сигнатуры совпадают с твоим InventoryService
                _ctrl.Service.OnItemAdded    += HandleAdded;
                _ctrl.Service.OnItemMoved    += HandleMoved;
                _ctrl.Service.OnItemRemoved  += HandleRemoved;
            }
            else
            {
                _ctrl.Service.OnItemAdded    -= HandleAdded;
                _ctrl.Service.OnItemMoved    -= HandleMoved;
                _ctrl.Service.OnItemRemoved  -= HandleRemoved;
            }
        }

        private void HandleAdded(ItemInstance inst, string gridId)
        {
            if (!_inited || gridId != _gridId || inst == null) return;
            if (_views.ContainsKey(inst.Id)) return;
            SpawnView(inst);
        }

        private void HandleMoved(ItemInstance inst, string gridId)
        {
            if (!_inited || gridId != _gridId || inst == null) return;

            if (!_views.TryGetValue(inst.Id, out var v))
            {
                // если по какой-то причине не было вью — догенерим
                SpawnView(inst);
                return;
            }

            var grid = _ctrl.Model.Grids[_gridId];
            var p = grid.GetPlacement(inst.Id);
            if (!p.HasValue) return;

            var center = CellToWorldCenter(p.Value.pos.X, p.Value.pos.Y);
            var rot    = RotationToWorld(p.Value.rot);
            v.SetWorldPose(center, rot, cellSize, inst.Def.shapeWidth, inst.Def.shapeHeight, p.Value.rot);
        }

        private void HandleRemoved(ItemInstance inst, string gridId)
        {
            if (!_inited || gridId != _gridId || inst == null) return;
            if (_views.TryGetValue(inst.Id, out var v) && v)
            {
                Destroy(v.gameObject);
            }
            _views.Remove(inst.Id);
        }

        public void PlaceInFront(Transform player, float distance, float heightOffset)
        {
            if (!player) return;
            var fwd = player.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = player.transform.forward;
            fwd.Normalize();

            var pos = player.position + fwd * distance;
            pos.y += heightOffset;
            transform.position = pos;

            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // =================== Visual Build / Render ===================

        private void BuildTilesVisual()
        {
            ClearTiles(_gridTiles);
            if (!cellTilePrefab) return;

            float step = cellSize + cellGap;
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                var go = Instantiate(cellTilePrefab, boardRoot, false);
                var c = CellCenterLocal(x, y);

                // Нормализуем ориентацию под XZ и подгоняем масштаб строго под cellSize
                FitTileToCell(go);

                // мелкий подъём против z-fighting
                go.transform.localPosition = new Vector3(c.x, 0.0005f, c.y);

                _gridTiles.Add(go);
            }
        }

        private void ClearTiles(List<GameObject> list)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i]) Destroy(list[i]);
            list.Clear();
        }

        private void RenderExistingItems()
        {
            if (_ctrl == null) return;
            if (!_ctrl.Model.Grids.TryGetValue(_gridId, out var grid)) return;

            foreach (var kv in _ctrl.Model.Instances)
            {
                var inst = kv.Value;
                if (inst == null) continue;

                var place = grid.GetPlacement(inst.Id);
                if (!place.HasValue) continue;
                if (_views.ContainsKey(inst.Id)) continue;

                SpawnView(inst);
            }
        }

        private void SpawnView(ItemInstance inst)
        {
            var go = new GameObject($"Item3DView_{inst.Def.name}_{inst.Id}");
            go.transform.SetParent(boardRoot, false);

            var view = go.AddComponent<Item3DView>();
            view.Bind(inst, this, _ctrl);
            _views[inst.Id] = view;

            var grid = _ctrl.Model.Grids[_gridId];
            var p = grid.GetPlacement(inst.Id);
            if (p.HasValue)
            {
                var center = CellToWorldCenter(p.Value.pos.X, p.Value.pos.Y);
                var rot    = RotationToWorld(p.Value.rot);
                view.SetWorldPose(center, rot, cellSize, inst.Def.shapeWidth, inst.Def.shapeHeight, p.Value.rot);
            }
        }

        // =================== Hover highlight ===================

        public void ShowHover(ItemDefinition def, GridCoord pos, Rotation rot, bool valid)
        {
            if (!def) return;

            ClearTiles(_hoverTiles);

            var mat = valid ? highlightValid : highlightInvalid;
            foreach (var cell in EnumerateOccupiedCells(def, pos, rot))
            {
                if (!Inside(cell.x, cell.y)) continue;
                var tile = CreateHoverTile(cell.x, cell.y, mat);
                _hoverTiles.Add(tile);
            }
        }

        public void ClearHover()
        {
            ClearTiles(_hoverTiles);
        }

        private GameObject CreateHoverTile(int cx, int cy, Material mat)
        {
            GameObject go;
            if (cellTilePrefab)
            {
                go = Instantiate(cellTilePrefab, boardRoot, false);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr && mat) mr.sharedMaterial = mat;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(boardRoot, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr && mat) mr.sharedMaterial = mat;
            }

            // Нормализуем ориентацию и подгоняем масштаб под cellSize
            FitTileToCell(go);

            var c = CellCenterLocal(cx, cy);
            go.transform.localPosition = new Vector3(c.x, 0.001f, c.y);
            return go;
        }

        private IEnumerable<Vector2Int> EnumerateOccupiedCells(ItemDefinition def, GridCoord pos, Rotation rot)
        {
            int w = def.shapeWidth;
            int h = def.shapeHeight;
            for (int sy = 0; sy < h; sy++)
            for (int sx = 0; sx < w; sx++)
            {
                if (!def.Occupies(sx, sy)) continue;
                var pr = RotatePoint(sx, sy, w, h, rot);
                yield return new Vector2Int(pos.X + pr.x, pos.Y + pr.y);
            }
        }

        // =================== Coordinate transforms ===================

        /// Проекция курсора в клетку сетки; worldOnBoard — ближайшая точка на плоскости доски.
        public bool ScreenToCell(Vector2 screenPos, out int cx, out int cy, out Vector3 worldOnBoard)
        {
            cx = cy = -1;
            worldOnBoard = Vector3.zero;
            var cam = _ctrl && _ctrl.playerCamera ? _ctrl.playerCamera : Camera.main;
            if (!cam || !boardRoot) return false;

            var plane = new Plane(boardRoot.up, boardRoot.position);
            var ray = cam.ScreenPointToRay(screenPos);
            if (!plane.Raycast(ray, out float dist)) return false;

            var hit = ray.GetPoint(dist);
            worldOnBoard = hit;

            var local = boardRoot.InverseTransformPoint(hit);

            float step = cellSize + cellGap;
            float totalW = _w * step - cellGap;
            float totalH = _h * step - cellGap;

            float startX = -0.5f * totalW + cellSize * 0.5f; // центр клетки (0,0)
            float startZ = -0.5f * totalH + cellSize * 0.5f;

            float relX = local.x - startX;
            float relZ = local.z - startZ;

            cx = Mathf.FloorToInt(relX / step);
            cy = Mathf.FloorToInt(relZ / step);

            return Inside(cx, cy);
        }

        /// Центр клетки (cx,cy) в мировых координатах доски.
        public Vector3 CellToWorldCenter(int cx, int cy)
        {
            var c = CellCenterLocal(cx, cy);
            var local = new Vector3(c.x, itemYOffset, c.y);
            return boardRoot.TransformPoint(local);
        }

        /// Поворот grid → мировой (ось Y).
        public Quaternion RotationToWorld(Rotation r)
        {
            float y = r switch
            {
                Rotation.R0   => 0f,
                Rotation.R90  => 90f,
                Rotation.R180 => 180f,
                Rotation.R270 => 270f,
                _             => 0f
            };
            return boardRoot.rotation * Quaternion.Euler(0f, y, 0f);
        }

        private Vector2 CellCenterLocal(int cx, int cy)
        {
            float step = cellSize + cellGap;
            float totalW = _w * step - cellGap;
            float totalH = _h * step - cellGap;

            float startX = -0.5f * totalW + cellSize * 0.5f;
            float startZ = -0.5f * totalH + cellSize * 0.5f;

            float x = startX + cx * step;
            float z = startZ + cy * step;
            return new Vector2(x, z);
        }

        private bool Inside(int x, int y) => (x >= 0 && y >= 0 && x < _w && y < _h);

        private (int x, int y) RotatePoint(int x, int y, int w, int h, Rotation r)
        {
            if (r == Rotation.R0)   return (x, y);
            if (r == Rotation.R90)  return (h - 1 - y, x);
            if (r == Rotation.R180) return (w - 1 - x, h - 1 - y);
            if (r == Rotation.R270) return (y, w - 1 - x);
            return (x, y);
        }

        // =================== Helpers: тайлы/габариты ===================

        private void FitTileToCell(GameObject go)
        {
            if (!go) return;

            // Если префаб плоский в XY — положим на XZ
            var aabb0 = CalcLocalAABBRelativeTo(go.transform, go);
            bool looksXY = aabb0.size.z < 0.0001f && aabb0.size.y >= 0.0001f;
            if (looksXY)
            {
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                // обнулим наклон, если префаб "криво" ориентирован
                go.transform.localRotation = Quaternion.identity;
            }

            // Пересчитаем габариты после нормализации ориентации
            var aabb = CalcLocalAABBRelativeTo(go.transform, go);
            float sx = Mathf.Max(aabb.size.x, 0.0001f);
            float sz = Mathf.Max(aabb.size.z, 0.0001f);

            // подгон строго под cellSize (зазор обеспечивается cellGap на уровне шага сетки)
            var ls = go.transform.localScale;
            go.transform.localScale = new Vector3(ls.x * (cellSize / sx), ls.y, ls.z * (cellSize / sz));
        }

        private Bounds CalcLocalAABBRelativeTo(Transform root, GameObject go)
        {
            bool has = false;
            Bounds aabb = new Bounds(Vector3.zero, Vector3.zero);

            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mfs.Length; i++)
            {
                var mesh = mfs[i].sharedMesh;
                if (!mesh) continue;
                Matrix4x4 m = root.worldToLocalMatrix * mfs[i].transform.localToWorldMatrix;
                Bounds b = mesh.bounds;
                EncapsulateTransformedAABB(ref aabb, ref has, m, b);
            }

            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < smrs.Length; i++)
            {
                Matrix4x4 m = root.worldToLocalMatrix * smrs[i].transform.localToWorldMatrix;
                Bounds b = smrs[i].localBounds;
                EncapsulateTransformedAABB(ref aabb, ref has, m, b);
            }

            if (!has) aabb = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            return aabb;
        }

        private void EncapsulateTransformedAABB(ref Bounds dst, ref bool has, Matrix4x4 toRoot, Bounds local)
        {
            Vector3 min = local.min, max = local.max;
            Vector3[] c =
            {
                new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z),
                new Vector3(min.x,max.y,min.z), new Vector3(max.x,max.y,min.z),
                new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z),
                new Vector3(min.x,max.y,max.z), new Vector3(max.x,max.y,max.z)
            };
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(c[i]);
                if (!has) { dst = new Bounds(p, Vector3.zero); has = true; }
                else dst.Encapsulate(p);
            }
        }
    }
}
