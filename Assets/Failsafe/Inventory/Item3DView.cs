using UnityEngine;

namespace Failsafe.Inventory
{
    public class Item3DView : MonoBehaviour
    {
        // ---- Публично/внешне ----
        public ItemInstance Inst { get; private set; }

        // ---- Внутренние ссылки ----
        private CaseProxy _board;
        private InventoryController _ctrl;
        private GameObject _model;

        // ---- Драг-состояние ----
        private bool _drag;
        private Rotation _rot = Rotation.R0;
        private Vector3 _origPos;
        private Quaternion _origRot;
        private GridCoord _origGridPos;
        private Rotation _origGridRot;
        private bool _hasOrigPlacement;
        private bool _freedOnDrag; // NEW: клетки освобождены?

        // ---- Рендер/тент ----
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private static readonly int _ColorId     = Shader.PropertyToID("_Color");
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _TintId      = Shader.PropertyToID("_TintColor");

        private Color _tintNormal     = Color.white;
        private Color _tintDragNeutral= new Color(1f, 0.95f, 0.60f, 1f);
        private Color _tintDragValid  = new Color(0.60f, 1f, 0.60f, 1f);
        private Color _tintDragInvalid= new Color(1f, 0.60f, 0.60f, 1f);

        // ---- Геометрия/масштаб ----
        private Vector3 _baseScale = Vector3.one;

        // ---- Коллайдеры для хит-теста ----
        private Collider[] _colliders;

        // ================== API ==================

        public void Bind(ItemInstance inst, CaseProxy board, InventoryController ctrl)
        {
            Inst = inst; _board = board; _ctrl = ctrl;

            transform.localScale = Vector3.one;

            if (Inst.Def.WorldPrefab)
            {
                _model = Instantiate(Inst.Def.WorldPrefab, transform, false);
                _model.transform.localPosition = Vector3.zero;
                _model.transform.localRotation = Quaternion.identity;

                _baseScale = _model.transform.localScale;

                EnsureCollider(_model);
                _renderers = _model.GetComponentsInChildren<Renderer>(true);
                _colliders = _model.GetComponentsInChildren<Collider>(true);
                _mpb = new MaterialPropertyBlock();

                AlignModelBottomCenterToOrigin_Local();
            }
        }

        public void SetWorldPose(Vector3 center, Quaternion rot, float cellSize, int defW, int defH, Rotation gridRot)
        {
            transform.SetPositionAndRotation(center, rot);
            if (_model == null) return;

            var def = Inst.Def;

            if (def.poseMode == InventoryPoseMode.ManualMeters || def.poseMode == InventoryPoseMode.ManualCells)
            {
                _model.transform.localScale    = def.manualLocalScale;
                _model.transform.localRotation = Quaternion.Euler(def.manualLocalEuler);

                if (def.poseMode == InventoryPoseMode.ManualMeters)
                {
                    _model.transform.localPosition = def.manualLocalPositionMeters;
                }
                else
                {
                    Vector3 off = new Vector3(
                        def.manualOffsetCellsXZ.x * cellSize,
                        def.manualOffsetY,
                        def.manualOffsetCellsXZ.y * cellSize
                    );
                    _model.transform.localPosition = off;
                }
                return;
            }

            _model.transform.localScale = _baseScale;

            Vector2Int ext = GetRotatedFootprintExtents(def, gridRot);
            float targetW = Mathf.Max(cellSize * ext.x, 0.0001f);
            float targetD = Mathf.Max(cellSize * ext.y, 0.0001f);

            Bounds lb = CalcLocalAABBRelativeToRoot(transform, _model);
            float sizeX = Mathf.Max(lb.size.x, 0.0001f);
            float sizeZ = Mathf.Max(lb.size.z, 0.0001f);

            float sx = targetW / sizeX;
            float sz = targetD / sizeZ;

            float k;
            if (def.poseMode == InventoryPoseMode.AutoFill) k = Mathf.Max(sx, sz);
            else
            {
                if (def.fitMode == FitMode.UniformFill) k = Mathf.Max(sx, sz);
                else if (def.fitMode == FitMode.Stretch)
                {
                    _model.transform.localScale = new Vector3(_baseScale.x * sx,
                                                              _baseScale.y * Mathf.Min(sx, sz),
                                                              _baseScale.z * sz) * Mathf.Max(def.scaleMultiplier, 0.0001f);
                    AlignModelBottomCenterToOrigin_Local();
                    return;
                }
                else k = Mathf.Min(sx, sz);
            }
            k *= Mathf.Max(def.scaleMultiplier, 0.0001f);

            _model.transform.localScale = _baseScale * k;
            AlignModelBottomCenterToOrigin_Local();
        }

