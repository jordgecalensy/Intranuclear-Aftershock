using Cysharp.Threading.Tasks;
using Failsafe.Inventory.Integration;
using Failsafe.Items;
using Failsafe.Player.Model;
using Failsafe.Player.View;
using Failsafe.PlayerMovements;
using System;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Использование предметов в руках.
/// </summary>
public class PlayerHandsSystem : ITickable
{
    public enum UsingState { None, Start, Using, OnDelay }

    public event Action<ItemType> OnItemStartUsing;

    public UsingState ItemUsingState => _usingState;

    private readonly PlayerHandsContainer _playerHandsContainer;
    private readonly InputHandler _inputHandler;
    private readonly PlayerControlBlocker _controlBlocker;
    private readonly PlayerModelParameters _playerModelParameters;
    private readonly Transform _itemThrowOrigin;
    private readonly IInventoryHeldItemLifecycle _inventoryItemLifecycle;

    private UsingState _usingState = UsingState.None;

    /// <summary>
    /// Пропускать начальную анимацию при повторном применении.
    /// </summary>
    private bool _skipStartDelay;

    public PlayerHandsSystem(
        PlayerHandsContainer playerHandsContainer,
        InputHandler inputHandler,
        PlayerControlBlocker controlBlocker,
        PlayerModelParameters playerModelParameters,
        PlayerView playerView,
        IInventoryHeldItemLifecycle inventoryItemLifecycle)
    {
        _playerHandsContainer = playerHandsContainer;
        _inputHandler = inputHandler;
        _controlBlocker = controlBlocker;
        _playerModelParameters = playerModelParameters;
        _itemThrowOrigin = playerView != null
            ? playerView.PlayerCamera
            : null;
        _inventoryItemLifecycle = inventoryItemLifecycle ??
            throw new ArgumentNullException(
                nameof(inventoryItemLifecycle));
    }

    public void Tick()
    {
        if (_inputHandler.AttackTrigger.IsTriggered && CanUseItemInHand())
            UseItemInHand().Forget();

        if (_inputHandler.AltModeTrigger.IsTriggered)
        {
            _inputHandler.AltModeTrigger.ReleaseTrigger();

            if (CanAltUseItemInHand())
                _playerHandsContainer.ItemInHand.ItemUsable.AltMode();
        }

        if (!_inputHandler.AttackTrigger.IsPressed)
            _skipStartDelay = false;
    }

    private bool CanUseItemInHand()
    {
        if (_playerHandsContainer.State == PlayerHandsContainer.HandState.EmptyHands)
            return false;

        if (_usingState != UsingState.None)
            return false;

        if (_controlBlocker != null)
        {
            if (_controlBlocker.IsBlocked(PlayerControlBlock.ItemUse))
                return false;

            if (_controlBlocker.IsBlocked(PlayerControlBlock.Shooting))
                return false;
        }

        return true;
    }

    private bool CanAltUseItemInHand()
    {
        if (_playerHandsContainer.State == PlayerHandsContainer.HandState.EmptyHands)
            return false;

        if (_controlBlocker != null)
        {
            if (_controlBlocker.IsBlocked(PlayerControlBlock.ItemUse))
                return false;

            if (_controlBlocker.IsBlocked(PlayerControlBlock.Shooting))
                return false;
        }

        return true;
    }

    private async UniTask<ItemUseResult> UseItemInHand()
    {
        ItemInHand itemInHand = _playerHandsContainer.ItemInHand;

        if (itemInHand == null || itemInHand.ItemObject == null || itemInHand.ItemUsable == null)
        {
            _usingState = UsingState.None;
            return new ItemUseResult
            {
                UsageType = UsageType.ClickToUse,
                ItemStateAfterUse = ItemState.Hold
            };
        }

        if (!_skipStartDelay)
        {
            OnItemStartUsing?.Invoke(itemInHand.ItemObject.ItemData.Type);

            _usingState = UsingState.Start;

            await UniTask.Delay(TimeSpan.FromSeconds(_playerHandsContainer.ItemUseStartDelay));
        }

        _usingState = UsingState.Using;

        ItemUseResult useResult = itemInHand.ItemUsable.Use();

        HandleItemStateAfterUse(useResult);

        if (useResult.UsageType == UsageType.ClickToUse)
        {
            _skipStartDelay = false;

            _inputHandler.AttackTrigger.ReleaseTrigger();

            _usingState = UsingState.OnDelay;

            await UniTask.Delay(TimeSpan.FromSeconds(_playerHandsContainer.ItemUseDelay));

            _usingState = UsingState.None;
        }
        else if (useResult.UsageType == UsageType.HoldToUse)
        {
            _skipStartDelay = true;

            _usingState = UsingState.OnDelay;

            float useDelay = Mathf.Max(0.02f, _playerHandsContainer.ItemUseDelay);
            await UniTask.Delay(TimeSpan.FromSeconds(useDelay));

            _usingState = UsingState.None;
        }

        return useResult;
    }

    private void HandleItemStateAfterUse(ItemUseResult useResult)
    {
        switch (useResult.ItemStateAfterUse)
        {
            case ItemState.Hold:
                return;

            case ItemState.Drop:
                PrepareReleaseToWorld(
                    _playerHandsContainer.ItemInHand?.ItemObject);
                _playerHandsContainer.DropItemFromHand();
                return;

            case ItemState.Throw:
                PrepareReleaseToWorld(
                    _playerHandsContainer.ItemInHand?.ItemObject);

                float throwForce =
                    _playerModelParameters?.ThrowItemPower != null
                        ? _playerModelParameters.ThrowItemPower.Value
                        : 0f;

                _playerHandsContainer.ThrowItemFromHand(
                    _itemThrowOrigin,
                    throwForce);
                return;

            case ItemState.Consume:
                Item item =
                    _playerHandsContainer.ConsumeItemFromHand();

                if (item != null &&
                    !_inventoryItemLifecycle.TryConsume(
                        item,
                        out string consumeError))
                {
                    Debug.LogError(
                        $"Consumed item inventory cleanup failed: " +
                        consumeError,
                        item);
                }

                return;
        }
    }

    private void PrepareReleaseToWorld(Item item)
    {
        if (item == null)
            return;

        if (_inventoryItemLifecycle.TryReleaseToWorld(
                item,
                out string error))
        {
            return;
        }

        Debug.LogError(
            $"Held item inventory cleanup failed before release: " +
            error,
            item);
    }
}

/// <summary>
/// Действие с предметом.
/// Legacy. Оставлено, чтобы старые action-классы не развалились.
    /// Новая логика должна идти через IUsable.Use().
/// </summary>
public interface IActionWithItem
{
    ItemUseResult Execute(PlayerHandsContainer playerHandsContainer);
}
