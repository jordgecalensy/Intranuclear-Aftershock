// ItemDefinition.cs
using UnityEngine;

namespace Failsafe.Inventory
{
    public enum FitMode
    {
        Stretch,     // растянуть по X/Z (даёт искажения, используйте редко)
        UniformFit,  // вписать без искажений (по меньшей стороне)
        UniformFill  // заполнить без искажений (по большей стороне)
    }

    public enum InventoryPoseMode
    {
        AutoFit,      // автоподгон: UniformFit
        AutoFill,     // автоподгон: UniformFill
        ManualMeters, // ручная поза: позиция в метрах в локали Item3DView
        ManualCells   // ручная поза: смещение по X/Z в "клетках" + Y в метрах
    }

    [CreateAssetMenu(menuName = "Failsafe/Inventory/Item Definition", fileName = "ItemDefinition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("World")]
        [Tooltip("Префаб, который используется и в мире, и для показа в инвентаре.")]
        public GameObject WorldPrefab;

        [Header("Meta (опционально)")]
        [Tooltip("Человекочитаемое имя в HUD. Если пусто — берётся name SO.")]
        public string displayName;

        [Header("Grid Footprint")]
        [Min(1)] public int shapeWidth = 1;
        [Min(1)] public int shapeHeight = 1;

        [Tooltip("Матрица W×H по строкам (row-major). true = клетка занята.")]
        public bool[] footprint = new bool[1] { true };

        [Header("Stack/Flags")]
        [Min(1)] public int maxStack = 1;
        public bool isHeavy = false;
        public bool canRotate = true;

        [Header("Visual Fit (авторежим)")]
        public FitMode fitMode = FitMode.UniformFit;
        [Tooltip("Доп. множитель масштаба в авторежиме.")]
        public float scaleMultiplier = 1f;

        [Header("Inventory Pose Override (ручной режим)")]
        public InventoryPoseMode poseMode = InventoryPoseMode.AutoFit;

        [Tooltip("Ручной локальный масштаб модели (локаль Item3DView).")]
        public Vector3 manualLocalScale = Vector3.one;

        [Tooltip("Ручной локальный поворот модели в градусах.")]
        public Vector3 manualLocalEuler = Vector3.zero;

        [Tooltip("ManualMeters: локальная позиция модели в метрах.")]
        public Vector3 manualLocalPositionMeters = Vector3.zero;

        [Tooltip("ManualCells: смещение по X/Z в 'клетках' относительно центра тайла.")]
        public Vector2 manualOffsetCellsXZ = Vector2.zero;

        [Tooltip("ManualCells: смещение по Y (в метрах).")]
        public float manualOffsetY = 0f;

        // ------------------- API -------------------

        /// Возвращает, занята ли клетка формы (x,y) в пределах W×H.
        public bool Occupies(int x, int y)
        {
            if (x < 0 || y < 0 || x >= shapeWidth || y >= shapeHeight) return false;
            if (footprint == null || footprint.Length != shapeWidth * shapeHeight) return true; // трактуем как прямоугольник
            return footprint[y * shapeWidth + x];
        }

        /// Установить значение клетки footprint (безопасно по границам).
        public void SetFootprint(int x, int y, bool value)
        {
            if (x < 0 || y < 0 || x >= shapeWidth || y >= shapeHeight) return;
            EnsureFootprintSize();
            footprint[y * shapeWidth + x] = value;
        }

        /// Очистить все клетки (false).
        [ContextMenu("Footprint/Clear All")]
        public void ClearFootprint()
        {
            EnsureFootprintSize();
            for (int i = 0; i < footprint.Length; i++) footprint[i] = false;
        }

        /// Заполнить прямоугольник W×H (true).
        [ContextMenu("Footprint/Fill Rectangle")]
        public void FillFootprint()
        {
            EnsureFootprintSize();
            for (int i = 0; i < footprint.Length; i++) footprint[i] = true;
        }

        // ------------------- Validation -------------------

        private void OnValidate()
        {
            // нормализуем размеры формы
            shapeWidth = Mathf.Max(1, shapeWidth);
            shapeHeight = Mathf.Max(1, shapeHeight);

            // следим за валидностью стэка и масштаба
            maxStack = Mathf.Max(1, maxStack);
            if (scaleMultiplier <= 0f) scaleMultiplier = 1f;

            // привести массив footprint к нужной длине
            EnsureFootprintSize();

            // если включён ручной режим, убедимся, что масштаб не вырожден
            if (poseMode == InventoryPoseMode.ManualMeters || poseMode == InventoryPoseMode.ManualCells)
            {
                manualLocalScale.x = Mathf.Max(0.0001f, manualLocalScale.x);
                manualLocalScale.y = Mathf.Max(0.0001f, manualLocalScale.y);
                manualLocalScale.z = Mathf.Max(0.0001f, manualLocalScale.z);
            }
        }

        private void EnsureFootprintSize()
        {
            int need = Mathf.Max(1, shapeWidth * shapeHeight);
            if (footprint == null || footprint.Length != need)
            {
                var old = footprint;
                footprint = new bool[need];

                if (old != null && old.Length > 0)
                {
                    // копируем пересечение старой/новой матрицы,
                    // предполагая прежнюю раскладку row-major
                    int copy = Mathf.Min(old.Length, footprint.Length);
                    for (int i = 0; i < copy; i++) footprint[i] = old[i];
                }
                else
                {
                    // по умолчанию предмет 1×1 занимает свою единственную клетку
                    if (need > 0) footprint[0] = true;
                }
            }
        }
    }
}