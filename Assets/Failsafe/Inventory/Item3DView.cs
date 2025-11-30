using UnityEngine;

namespace Failsafe.Inventory
{
    /// 3D-представление предмета на доске. Drag&drop,
    /// освобождение клеток на время драга, назначение в квикбар (с учётом span).
    public class Item3DView : MonoBehaviour
    {
        public ItemInstance Inst { get; private set; }

        private CaseProxy _board;
        private InventoryController _ctrl;
        private GameObject _model;

        private bool _drag;
        private bool _freedOnDrag;
        private Rotation _rot = Rotation.R0;
        private Vector3 _origPos;
        private Quaternion _origRot;
        private GridCoord _origGridPos;
        private Rotation _origGridRot;
        private bool _hasOrigPlacement;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private static readonly int _ColorId     = Shader.PropertyToID("_Color");
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _TintId      = Shader.PropertyToID("_TintColor");

        private Color _tintNormal      = Color.white;
        private Color _tintDragNeutral = new Color(1f, 0.95f, 0.60f, 1f);
        private Color _tintDragValid   = new Color(0.60f, 1f, 0.60f, 1f);
        private Color _tintDragInvalid = new Color(1f, 0.60f, 0.60f, 1f);

        private Vector3 _baseScale = Vector3.one;
        private Collider[] _colliders;

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

        private void Update()
        {
            if (_ctrl == null || _board == null || Inst == null) return;
            if (!Cursor.visible) return;

            if (!_drag)
            {
                bool wantsUse = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.F);
                if (wantsUse && RayHitsThis()) { TryUseInInventory(); return; }
            }

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

                        grid.FreeByInstance(Inst.Id); // освобождаем свои клетки на время драга
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
                if (Inst.Def.canRotate && Input.GetKeyDown(KeyCode.R))
                    _rot = NextRotation(_rot);

