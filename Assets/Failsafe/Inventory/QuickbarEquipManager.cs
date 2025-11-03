// QuickbarEquipManager.cs
using System.Collections.Generic;
using UnityEngine;
using Failsafe.Player.View;
using Failsafe.Items;

namespace Failsafe.Inventory
{
    public class QuickbarEquipManager : MonoBehaviour
    {
        [Header("Refs")]
        public InventoryController inventory;     // укажи в инспекторе
        public Transform handsFallbackAttach;     // если PlayerHandsContainer недоступен — куда "брать в руки"
        public PlayerView playerView;            // если используешь свой PlayerView
        public PlayerHandsContainer hands;       // опционально: если твоё ядро рук создаётся где-то ещё — сюда ссылку

        [Header("Behaviour")]
        public bool returnPreviousToInventory = true;   // при переключении слота вернуть в инвентарь
        public bool dropOnReturnFail = true;            // если не влез — выбросить в мир

        private int _activeSlot = -1;                   // выбранный слот
        private string _equippedInstanceId = null;      // какой инстанс сейчас в руках

        void Awake()
        {
            if (!inventory) inventory = InventoryController.Instance;
        }

        void Update()
        {
            // цифры -> выбор слота (работает всегда, хоть инвентарь открыт, хоть закрыт)
            for (int i = 0; i < inventory.Model.QuickbarSlots.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    EquipFromQuickbar(i);
                }
            }
        }

        public void EquipFromQuickbar(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= inventory.Model.QuickbarSlots.Length) return;

            var id = inventory.Model.QuickbarSlots[slotIndex];
            if (string.IsNullOrEmpty(id))
            {
                // пустой слот → можно рассматривать как "убрать из рук"
                UnequipCurrent();
                _activeSlot = -1;
                return;
            }

            // если этот же — просто переподтвердить
            if (_equippedInstanceId == id && _activeSlot == slotIndex) return;

            // снять предыдущий в руках
            UnequipCurrent();

            // взять новый
            if (!inventory.Model.Instances.TryGetValue(id, out var inst)) return;

            // убрать с доски (если лежал)
            inventory.Service.Remove(inventory.playerGridId, inst);

            // заспавнить world-префаб
            var prefab = inst.Def.WorldPrefab;
            if (!prefab) { Debug.LogWarning($"[{nameof(QuickbarEquipManager)}] У предмета {inst.Def.name} нет WorldPrefab."); return; }

            var go = Instantiate(prefab);
            // ⚠️ Требование к префабу: на нём должен быть твой Failsafe.Items.Item
            var itemComponent = go.GetComponent<Item>();
            if (hands != null && itemComponent != null)
            {
                // через твою систему рук
                var ok = hands.TryTakeItemInHand(itemComponent); // берём в руку
                if (!ok)
                {
                    Destroy(go);
                    // попытка вернуть в инвентарь
                    if (!inventory.Service.TryAdd(inventory.playerGridId, inst) && dropOnReturnFail)
                        inventory.DropToWorld(inst);
                    return;
                }
            }
            else
            {
                // фоллбек — прикрепить к точке в руках
                var t = handsFallbackAttach ? handsFallbackAttach
                        : (playerView ? playerView.RightHandItemPlace : inventory.player);
                go.transform.SetParent(t, worldPositionStays: false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }

            _equippedInstanceId = id;
            _activeSlot = slotIndex;
        }

        public void UnequipCurrent()
        {
            if (_equippedInstanceId == null) return;

            // 1) если есть руки — отпустить то, что в них
            if (hands != null)
            {
                var dropped = hands.DropItemFromHand(); // вернёт Item, уже переведённый ToWorldState
                if (dropped)
                {
                    // Мы не хотим бросать физику на землю — просто удалить визуал
                    Destroy(dropped.gameObject);
                }
                // очистить состояние рук
                hands.SetItemNull();
            }
            else
            {
                // иначе — попробовать найти ребёнка у fallback-узла и удалить
                if (handsFallbackAttach && handsFallbackAttach.childCount > 0)
                {
                    for (int i = handsFallbackAttach.childCount - 1; i >= 0; i--)
                        Destroy(handsFallbackAttach.GetChild(i).gameObject);
                }
            }

            // 2) вернуть инстанс в инвентарь
            if (inventory.Model.Instances.TryGetValue(_equippedInstanceId, out var inst))
            {
                bool back = inventory.Service.TryAdd(inventory.playerGridId, inst);
                if (!back && dropOnReturnFail)
                {
                    inventory.DropToWorld(inst);
                }
            }

            _equippedInstanceId = null;
        }
    }
}