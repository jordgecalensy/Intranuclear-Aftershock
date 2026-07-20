using Failsafe.Items;
using Failsafe.Player.View;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Предмет в руке персонажа.
/// </summary>
public class ItemInHand
{
    public Item ItemObject;
    public IUsable ItemUsable;
}

/// <summary>
/// Руки персонажа как контейнер предмета.
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

    private readonly IEnumerable<IUsable> _items;
    private readonly Transform _rightHandItemPlace;

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
    /// Поместить предмет в руку.
    /// </summary>
    public bool TryTakeItemInHand(Item itemObject)
    {
        if (itemObject == null)
            return false;

        if (itemObject.ItemData == null)
        {
            Debug.LogError($"TryTakeItemInHand. У предмета {itemObject.name} не назначен ItemData.", itemObject);
            return false;
        }

        if (_handState == HandState.ItemInHand)
        {
            return false;
        }

        IUsable usableItem = ResolveUsable(itemObject);

        if (usableItem == null)
        {
            Debug.LogError(
                $"TryTakeItemInHand. Не найден IUsable для предмета {itemObject.name} (Тип: {itemObject.ItemData.Type})",
                itemObject);

            return false;
        }

        Transform handlePoint = itemObject.HandlePoint;

        itemObject.ToInventoryState();
        itemObject.transform.SetParent(_rightHandItemPlace, false);

        if (handlePoint != null)
        {
            itemObject.transform.localPosition = handlePoint.localPosition * -1;
            itemObject.transform.rotation = _rightHandItemPlace.rotation;
        }
        else
        {
            Debug.LogWarning("У предмета " + itemObject.name + "нет handlepoint, используем дефолт");
            itemObject.transform.localPosition = Vector3.zero;
        }

        usableItem.ParseItem(itemObject);

        _itemInHand = new ItemInHand
        {
            ItemObject = itemObject,
            ItemUsable = usableItem
        };

        _handState = HandState.ItemInHand;

        _itemInHand.ItemUsable.GetItemUseDelays(
            out _itemUseStartDelay,
            out _itemUseDelay);

        OnItemTaken?.Invoke(_itemInHand.ItemObject.ItemData.Type);

        return true;
    }

    private IUsable ResolveUsable(Item itemObject)
    {
        IUsable componentUsable = ResolveComponentUsable(itemObject);

        if (componentUsable != null)
            return componentUsable;

        IUsable registeredUsable = ResolveRegisteredUsable(itemObject);

        if (registeredUsable != null)
            return registeredUsable;

        if (itemObject.IsUsable())
            return new LegacyActionsGroupUsable(itemObject);

        return null;
    }

    private static IUsable ResolveComponentUsable(Item itemObject)
    {
        return itemObject
            .GetComponents<MonoBehaviour>()
            .OfType<IUsable>()
            .FirstOrDefault();
    }

    private IUsable ResolveRegisteredUsable(Item itemObject)
    {
        ItemType type = itemObject.ItemData.Type;

        switch (type)
        {
            case ItemType.StasisGun:
                return _items.FirstOrDefault(x => x is StasisGun);

            case ItemType.Gun:
                return _items.FirstOrDefault(x => x is GunUsable);

            default:
                return _items.FirstOrDefault(x => itemObject.name.StartsWith(x.GetType().Name));
        }
    }

    private sealed class LegacyActionsGroupUsable : IUsable
    {
        private Item _item;

        public LegacyActionsGroupUsable(Item item)
        {
            _item = item;
        }

        public ItemUseResult Use()
        {
            _item?.Use();

            return new ItemUseResult
            {
                UsageType = UsageType.ClickToUse,
                ItemStateAfterUse = ItemState.Hold
            };
        }

        public void AltMode()
        {
        }

        public void ParseItem(Item item_object)
        {
            if (item_object != null)
                _item = item_object;
        }

        public void GetItemUseDelays(out float startDelay, out float useDelay)
        {
            startDelay = 0f;
            useDelay = 0.2f;

            if (_item?.ItemData == null)
                return;

            startDelay = Mathf.Max(0f, _item.ItemData.StartUseDelay);
            useDelay = Mathf.Max(0f, _item.ItemData.UseDelay);
        }
    }

    /// <summary>
    /// Выбросить предмет из рук.
    /// </summary>
    public Item DropItemFromHand()
    {
        if (_handState == HandState.EmptyHands)
            return null;

        Item item = _itemInHand.ItemObject;

        item.ToWorldState();

        _rightHandItemPlace.DetachChildren();

        _itemInHand = null;
        _handState = HandState.EmptyHands;

        OnItemDropped?.Invoke();

        return item;
    }

    /// <summary>
    /// Очистить руку.
    /// </summary>
    public void SetItemNull()
    {
        _itemInHand = null;
        _handState = HandState.EmptyHands;
    }

    public Item PlaceItem(Transform place)
    {
        if (_handState == HandState.EmptyHands)
            return null;

        Item item = _itemInHand.ItemObject;

        Vector3 position = place.position;
        position.y += 0.2f;

        Quaternion rotation = place.rotation;

        item.transform.SetPositionAndRotation(position, rotation);
        item.transform.Rotate(0, 0, -90);
        item.ToWorldState();

        _rightHandItemPlace.DetachChildren();

        _itemInHand = null;
        _handState = HandState.EmptyHands;

        return item;
    }
}
