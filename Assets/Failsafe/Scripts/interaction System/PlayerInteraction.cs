using UnityEngine.UI;
using VContainer;
using UnityEngine;
using UnityEngine.EventSystems;
using Assets.Failsafe.Scripts.interaction_System;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private Camera _playerCam;
    [SerializeField] private float _distance;
    [SerializeField] private LayerMask _mask;

    [Inject]
    private InputHandler _inputHandler;
    [Inject]
    private PlayerHandsContainer _handsContainer;

    private ItemPlaceArea _itemArea;
    private ScrollbarInteractable _activeScrollbar; // <-- запоминаем текущий скроллбар
    private Interactable lastHoveredObject = null;

    void Update()
    {

        Ray ray = new Ray(_playerCam.transform.position, _playerCam.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * _distance);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, _distance, _mask))
        {
            Interactable interactable = hitInfo.collider.GetComponent<Interactable>();
            if (interactable != null)
            {

                if (interactable != lastHoveredObject)
                {
                    if (lastHoveredObject != null)
                        lastHoveredObject.OnHoverExit();

                    interactable.OnHover();

                    lastHoveredObject = interactable;
                }

                if (_inputHandler.GrabOrDropAction.WasPressedThisFrame()) //использовал триггер GrapOrDrop так как не смг создать свой
                {

                    interactable.BaseInteract();
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
                                {
                                    _itemArea.PutItemInside(_handsContainer.PlaceItem(itemPlace));
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning("Take item here");
                            _handsContainer.TryTakeItemInHand(_itemArea.TakeItem());
                        }
                    }
                    else if (interactable is CarryObjectPlaceArea carryObjectPlaceArea)
                    {
                        if (carryObjectPlaceArea.IsEmpty)
                        {
                            if (_handsContainer.State == PlayerHandsContainer.HandState.ItemInHand)
                            {
                                Transform itemPlace = carryObjectPlaceArea.TryGetItemPlace(_handsContainer.ItemInHand.ItemObject);
                                if (itemPlace != null)
                                {
                                    carryObjectPlaceArea.PutItemInside(_handsContainer.PlaceItem(itemPlace));
                                }
                            }
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
            }
        }
        else
        {
            if (_activeScrollbar != null && _inputHandler.GrabOrDropAction.WasReleasedThisFrame())
            {
                _activeScrollbar.StopDrag();
                _activeScrollbar = null;
            }
            if (lastHoveredObject != null)
            {
                lastHoveredObject.OnHoverExit();
                lastHoveredObject = null;
            }
        }
    }    
}
