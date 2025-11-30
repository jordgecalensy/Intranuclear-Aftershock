using UnityEngine;
using UnityEngine.UI;

namespace Failsafe.Inventory
{
    /// HUD квикбара: иконки из SO, бейдж «занято», выбор слота.
    public class QuickbarHUD : MonoBehaviour
    {
        [Header("Refs")]
        public InventoryController inventory;
        public QuickbarEquipManager equip;

        [Header("UI Controls")]
        public Button[] slotButtons;
        public Text[]   slotLabels;
        public Image[]  highlights;
        public Image[]  occupiedBadges;
        public Image[]  slotIconImages;   // картинки-иконки слотов

        private void Awake()
        {
            if (!inventory) inventory = InventoryController.Instance;
        }

        private void OnEnable()
        {
            if (inventory?.Service == null) return;

            inventory.Service.OnQuickbarAssigned += HandleQuickbarChanged;
            inventory.Service.OnQuickbarSwapped  += HandleQuickbarSwapped;
            inventory.Service.OnItemStacked      += HandleItemStacked;

            WireButtons();
            RefreshAll();
        }

        private void OnDisable()
        {
            if (inventory?.Service == null) return;

            inventory.Service.OnQuickbarAssigned -= HandleQuickbarChanged;
            inventory.Service.OnQuickbarSwapped  -= HandleQuickbarSwapped;
            inventory.Service.OnItemStacked      -= HandleItemStacked;

            UnwireButtons();
        }

        private void WireButtons()
        {
            if (slotButtons == null) return;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int idx = i;
                if (slotButtons[i]) slotButtons[i].onClick.AddListener(()=> equip.EquipFromQuickbar(idx));
            }
        }

        private void UnwireButtons()
        {
            if (slotButtons == null) return;
            for (int i = 0; i < slotButtons.Length; i++)
                if (slotButtons[i]) slotButtons[i].onClick.RemoveAllListeners();
        }

        private void HandleQuickbarChanged(ItemInstance _, int __) => RefreshAll();
        private void HandleQuickbarSwapped(int _, int __) => RefreshAll();
        private void HandleItemStacked(ItemInstance _, int __) => RefreshAll();

        public void RefreshAll()
        {
            if (inventory == null) return;

            var model = inventory.Model;
            var slots = model.QuickbarSlots;
            int n = slots.Length;

            for (int i = 0; i < n; i++)
            {
                string id = slots[i];
                ItemInstance inst = null;
                if (!string.IsNullOrEmpty(id))
                    model.Instances.TryGetValue(id, out inst);

                if (inst == null)
                {
                    if (slotLabels      != null && i < slotLabels.Length      && slotLabels[i])      slotLabels[i].text = $"{i+1}";
                    if (occupiedBadges  != null && i < occupiedBadges.Length  && occupiedBadges[i])  occupiedBadges[i].enabled = false;

                    if (slotIconImages  != null && i < slotIconImages.Length  && slotIconImages[i])
                    {
                        slotIconImages[i].sprite = null;
                        var col = slotIconImages[i].color; col.a = 0f; slotIconImages[i].color = col;
                    }
                }
                else
                {
                    var name  = string.IsNullOrEmpty(inst.Def.displayName) ? inst.Def.name : inst.Def.displayName;
                    var stack = inst.Def.maxStack > 1 && inst.Stack > 1 ? $" x{inst.Stack}" : "";
                    if (slotLabels     != null && i < slotLabels.Length     && slotLabels[i])     slotLabels[i].text = $"{i+1} {name}{stack}";
                    if (occupiedBadges != null && i < occupiedBadges.Length && occupiedBadges[i]) occupiedBadges[i].enabled = true;

                    if (slotIconImages != null && i < slotIconImages.Length && slotIconImages[i])
                    {
                        slotIconImages[i].sprite = inst.Def.quickbarIcon;
                        var col = slotIconImages[i].color; col.a = inst.Def.quickbarIcon ? 1f : 0f; slotIconImages[i].color = col;
                        slotIconImages[i].preserveAspect = true;
                    }
                }
            }
        }

        public void SetActive(int index)
        {
            if (highlights == null) return;
            for (int i = 0; i < highlights.Length; i++)
                if (highlights[i]) highlights[i].enabled = (i == index);
        }
    }
}
