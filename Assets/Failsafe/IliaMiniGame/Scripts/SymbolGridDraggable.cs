using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class SymbolGridDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Ссылки")]
    public Transform container;
    public CodeOrderJudge judge;

    [Header("Ось и направление")]
    public bool horizontal = true;
    public bool reverseFlow = false;

    [Header("Визуал (опц.)")]
    public CanvasGroup cg;
    public float draggingAlpha = 0.8f;

    private RectTransform _rt;
    private LayoutElement _le;
    private bool _dragging;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (!container) container = transform.parent;
        if (!cg) cg = GetComponent<CanvasGroup>();
        _le = GetComponent<LayoutElement>();
        if (!_le) _le = gameObject.AddComponent<LayoutElement>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Блокируем старт перетаскивания во время рисования
        if (GridPixel.IsPainting) { _dragging = false; return; }

        if (!container) return;
        _dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Если в процессе начали рисовать — перестаём двигать
        if (!_dragging || container == null || GridPixel.IsPainting) return;

        var contRT = container as RectTransform;
        var cam = eventData.pressEventCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(contRT, eventData.position, cam, out var p);
        int target = FindNearestIndexByCenters(contRT, p);

        int cur = transform.GetSiblingIndex();
        if (target != cur)
        {
            transform.SetSiblingIndex(target);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contRT);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Ничего не делаем, если d&d был заблокирован рисованием
        if (!_dragging || GridPixel.IsPainting) return;
        _dragging = false;

        GetComponent<SymbolGrid>()?.NotifyChanged();
        if (judge) judge.Check();
    }

    private int FindNearestIndexByCenters(RectTransform contRT, Vector2 localPos)
    {
        int childCount = container.childCount;
        if (childCount <= 1) return 0;

        int selfIndex = transform.GetSiblingIndex();
        int firstAfter = -1;

        for (int i = 0; i < childCount; i++)
        {
            var child = container.GetChild(i) as RectTransform;
            if (!child || child == _rt) continue;

            Vector3[] c = new Vector3[4];
            child.GetWorldCorners(c);
            for (int k = 0; k < 4; k++) c[k] = contRT.InverseTransformPoint(c[k]);

            float center = horizontal
                ? ((Mathf.Min(c[0].x, c[1].x) + Mathf.Max(c[2].x, c[3].x)) * 0.5f)
                : ((Mathf.Min(c[0].y, c[3].y) + Mathf.Max(c[1].y, c[2].y)) * 0.5f);

            float coord = horizontal ? localPos.x : localPos.y;

            bool passed;
            if (horizontal)
                passed = reverseFlow ? (coord <= center) : (coord < center);
            else
                passed = reverseFlow ? (coord <= center) : (coord > center);

            if (passed) { firstAfter = i; break; }
        }

        if (firstAfter == -1) firstAfter = childCount;

        int target = firstAfter;
        if (firstAfter > selfIndex) target--;
        return Mathf.Clamp(target, 0, childCount - 1);
    }
}