        // ================== Update: drag/use ==================

        private void Update()
        {
            if (_ctrl == null || _board == null || Inst == null) return;
            if (!Cursor.visible) return; // когда инвентарь закрыт — мышь залочена

            // ПКМ/F → использовать (если не тянем сейчас)
            if (!_drag)
            {
                bool wantsUse = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.F);
                if (wantsUse && RayHitsThis())
                {
                    TryUseInInventory();
                    return;
                }
            }

            // --- Старт драга ЛКМ ---
            if (Input.GetMouseButtonDown(0))
            {
                if (RayHitsThis())
                {
                    _drag = true;

                    _origPos = transform.position;
                    _origRot = transform.rotation;

                    var grid = _ctrl.Model.Grids[_ctrl.playerGridId];
                    var place = grid.GetPlacement(Inst.Id);
                    _hasOrigPlacement = place.HasValue;
                    if (_hasOrigPlacement)
                    {
                        _origGridPos = place.Value.pos;
                        _origGridRot = place.Value.rot;
                        _rot = _origGridRot;

                        // NEW: временно освободить клетки своего предмета,
                        // чтобы валидатор не считал их занятыми
                        grid.FreeByInstance(Inst.Id);
                        _freedOnDrag = true;
                    }
                    else
                    {
                        _rot = Rotation.R0;
                        _freedOnDrag = false;
                    }

                    SetDragTint(_tintDragNeutral);
                }
            }

            if (_drag)
            {
                // Поворот во время драга
                if (Inst.Def.canRotate && Input.GetKeyDown(KeyCode.R))
                {
                    _rot = NextRotation(_rot);
                }

                // Назначение в слот цифрами 1..N (и NumPad)
                for (int k = 0; k < _ctrl.Model.QuickbarSlots.Length; k++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + k) || Input.GetKeyDown(KeyCode.Keypad1 + k))
                    {
                        _ctrl.Service.Remove(_ctrl.playerGridId, Inst);
                        if (!_ctrl.Service.AssignQuickbarSlot(k, Inst, true))
                            _ctrl.Service.TryAdd(_ctrl.playerGridId, Inst); // вернуть, если не вышло

                        EndDrag(false); // НЕ восстанавливаем старые клетки
                        return;
                    }
                }

                // Револьвер E
                if (Input.GetKeyDown(KeyCode.E))
                {
                    _ctrl.Service.Remove(_ctrl.playerGridId, Inst);
                    if (!_ctrl.Service.TryAssignQuickbarNext(Inst))
                        _ctrl.Service.TryAdd(_ctrl.playerGridId, Inst);

                    EndDrag(false); // НЕ восстанавливаем старые клетки
                    return;
                }

