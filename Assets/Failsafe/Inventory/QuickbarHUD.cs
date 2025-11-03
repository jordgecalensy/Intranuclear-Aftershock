// QuickbarHUD.cs
using UnityEngine;
using UnityEngine.UI;

namespace Failsafe.Inventory
{
    public class QuickbarHUD : MonoBehaviour
    {
        [Header("Refs")]
        public InventoryController inventory;
        public QuickbarEquipManager equip;
        public Button[] slotButtons;     // 5 кнопок
        public Text[] slotLabels;        // подписи под кнопками (имя + xN)
        public Image[] highlights;       // подсветка активного

        [Header("Badges (optional)")]
        [Tooltip("Небольшие иконки 'занято' поверх кнопок слотов.")]
        public Image[] occupiedBadges;

        private void Awake()
        {
            if (!inventory) inventory = InventoryController.Instance;
        }

        private void OnEnable()
        {
            if (inventory?.Service == null) return;

            // Подписки ИМЕННО методами (чтобы корректно отписываться)
            inventory.Service.OnQuickbarAssigned += HandleQuickbarChanged;
            inventory.Service.OnQuickbarSwapped  += HandleQuickbarSwapped;
            inventory.Service.OnItemStacked      += HandleItemStacked;

            RefreshAll();

            // клики
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int idx = i;
                slotButtons[i].onClick.AddListener(()=> equip.EquipFromQuickbar(idx));
            }
        }

        private void OnDisable()
        {
            if (inventory?.Service == null) return;

            inventory.Service.OnQuickbarAssigned -= HandleQuickbarChanged;
            inventory.Service.OnQuickbarSwapped  -= HandleQuickbarSwapped;
            inventory.Service.OnItemStacked      -= HandleItemStacked;

            // чтобы не копились обработчики при повторном OnEnable
            for (int i = 0; i < slotButtons.Length; i++)
            {
                slotButtons[i].onClick.RemoveAllListeners();
            }
        }

        private void HandleQuickbarChanged(ItemInstance _, int __) => RefreshAll();
        private void HandleQuickbarSwapped(int _, int __) => RefreshAll();
        private void HandleItemStacked(ItemInstance _, int __) => RefreshAll();

        public void RefreshAll()
        {
            if (inventory == null) return;
            var model = inventory.Model;
            int n = Mathf.Min(slotLabels.Length, model.QuickbarSlots.Length);

            for (int i = 0; i < n; i++)
            {
                var id = model.QuickbarSlots[i];
                if (string.IsNullOrEmpty(id) || !model.Instances.TryGetValue(id, out var inst))
                {
                    if (slotLabels[i]) slotLabels[i].text = $"{i+1}";
                    if (highlights != null && i < highlights.Length && highlights[i]) highlights[i].enabled = false;
                    if (occupiedBadges != null && i < occupiedBadges.Length && occupiedBadges[i]) occupiedBadges[i].enabled = false;
                    continue;
                }

                var name = string.IsNullOrEmpty(inst.Def.name) ? $"Item{i+1}" : inst.Def.name;
                var stack = inst.Def.maxStack > 1 && inst.Stack > 1 ? $" x{inst.Stack}" : "";
                var heavy = inst.Def.isHeavy ? " [H]" : "";
                if (slotLabels[i]) slotLabels[i].text = $"{i+1} {name}{stack}{heavy}";

                if (occupiedBadges != null && i < occupiedBadges.Length && occupiedBadges[i])
                    occupiedBadges[i].enabled = true;
            }
        }

        // Вызови это из EquipManager после смены активного слота (либо подпиской на событие)
        public void SetActive(int index)
        {
            for (int i = 0; i < highlights.Length; i++)
                if (highlights[i]) highlights[i].enabled = (i == index);
        }
    }
}
