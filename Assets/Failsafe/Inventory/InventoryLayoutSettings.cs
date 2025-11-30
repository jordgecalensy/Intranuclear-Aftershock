using UnityEngine;

namespace Failsafe.Inventory
{
    [CreateAssetMenu(menuName = "Failsafe/Inventory Layout Settings")]
    public class InventoryLayoutSettings : ScriptableObject
    {
        [Header("Grid (world-case)")]
        public float cellSize = 0.06f;
        public float cellGap = 0.004f;
        public float itemYOffset = 0.01f;
        public float itemScaleMultiplier = 1.0f;

        [Header("Quickbar Dock (world-case)")]
        public QuickbarDockSide quickbarDockSide = QuickbarDockSide.Right;
        public float quickbarRowGap = 0.02f;
        public bool createQuickbarTiles = true;

        [Header("Quickbar UI (Canvas)")]
        public Vector2 quickbarSlotSize = new Vector2(64, 64);
        public float quickbarUISpacing = 8f;
        public QuickbarDockSide quickbarUIAnchor = QuickbarDockSide.Bottom;
        public Vector2 quickbarUIOffset = new Vector2(8f, 8f);

        [Header("Inventory UI")]
        [Tooltip("Количество колонок в UI-инвентаре (0 = использовать логику по умолчанию).")]
        [Min(1)] public int uiColumnsCount = 2;
        [Tooltip("Фон/картинка для ячейки UI инвентаря (если задано, применяется ко всем слотам).")]
        public Sprite cellSprite;

        [Header("Editor / Runtime")]
        public bool applyOnStart = true;
    }
}