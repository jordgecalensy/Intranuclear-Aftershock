using UnityEngine.UI;
using VContainer;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction: MonoBehaviour
{
    [SerializeField] private Camera _playerCam;
    [SerializeField] private float _distance;
    [SerializeField] private LayerMask _mask;

    [Inject]
    private InputHandler _inputHandler;
    


    
    void Update()
    {

        Ray ray = new Ray(_playerCam.transform.position, _playerCam.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * _distance);
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo, _distance, _mask))
        {
            if(hitInfo.collider.GetComponent<Interactable>() != null)
            {
                Interactable interactable = hitInfo.collider.GetComponent<Interactable>();
                if (_inputHandler.GrabOrDropAction.WasPressedThisFrame()) //использовал триггер GrapOrDrop так как не смг создать свой
                {
                    interactable.BaseInteract();
                }
            }
        }
    }
    
}
