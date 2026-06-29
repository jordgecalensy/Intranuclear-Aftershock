using UnityEngine.UI;
using VContainer;
using UnityEngine;
using Assets.Failsafe.Scripts.interaction_System;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private Camera _playerCam;
    [SerializeField] private float _distance;
    [SerializeField] private LayerMask _mask;

    [Inject] private InputHandler _inputHandler;
    [Inject] private PlayerHandsContainer _handsContainer;

    private ItemPlaceArea _itemArea;
    private ScrollbarInteractable _activeScrollbar;
    private Interactable _lastHoveredObject = null;

    private void Update()
    {
        Ray ray = new Ray(_playerCam.transform.position, _playerCam.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * _distance);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, _distance, _mask))
        {
            Interactable interactable = hitInfo.collider.GetComponentInParent<Interactable>();

            if (interactable != null)
            {
                HandleHover(interactable);

                if (_inputHandler.GrabOrDropAction.WasPressedThisFrame())
                {
                    var context = new PlayerInteractionContext(
                        this,
                        _handsContainer,
                        _playerCam,
                        hitInfo);

                    interactable.BaseInteract(context);

                    _activeScrollbar = interactable as ScrollbarInteractable;

                    if (interactable is ItemPlaceArea)
                    {
                        _itemArea = interactable as ItemPlaceArea;

                        if (_itemArea.IsEmpty)
                        {
                            if (_handsContainer.State == PlayerHandsContainer.HandState.ItemInHand)
                            {
                                Transform itemPlace = _itemArea.TryGetItemPlace(_handsContainer.ItemInHand.ItemObject);

                                if (itemPlace != null)
                                    _itemArea.PutItemInside(_handsContainer.PlaceItem(itemPlace));
                            }
                        }
                        else
                        {
                            Debug.LogWarning("Take item here");
                            _handsContainer.TryTakeItemInHand(_itemArea.TakeItem());
                        }
                    }
                }

                if (_activeScrollbar != null && _inputHandler.GrabOrDropAction.IsPressed())
                    _activeScrollbar.DragTo(hitInfo);

                if (_activeScrollbar != null && _inputHandler.GrabOrDropAction.WasReleasedThisFrame())
                {
                    _activeScrollbar.StopDrag();
                    _activeScrollbar = null;
                }

                return;
            }
        }

        HandleNoHit();
    }

    private void HandleHover(Interactable interactable)
    {
        if (interactable == _lastHoveredObject)
            return;

        if (_lastHoveredObject != null)
            _lastHoveredObject.OnHoverExit();

        interactable.OnHover();

        _lastHoveredObject = interactable;
    }

    private void HandleNoHit()
    {
        if (_activeScrollbar != null && _inputHandler.GrabOrDropAction.WasReleasedThisFrame())
        {
            _activeScrollbar.StopDrag();
            _activeScrollbar = null;
        }

        if (_lastHoveredObject != null)
        {
            _lastHoveredObject.OnHoverExit();
            _lastHoveredObject = null;
        }
    }
}