                // Выкинуть Q
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    _ctrl.Service.Remove(_ctrl.playerGridId, Inst);
                    _ctrl.DropToWorld(Inst);
                    Destroy(gameObject);
                    return;
                }

                // Следуем за курсором
                int cx, cy; Vector3 worldOnBoard;
                if (_board.ScreenToCell(Input.mousePosition, out cx, out cy, out worldOnBoard))
                {
                    GridCoord pos = new GridCoord(cx, cy);
                    bool can = _ctrl.Placement.CanPlace(_ctrl.Model.Grids[_ctrl.playerGridId], Inst.Def, pos, _rot);

                    Vector3 center = _board.CellToWorldCenter(cx, cy);
                    transform.position = center;
                    transform.rotation = _board.RotationToWorld(_rot);

                    _board.ShowHover(Inst.Def, pos, _rot, can);
                    SetDragTint(can ? _tintDragValid : _tintDragInvalid);
                }
                else
                {
                    transform.position = worldOnBoard;
                    transform.rotation = _board.RotationToWorld(_rot);

                    _board.ClearHover();
                    SetDragTint(_tintDragInvalid);
                }

                // Завершение драга
                if (Input.GetMouseButtonUp(0))
                {
                    if (_board.ScreenToCell(Input.mousePosition, out cx, out cy, out worldOnBoard))
                    {
                        bool ok = _ctrl.Service.TryMove(_ctrl.playerGridId, Inst, new GridCoord(cx, cy), _rot);
                        if (ok)
                        {
                            var p = _ctrl.Model.Grids[_ctrl.playerGridId].GetPlacement(Inst.Id);
                            if (p.HasValue)
                            {
                                Vector3 center = _board.CellToWorldCenter(p.Value.pos.X, p.Value.pos.Y);
                                transform.position = center;
                                transform.rotation = _board.RotationToWorld(p.Value.rot);
                            }
                            _freedOnDrag = false;
                            EndDrag(false);
                        }
                        else
                        {
                            // Попробовать стекнуть
                            if (TryStackIntoTargetUnderCursor())
                            {
                                _freedOnDrag = false; // инстанс, скорее всего, удалён
                                EndDrag(false);
                            }
                            else
                            {
                                // Отмена: вернуть старое бронирование и визуально откатиться
                                RestoreOriginalPlacementGrid();
                                SnapBackToOriginalTile();
                                EndDrag(false);
                            }
                        }
                    }
                    else
                    {
                        // Вне инвентаря → дроп
                        _ctrl.Service.Remove(_ctrl.playerGridId, Inst);
                        _ctrl.DropToWorld(Inst);
                        Destroy(gameObject);
                    }
                }
            }
        }

        // ================== Helpers ==================

        private void EndDrag(bool resetToOriginalVisual)
        {
            _drag = false;
            _board.ClearHover();
            SetDragTint(_tintNormal);

            if (resetToOriginalVisual) SnapBackToOriginalTile();
        }

        private void RestoreOriginalPlacementGrid()
        {
            if (!_freedOnDrag || !_hasOrigPlacement) { _freedOnDrag = false; return; }
            var grid = _ctrl.Model.Grids[_ctrl.playerGridId];
            var cellsOld = _ctrl.Placement.EnumerateCells(Inst.Def, _origGridPos, _origGridRot);
            grid.Reserve(Inst.Id, cellsOld, _origGridPos, _origGridRot);
            _freedOnDrag = false;
        }

        private void SnapBackToOriginalTile()
        {
            if (_hasOrigPlacement)
            {
                Vector3 center = _board.CellToWorldCenter(_origGridPos.X, _origGridPos.Y);
                transform.position = center;
                transform.rotation = _board.RotationToWorld(_origGridRot);
            }
            else
            {
                transform.position = _origPos;
                transform.rotation = _origRot;
            }
        }

        private void TryUseInInventory()
        {
            if (_model == null) return;

            // NB: действия юзов — оставлены как у тебя
            if (!_ctrl.Model.Instances.ContainsKey(Inst.Id))
            {
                Destroy(gameObject);
            }
            else
            {
                var grid = _ctrl.Model.Grids[_ctrl.playerGridId];
                var p = grid.GetPlacement(Inst.Id);
                if (p.HasValue)
                {
                    Vector3 center = _board.CellToWorldCenter(p.Value.pos.X, p.Value.pos.Y);
                    transform.position = center;
                    transform.rotation = _board.RotationToWorld(p.Value.rot);
                }
                return;
            }

            var wi = _model.GetComponentInChildren<WorldItem>();
            if (wi && wi.IsUsable()) wi.Use();
        }
        private bool TryStackIntoTargetUnderCursor()
        {
            if (Inst.Def.maxStack <= 1) return false;

            var cam = _ctrl.playerCamera ? _ctrl.playerCamera : Camera.main;
            if (!cam) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
            float minDist = float.MaxValue;
            Item3DView best = null;

            for (int i = 0; i < hits.Length; i++)
            {
                var v = hits[i].collider ? hits[i].collider.GetComponentInParent<Item3DView>() : null;
                if (v == null || v == this) continue;
                if (v.Inst == null || v.Inst.Def != Inst.Def) continue; // только одинаковый тип
                if (hits[i].distance < minDist)
                {
                    minDist = hits[i].distance;
                    best = v;
                }
            }

            if (best != null)
            {
                bool ok = _ctrl.Service.TryStack(Inst, best.Inst);
                if (ok)
                {
                    // этот инстанс удалён → уничтожаем вью
                    Destroy(gameObject);
                    return true;
                }
            }
            return false;
        }

        private bool RayHitsThis()
        {
            var cam = _ctrl.playerCamera ? _ctrl.playerCamera : Camera.main;
            if (!cam) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                if (hit.collider == null) return false;
                // ищем среди наших коллайдеров
                if (_colliders != null)
                {
                    for (int i = 0; i < _colliders.Length; i++)
                        if (_colliders[i] == hit.collider) return true;
                }
                // запасной путь — сравнить родителя
                return hit.collider.GetComponentInParent<Item3DView>() == this;
            }
            return false;
        }

        private void SetDragTint(Color c)
        {
            if (_renderers == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(_ColorId, c);
                _mpb.SetColor(_BaseColorId, c);
                _mpb.SetColor(_TintId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        private static Rotation NextRotation(Rotation r)
        {
            if (r == Rotation.R0) return Rotation.R90;
            if (r == Rotation.R90) return Rotation.R180;
            if (r == Rotation.R180) return Rotation.R270;
            return Rotation.R0;
        }

        // ---- Геометрия/Bounds ----

        private void EnsureCollider(GameObject root)
        {
            var cols = root.GetComponentsInChildren<Collider>(true);
            if (cols != null && cols.Length > 0) return;

            // Добавим BoxCollider по локальному AABB модели
            var lb = CalcLocalAABBRelativeToRoot(root.transform, root);
            var bc = root.AddComponent<BoxCollider>();
            bc.center = lb.center;
            bc.size   = new Vector3(
                Mathf.Max(0.01f, lb.size.x),
                Mathf.Max(0.01f, lb.size.y),
                Mathf.Max(0.01f, lb.size.z)
            );
        }

        private Bounds CalcLocalAABBRelativeToRoot(Transform root, GameObject go)
        {
            bool has = false;
            Bounds aabb = new Bounds(Vector3.zero, Vector3.zero);

            // MeshFilter
            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mfs.Length; i++)
            {
                var mesh = mfs[i].sharedMesh;
                if (!mesh) continue;
                Matrix4x4 m = root.worldToLocalMatrix * mfs[i].transform.localToWorldMatrix;
                Bounds b = mesh.bounds; // локальные bounds меша
                EncapsulateTransformedAABB(ref aabb, ref has, m, b);
            }

            // SkinnedMeshRenderer
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

        private void AlignModelBottomCenterToOrigin_Local()
        {
            if (_model == null) return;
            Bounds lb = CalcLocalAABBRelativeToRoot(transform, _model); // в локали view
            Vector3 offset = new Vector3(lb.center.x, lb.min.y, lb.center.z);
            _model.transform.localPosition -= offset;
        }

        private Vector2Int GetRotatedFootprintExtents(ItemDefinition def, Rotation rot)
        {
            int w = def.shapeWidth, h = def.shapeHeight;
            bool any = false;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

            for (int sy = 0; sy < h; sy++)
            for (int sx = 0; sx < w; sx++)
            {
                if (!def.Occupies(sx, sy)) continue;
                any = true;
                var pr = RotatePoint(sx, sy, w, h, rot);
                int rx = pr.x, ry = pr.y;
                if (rx < minX) minX = rx; if (ry < minY) minY = ry;
                if (rx > maxX) maxX = rx; if (ry > maxY) maxY = ry;
            }

            if (!any) return new Vector2Int(1, 1);
            return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
        }

        private (int x, int y) RotatePoint(int x, int y, int w, int h, Rotation r)
        {
            if (r == Rotation.R0)   return (x, y);
            if (r == Rotation.R90)  return (h - 1 - y, x);
            if (r == Rotation.R180) return (w - 1 - x, h - 1 - y);
            if (r == Rotation.R270) return (y, w - 1 - x);
            return (x, y);
        }
    }
}