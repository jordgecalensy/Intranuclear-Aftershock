using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class GridPainter : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public static bool IsPainting { get; private set; }

    [Tooltip("Контейнер с дочерними пикселями (GridPixel). По умолчанию — этот объект.")]
    [SerializeField] private Transform pixelsParent;

    [Tooltip("Доля размера клетки для шага семплирования (0.3–0.5).")]
    [Range(0.1f, 0.8f)] public float stepFactor = 0.35f;

    private RectTransform _rt;
    private Canvas _canvas;
    private Vector2 _lastScreen;
    private bool _paintState = true;
    private float _sampleStepPx = 6f;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        if (!pixelsParent) pixelsParent = transform;

        // подберём шаг из размера первой клетки
        if (pixelsParent.childCount > 0)
        {
            var c = pixelsParent.GetChild(0) as RectTransform;
            if (c)
            {
                Vector3[] w = new Vector3[4];
                c.GetWorldCorners(w);
                Vector2 a = WorldToScreen(w[0]);
                Vector2 b = WorldToScreen(w[2]);
                float minSide = Mathf.Max(2f, Mathf.Min(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)));
                _sampleStepPx = Mathf.Clamp(minSide * stepFactor, 2f, 20f);
            }
        }
    }

    public void OnPointerDown(PointerEventData e)
    {
        IsPainting = true;
        _lastScreen = e.position;

        var px = HitPixelByLocal(ScreenToLocal(e.position));
        _paintState = px ? !px.IsOn : true;

        PaintAtScreen(e.position);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!IsPainting) return;
        PaintLine(_lastScreen, e.position);
        _lastScreen = e.position;
    }

    public void OnPointerUp(PointerEventData e) => IsPainting = false;

    // ---- helpers ----
    private Vector2 ScreenToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rt, screen, _canvas ? _canvas.worldCamera : null, out var local);
        return local;
    }

    private Vector2 WorldToScreen(Vector3 world)
    {
        if (_canvas && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, world);
        return (Vector2)world; // overlay: координаты уже эквивалентны
    }

    private void PaintLine(Vector2 a, Vector2 b)
    {
        float dist = (b - a).magnitude;
        if (dist < 0.5f) { PaintAtScreen(b); return; }
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / _sampleStepPx));
        float inv = 1f / steps;
        for (int i = 1; i <= steps; i++)
            PaintAtScreen(Vector2.Lerp(a, b, i * inv));
    }

    private void PaintAtScreen(Vector2 screen)
    {
        var px = HitPixelByLocal(ScreenToLocal(screen));
        if (px) px.SetOn(_paintState);
    }

    private GridPixel HitPixelByLocal(Vector2 local)
    {
        for (int i = 0; i < pixelsParent.childCount; i++)
        {
            var child = pixelsParent.GetChild(i) as RectTransform;
            if (!child) continue;

            Vector3[] c = new Vector3[4];
            child.GetWorldCorners(c);
            for (int k = 0; k < 4; k++) c[k] = _rt.InverseTransformPoint(c[k]);

            float minX = Mathf.Min(c[0].x, c[2].x);
            float maxX = Mathf.Max(c[0].x, c[2].x);
            float minY = Mathf.Min(c[0].y, c[2].y);
            float maxY = Mathf.Max(c[0].y, c[2].y);

            if (local.x >= minX && local.x <= maxX && local.y >= minY && local.y <= maxY)
                return child.GetComponent<GridPixel>();
        }
        return null;
    }
}