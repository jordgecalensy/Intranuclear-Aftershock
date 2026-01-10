using UnityEngine.UI;
using VContainer;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private Camera _playerCam;
    [SerializeField] private float _distance;
    [SerializeField] private LayerMask _mask;

    [Inject]
    private InputHandler _inputHandler;
    
    private ScrollbarInteractable _activeScrollbar; // <-- запоминаем текущий скроллбар

    void Update()
    {

        Ray ray = new Ray(_playerCam.transform.position, _playerCam.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * _distance);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, _distance, _mask))
        {
            var interactable = hitInfo.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                targetScale = _hoverSize;

                if (_inputHandler.GrabOrDropAction.WasPressedThisFrame()) //использовал триггер GrapOrDrop так как не смг создать свой
                {
                    interactable.BaseInteract();
                    _activeScrollbar = interactable as ScrollbarInteractable;
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
        }
    }    
}
