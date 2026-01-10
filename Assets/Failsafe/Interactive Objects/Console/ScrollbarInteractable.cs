using UnityEngine;
using UnityEngine.UI;

public class ScrollbarInteractable : Interactable
{
    [Header("Refs")]
    [SerializeField] private Scrollbar _scrollbar;
    [SerializeField] private RectTransform _barRect;   // область, по которой "тащим" (Sliding Area или сам Scrollbar)
    [SerializeField] private Camera _rayCamera;        // камера игрока

    [Header("Settings")]
    [SerializeField] private bool _vertical = true;    // вертикальный/горизонтальный

    private bool _dragging;

    private void Reset()
    {
        _scrollbar = GetComponentInParent<Scrollbar>();
        _barRect = GetComponent<RectTransform>();
    }

    protected override void Interact()
    {
        _dragging = true;
    }

    public void StopDrag() => _dragging = false;

    public void DragTo(RaycastHit hit)
    {
        if (!_dragging || _scrollbar == null || _barRect == null || _rayCamera == null)
            return;

        // world hit -> screen -> local point inside bar rect
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_rayCamera, hit.point);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_barRect, screenPoint, _rayCamera, out var local))
            return;

        float v;
        if (_vertical)
            v = Mathf.InverseLerp(_barRect.rect.yMin, _barRect.rect.yMax, local.y);
        else
            v = Mathf.InverseLerp(_barRect.rect.xMin, _barRect.rect.xMax, local.x);

        // учитываем Direction скроллбара (чтобы не оказалось "инвертировано")
        v = ApplyDirection(v);

        _scrollbar.value = Mathf.Clamp01(v);
    }

    private float ApplyDirection(float v)
    {
        if (_scrollbar == null) return v;

        // Vertical
        if (_scrollbar.direction == Scrollbar.Direction.BottomToTop) return v;
        if (_scrollbar.direction == Scrollbar.Direction.TopToBottom) return 1f - v;

        // Horizontal
        if (_scrollbar.direction == Scrollbar.Direction.LeftToRight) return v;
        if (_scrollbar.direction == Scrollbar.Direction.RightToLeft) return 1f - v;

        return v;
    }
}
