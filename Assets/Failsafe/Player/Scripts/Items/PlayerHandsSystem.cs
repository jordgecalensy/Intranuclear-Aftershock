using Cysharp.Threading.Tasks;
using Failsafe.Items;
using Failsafe.Player.View;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Использование предметов в руках
/// </summary>
public class PlayerHandsSystem : ITickable
{
    public enum UsingState { None, Start, Using, OnDelay }
    public event Action<ItemType> OnItemStartUsing;
    public UsingState ItemUsingState => _usingState;

    private readonly PlayerHandsContainer _playerHandsContainer;
    private readonly InputHandler _inputHandler;
    private readonly PlayerView _playerView;

    private UsingState _usingState = UsingState.None;
    private Dictionary<ItemType, IActionWithItem> _actionsWithItems;

    // Пропускать начальную анимацию при повторном применении, скорее всего нужно вынести в параметры предмета или в UseResult
    private bool _skipStartDelay;

    private ItemType _itemInHandType;

    public PlayerHandsSystem(PlayerHandsContainer playerHandsSystem, InputHandler inputHandler, PlayerView playerView)
    {
        _playerHandsContainer = playerHandsSystem;
        _inputHandler = inputHandler;
        _playerView = playerView;
        _actionsWithItems = new()
        {
            [ItemType.Consumable] = new UseOnSelfAction(),
            [ItemType.Gun] = new ShootAction(playerView.PlayerCamera),
            [ItemType.Grenade] = new ThrowItemAction(playerView.PlayerCamera),
            [ItemType.GroundItem] = new DropItemAction(playerView.PlayerCamera),
        };
    }

    [Obsolete("Используется для теста разных действий. Тип предмета должен быть определен в предмете")]
    private void TestSelectItemType()
    {
        if (Input.GetKey(KeyCode.Alpha1)) _itemInHandType = ItemType.Consumable;
        if (Input.GetKey(KeyCode.Alpha2)) _itemInHandType = ItemType.Grenade;
        if (Input.GetKey(KeyCode.Alpha3)) _itemInHandType = ItemType.Gun;
        if (Input.GetKey(KeyCode.Alpha4)) _itemInHandType = ItemType.GroundItem;
    }

    public void Tick()
    {
        TestSelectItemType();
        if (_inputHandler.UseTrigger.IsTriggered && CanUseItemInHand())
        {
            UseItemInHand().Forget();
        }
        if (!_inputHandler.UseTrigger.IsPressed)
        {
            _skipStartDelay = false;
        }
    }

    private bool CanUseItemInHand()
    {
        Debug.Log(_playerHandsContainer.State == PlayerHandsContainer.HandState.EmptyHands ? "Нет предмета в руке"
         : _usingState != UsingState.None ? "Нельзя использовать предмет - " + _usingState
         : "Можно использовать предмет");
        return _playerHandsContainer.State == PlayerHandsContainer.HandState.ItemInHand && _usingState == UsingState.None;
    }

    private async UniTask<ItemUseResult> UseItemInHand()
    {
        if (!_skipStartDelay)
        {
            // Если предмет - расходник, запускаем анимацию
            if (_itemInHandType == ItemType.Consumable)
            {
                _playerView.Animator.SetTrigger("Heal");
            }

            OnItemStartUsing?.Invoke(_itemInHandType);
            _usingState = UsingState.Start;
            await UniTask.Delay(TimeSpan.FromSeconds(_playerHandsContainer.ItemUseStartDelay));
        }
        _usingState = UsingState.Using;

        var useResult = _actionsWithItems[_itemInHandType].Execute(_playerHandsContainer);

        if (useResult.UsageType == UsageType.ClickToUse)
        {
            _skipStartDelay = false;
            _inputHandler.UseTrigger.ReleaseTrigger();
            _usingState = UsingState.OnDelay;
            await UniTask.Delay(TimeSpan.FromSeconds(_playerHandsContainer.ItemUseDelay));
            _usingState = UsingState.None;
        }
        else if (useResult.UsageType == UsageType.HoldToUse)
        {
            _skipStartDelay = true;
            _usingState = UsingState.None;
        }
        return useResult;
    }
}

// TODO перенести в предметы
public enum ItemType
{
    /// <summary>
    /// Расходник
    /// </summary>
    Consumable,
    /// <summary>
    /// Пистолет
    /// </summary>
    Gun,
    /// <summary>
    /// Граната
    /// </summary>
    Grenade,
    /// <summary>
    /// Выбрасываемый на землю предмет
    /// </summary>
    GroundItem,
    /// <summary>
    /// Инструмент
    /// </summary>
    Tool
}

/// <summary>
/// Действие с предметом
/// </summary>
public interface IActionWithItem
{
    /// <summary>
    /// Выполнить действие с предметом в руках
    /// </summary>
    /// <param name="playerHandsContainer"></param>
    /// <returns></returns>
    ItemUseResult Execute(PlayerHandsContainer playerHandsContainer);
}
