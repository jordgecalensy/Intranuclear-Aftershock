using UnityEngine;
using UnityEngine.EventSystems;

public class DeleteProfileDropzone : MonoBehaviour, IDropHandler
{
    ProfileMenu _profileMenu;

    void Start()
    {
        _profileMenu = GetComponentInParent<ProfileMenu>();
        _profileMenu.OnProfileStartDrag.AddListener(VisualizeStartDragging);
        _profileMenu.OnProfileEndDrag.AddListener(VisualizeEndDragging);
    }
    public void OnDrop(PointerEventData eventData)
    {
        DraggableUI draggable = eventData.pointerDrag.GetComponent<DraggableUI>();
        if (draggable != null)
        {
            _profileMenu.ConfirmDeleteProfile(draggable.GetProfileToDelete());
        }
    }

    public void VisualizeStartDragging()
    {
        transform.localScale = new Vector3(1.3f, 1.3f, 1);
    }

    public void VisualizeEndDragging()
    {
        transform.localScale = new Vector3(1, 1, 1);
    }
}