                for (int k = 0; k < _ctrl.Model.QuickbarSlots.Length; k++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + k) || Input.GetKeyDown(KeyCode.Keypad1 + k))
                    {
                        TryAssignToQuickbarIndex(k);
                        return;
                    }
                }

                int qSlot; Vector3 qWorld;
                if (_board.ScreenToQuickbarSlot(Input.mousePosition, out qSlot, out qWorld))
                {
                    int span = Mathf.Clamp(Inst.Def.quickbarSpan, 1, 2);
                    bool valid = IsQuickbarPlacementValid(qSlot, span);

                    _board.ShowQuickbarDockHoverSpan(qSlot, span, valid);
                    _board.ClearHover();
                    SetDragTint(valid ? _tintDragValid : _tintDragInvalid);

                    if (Input.GetMouseButtonUp(0) && valid)
                    {
                        AssignToQuickbar(qSlot, span);
                        EndDrag(false);
                    }
                    else if (Input.GetMouseButtonUp(0) && !valid)
                    {
                        RestoreOriginalPlacementGrid();
                        SnapBackToOriginalTile();
                        EndDrag(false);
                    }
                    return;
                }
                else
                {
                    _board.ClearQuickbarDockHover();
                }

                int cx, cy; Vector3 worldOnBoard;
                if (_board.ScreenToCell(Input.mousePosition, out cx, out cy, out worldOnBoard))
                {
                    var pos = new GridCoord(cx, cy);
                    bool can = _ctrl.Placement.CanPlace(_ctrl.Model.Grids[_ctrl.playerGridId], Inst.Def, pos, _rot);

                    Vector3 center = _board.CellToWorldCenter(cx, cy);
                    transform.position = center;
                    transform.rotation = _board.RotationToWorld(_rot);

                    _board.ShowHover(Inst.Def, pos, _rot, can);
                    SetDragTint(can ? _tintDragValid : _tintDragInvalid);

                    if (Input.GetMouseButtonUp(0))
                    {
                        bool ok = _ctrl.Service.TryMove(_ctrl.playerGridId, Inst, pos, _rot);
                        if (ok)
                        {
                            var p = _ctrl.Model.Grids[_ctrl.playerGridId].GetPlacement(Inst.Id);
                            if (p.HasValue)
                            {
                                Vector3 center2 = _board.CellToWorldCenter(p.Value.pos.X, p.Value.pos.Y);
                                transform.position = center2;
                                transform.rotation = _board.RotationToWorld(p.Value.rot);
                            }
                            _freedOnDrag = false;
                            EndDrag(false);
                        }
                        else
                        {
                            if (TryStackIntoTargetUnderCursor())
                            {
                                _freedOnDrag = false;
                                EndDrag(false);
                            }
                            else
                            {
                                RestoreOriginalPlacementGrid();
                                SnapBackToOriginalTile();
                                EndDrag(false);
                            }
                        }
                    }
                }
                else
                {
                    transform.position = worldOnBoard;
                    transform.rotation = _board.RotationToWorld(_rot);

                    _board.ClearHover();
                    SetDragTint(_tintDragInvalid);

                    if (Input.GetMouseButtonUp(0))
                    {
                        _ctrl.Service.Remove(_ctrl.playerGridId, Inst);
                        _ctrl.DropToWorld(Inst);
                        Destroy(gameObject);
                    }
                }
            }
        }

        // -------- quickbar helpers --------
        private bool IsQuickbarPlacementValid(int index, int span)
        {
            var slots = _ctrl.Model.QuickbarSlots;
            if (span <= 1)
                return index >= 0 && index < slots.Length && string.IsNullOrEmpty(slots[index]);

            if (index < 0 || index >= slots.Length - 1) return false;
            return string.IsNullOrEmpty(slots[index]) && string.IsNullOrEmpty(slots[index + 1]);
        }

        private void AssignToQuickbar(int index, int span)
        {
            _ctrl.Service.Remove(_ctrl.playerGridId, Inst);

            if (span <= 1)
            {
                _ctrl.Service.AssignQuickbarSlot(index, Inst, true);
            }
            else
            {
                var ok1 = _ctrl.Service.AssignQuickbarSlot(index, Inst, true);
                var ok2 = _ctrl.Service.AssignQuickbarSlot(index + 1, Inst, true);
                if (!ok1 || !ok2)
                {
                    if (ok1) _ctrl.Service.AssignQuickbarSlot(index, null, true);
                    _ctrl.Service.TryAdd(_ctrl.playerGridId, Inst);
                }
            }

            _freedOnDrag = false;
        }

        private void TryAssignToQuickbarIndex(int k)
        {
            int span = Mathf.Clamp(Inst.Def.quickbarSpan, 1, 2);
            bool valid = IsQuickbarPlacementValid(k, span);
            if (!valid)
            {
                RestoreOriginalPlacementGrid();
                SnapBackToOriginalTile();
                EndDrag(false);
                return;
            }
            AssignToQuickbar(k, span);
            EndDrag(false);
        }

        // -------- other helpers --------
        private void EndDrag(bool resetToOriginal)
        {
            _drag = false;
            _board.ClearHover();
            _board.ClearQuickbarDockHover();
            SetDragTint(_tintNormal);
            if (resetToOriginal) SnapBackToOriginalTile();
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

            if (!_ctrl.Model.Instances.ContainsKey(Inst.Id))
            {
                Destroy(gameObject);
                return;
            }

            var grid = _ctrl.Model.Grids[_ctrl.playerGridId];
            var p = grid.GetPlacement(Inst.Id);
            if (p.HasValue)
            {
                Vector3 center = _board.CellToWorldCenter(p.Value.pos.X, p.Value.pos.Y);
                transform.position = center;
                transform.rotation = _board.RotationToWorld(p.Value.rot);
            }

            var wi = _model.GetComponentInChildren<WorldItem>();
            if (wi && wi.IsUsable()) wi.Use();
        }

        private bool TryStackIntoTargetUnderCursor()
        {
            if (Inst.Def.maxStack <= 1) return false;

            var cam = _ctrl.playerCamera ? _ctrl.playerCamera : Camera.main;
            if (!cam) return false;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 3f)) return false;

            var other = hit.collider ? hit.collider.GetComponentInParent<Item3DView>() : null;
            if (other == null || other == this) return false;

            return _ctrl.Service.TryStack(Inst, other.Inst);
        }

        private void EnsureCollider(GameObject root)
        {
            _colliders = root.GetComponentsInChildren<Collider>(true);
            if (_colliders != null && _colliders.Length > 0) return;

            var mf = root.GetComponentInChildren<MeshFilter>();
            if (mf)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = mf.sharedMesh.bounds.center;
                box.size   = mf.sharedMesh.bounds.size;
            }
            else
            {
                root.AddComponent<BoxCollider>();
            }
            _colliders = root.GetComponentsInChildren<Collider>(true);
        }

        private bool RayHitsThis()
        {
            var cam = _ctrl.playerCamera ? _ctrl.playerCamera : Camera.main;
            if (!cam) return false;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 10f))
            {
                for (int i = 0; i < _colliders.Length; i++)
                    if (hit.collider == _colliders[i]) return true;
            }
            return false;
        }

        private void SetDragTint(Color c)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (!r) continue;
                _mpb ??= new MaterialPropertyBlock();
                r.GetPropertyBlock(_mpb);
                if (r.sharedMaterial && r.sharedMaterial.HasProperty(_BaseColorId)) _mpb.SetColor(_BaseColorId, c);
                else if (r.sharedMaterial && r.sharedMaterial.HasProperty(_ColorId)) _mpb.SetColor(_ColorId, c);
                if (r.sharedMaterial && r.sharedMaterial.HasProperty(_TintId)) _mpb.SetColor(_TintId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        private Vector2Int GetRotatedFootprintExtents(ItemDefinition def, Rotation r)
        {
            return r == Rotation.R90 || r == Rotation.R270
                ? new Vector2Int(def.shapeHeight, def.shapeWidth)
                : new Vector2Int(def.shapeWidth, def.shapeHeight);
        }

        private void AlignModelBottomCenterToOrigin_Local()
        {
            var b = CalcLocalAABBRelativeToRoot(transform, _model);
            var offset = new Vector3(b.center.x, b.min.y, b.center.z);
            _model.transform.localPosition -= offset;
        }

        private Bounds CalcLocalAABBRelativeToRoot(Transform root, GameObject go)
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

        private Rotation NextRotation(Rotation r)
        {
            return r switch
            {
                Rotation.R0   => Rotation.R90,
                Rotation.R90  => Rotation.R180,
                Rotation.R180 => Rotation.R270,
                Rotation.R270 => Rotation.R0,
                _             => Rotation.R0
            };
        }
    }
}
