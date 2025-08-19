using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class DraggableUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] RectTransform _rectTransform;
    Canvas _canvas;
    CanvasGroup _canvasGroup;
    ProfileMenu _profileMenu;
   
    
    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _profileMenu = GetComponentInParent<ProfileMenu>();
        
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        _profileMenu.OnProfileStartDrag?.Invoke();
        //нужно менять профилю родителя чтобы он мог показываться вне пределов окна профилей, ибо кнопка корзины как раз за пределами
        _rectTransform.SetParent(_canvas.transform);
        _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        _rectTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        _profileMenu.OnProfileEndDrag?.Invoke();
        _profileMenu.ReturnProfileToContainer(_rectTransform);
        _rectTransform.anchoredPosition = Vector2.zero;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
    }

	public Profile GetProfileToDelete()
    {
        return _rectTransform.GetComponent<Profile>();
    }
}
