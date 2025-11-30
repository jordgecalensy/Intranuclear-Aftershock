using UnityEngine;
using Failsafe.Player.View;
using Failsafe.Items;

namespace Failsafe.Inventory
{
    public class QuickbarEquipManager : MonoBehaviour
    {
        [Header("Refs")]
        public InventoryController inventory;
        public Transform handsFallbackAttach;
        public PlayerView playerView;
        public PlayerHandsContainer hands;

        public bool dropOnReturnFail = true;

        private int _activeSlot = -1;
        private string _equippedInstanceId;

        private void Awake()
        {
            if (!inventory) inventory = InventoryController.Instance;
        }

        private void Update()
        {
            for (int i = 0; i < inventory.Model.QuickbarSlots.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    EquipFromQuickbar(i);
        }

        public void EquipFromQuickbar(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= inventory.Model.QuickbarSlots.Length) return;

            var id = inventory.Model.QuickbarSlots[slotIndex];
            if (string.IsNullOrEmpty(id))
            {
                UnequipCurrent();
                _activeSlot = -1;
                return;
            }

            if (_equippedInstanceId == id && _activeSlot == slotIndex) return;

            UnequipCurrent();

            if (!inventory.Model.Instances.TryGetValue(id, out var inst))
            {
                _activeSlot = slotIndex; _equippedInstanceId = id; return;
            }

            // НЕ убираем предмет из инвентаря. Просто спавним визуал для рук.
            var prefab = inst.Def.WorldPrefab;
            if (!prefab)
            {
                Debug.LogWarning($"[{nameof(QuickbarEquipManager)}] У предмета {inst.Def.name} нет WorldPrefab.");
                _activeSlot = slotIndex; _equippedInstanceId = id; return;
            }

            var go = Instantiate(prefab);
            var itemComponent = go.GetComponent<Item>();

            if (hands != null && itemComponent != null)
            {
                var ok = hands.TryTakeItemInHand(itemComponent);
                if (!ok)
                {
                    Destroy(go);
                    _activeSlot = slotIndex; _equippedInstanceId = id;
                    return;
                }
            }
            else
            {
                var t = handsFallbackAttach ? handsFallbackAttach
                        : (playerView ? playerView.RightHandItemPlace : inventory.player);
                go.transform.SetParent(t, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }

            _activeSlot = slotIndex;
            _equippedInstanceId = id;
        }

        public void UnequipCurrent()
        {
            if (_equippedInstanceId == null) return;

            if (hands != null)
            {
                var dropped = hands.DropItemFromHand();
                if (dropped) Destroy(dropped.gameObject);
                hands.SetItemNull();
            }
            else if (handsFallbackAttach && handsFallbackAttach.childCount > 0)
            {
                for (int i = handsFallbackAttach.childCount - 1; i >= 0; i--)
                    Destroy(handsFallbackAttach.GetChild(i).gameObject);
            }

            _equippedInstanceId = null;
        }
    }
}
