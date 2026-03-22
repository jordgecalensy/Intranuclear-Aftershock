using Failsafe.Items;
using Failsafe.Player.View;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Предмет в руке персонажа
/// </summary>
public class ItemInHand
{
    public Item ItemObject;
    public IUsable ItemUsable;
}

/// <summary>
/// Руки персонажа как контейнер предмета
/// </summary>
public class PlayerHandsContainer
{
    public enum HandState { EmptyHands, ItemInHand }

    public event Action<ItemType> OnItemTaken;
    public event Action OnItemDropped;
    public HandState State => _handState;
    public ItemInHand ItemInHand => _itemInHand;

    private ItemInHand _itemInHand;
    private HandState _handState = HandState.EmptyHands;
    private IEnumerable<IUsable> _items;
    private Transform _rightHandItemPlace;

    private float _itemUseDelay = 0f;
    public float ItemUseDelay => _itemUseDelay;
    
    private float _itemUseStartDelay = 0f;
    public float ItemUseStartDelay => _itemUseStartDelay;

    public PlayerHandsContainer(IEnumerable<IUsable> items, PlayerView playerView)
    {
        _items = items;
        _rightHandItemPlace = playerView.RightHandItemPlace;
    }

    /// <summary>
    /// Поместить предмет в руку
    /// </summary>
    public bool TryTakeItemInHand(Item itemObject)
    {
        if (_handState == HandState.ItemInHand)
        {
            Debug.Log("TryTakeItemInHand. Не получилось взять предмет. Руки заняты");
            return false;
        }

        IUsable usableItem = null;

        // --- НОВАЯ ЛОГИКА: Поиск обработчика ---
        // 1. Если это Огнестрел (Gun), используем универсальный GunUsable
        if (itemObject.ItemData.Type == ItemType.Gun)
        {
            usableItem = _items.FirstOrDefault(x => x is GunUsable);
        }
        else
        {
            // 2. Для остальных предметов (аптечки, гранаты) ищем по совпадению имени класса
            // (Legacy подход для существующих предметов)
            usableItem = _items.FirstOrDefault(x => itemObject.name.StartsWith(x.GetType().Name));
        }

        if (usableItem == null)
        {
            Debug.LogError($"TryTakeItemInHand. Не найден IUsable скрипт для предмета {itemObject.name} (Тип: {itemObject.ItemData.Type})");
            return false;
        }

        // Логика размещения в руке
        Transform handlePoint = itemObject.HandlePoint;
        itemObject.ToInventoryState();
        itemObject.transform.SetParent(_rightHandItemPlace, false);
        
        // Коррекция позиции
        if (handlePoint != null)
            itemObject.transform.localPosition = handlePoint.localPosition * -1;
        else
            itemObject.transform.localPosition = Vector3.zero;

        // Инициализация предмета (ParseItem вытащит стратегию из GunStrategyHolder)
        usableItem.ParseItem(itemObject);

        var itemInHand = new ItemInHand
        {
            ItemObject = itemObject,
            ItemUsable = usableItem
        };

        _itemInHand = itemInHand;
        _handState = HandState.ItemInHand;
        
        // Получаем тайминги
        _itemInHand.ItemUsable.GetItemUseDelays(out _itemUseStartDelay, out _itemUseDelay);
        
        Debug.Log($"Предмет {itemObject.name} взят в руку");
        OnItemTaken?.Invoke(_itemInHand.ItemObject.ItemData.Type);
        return true;
    }

    /// <summary>
    /// Выбросить предмет из рук
    /// </summary>
    public Item DropItemFromHand()
    {
        if (_handState == HandState.EmptyHands)
        {
            return null;
        }
        var item = _itemInHand.ItemObject;
        item.ToWorldState();
        _rightHandItemPlace.DetachChildren();
        _itemInHand = null;
        _handState = HandState.EmptyHands;
        OnItemDropped?.Invoke();
        return item;
    }

    /// <summary>
    /// Очистить руку
    /// </summary>
    public void SetItemNull()
    {
        _itemInHand = null;
        _handState = HandState.EmptyHands;
    }
}