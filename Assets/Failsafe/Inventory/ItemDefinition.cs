using UnityEngine;

namespace Failsafe.Inventory
{
    public enum InventoryPoseMode { AutoFit, AutoFill, ManualMeters, ManualCells }
    public enum FitMode { UniformFit, UniformFill, Stretch }

    [CreateAssetMenu(menuName = "Failsafe/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string itemId;
        public string displayName;

        [Header("World Prefab")]
        public GameObject WorldPrefab;

        [Header("Shape (Grid)")]
        public int shapeWidth = 1;
        public int shapeHeight = 1;
        [Tooltip("true = клетка занята. Размер равен shapeWidth x shapeHeight.")]
        public bool[] footprint = new bool[1] { true };

        [Header("Stack/Flags")]
        public int maxStack = 1;
        public bool isHeavy;

        [Header("Inventory Pose")]
        public InventoryPoseMode poseMode = InventoryPoseMode.AutoFit;
        public FitMode fitMode = FitMode.UniformFit;
        public float scaleMultiplier = 1f;

        [Tooltip("ManualMeters: смещение в метрах; ManualCells: смещение в клетках по XZ и метрах по Y.")]
        public Vector3 manualLocalPositionMeters;
        public Vector2 manualOffsetCellsXZ;
        public float manualOffsetY;
        public Vector3 manualLocalEuler;
        public Vector3 manualLocalScale = Vector3.one;

        [Header("Quickbar UI")]
        [Tooltip("Иконка, которая будет показана в UI квикбара.")]
        public Sprite quickbarIcon;

        [Tooltip("Сколько слотов занимает предмет в квикбаре: 1 (обычный) или 2 (большой).")]
        [Range(1, 2)] public int quickbarSpan = 1;

        [Header("Rotation")]
        public bool canRotate = true;

        // ===== helpers =====
        public bool Occupies(int x, int y)
        {
            if (x < 0 || y < 0 || x >= shapeWidth || y >= shapeHeight) return false;
            int idx = y * shapeWidth + x;
            if (footprint == null || idx < 0 || idx >= footprint.Length) return false;
            return footprint[idx];
        }
    }
}
