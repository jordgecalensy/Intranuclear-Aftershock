using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GridPixel : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler
{
    [SerializeField] private Image img;
    [SerializeField] private Color onColor = new Color(1f, 0.4f, 0f, 1f);
    [SerializeField] private Color offColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color errorTint = new Color(1f, 0.2f, 0.2f, 0.6f);

    [SerializeField] private bool locked = false;
    public bool IsOn { get; private set; } = false;

    private static bool _painting;       // идёт рисование
    private static bool _paintState;     // состояние, в которое тянем
    public static bool IsPainting => _painting;

    private static readonly List<RaycastResult> _hits = new List<RaycastResult>(16);
    private static Vector2 _lastMousePos;
    private const float SampleStep = 6f; // px шаг дискретизации по траектории. Можешь уменьшить до 4, если клетки мелкие

    private void Reset()
    {
        if (!img) img = GetComponent<Image>();
        ApplyVisual();
    }

    private void Update()
    {
        if (_painting)
        {
            if (!Input.GetMouseButton(0)) { 
                _painting = false; 
                return; 
            }
            PaintAlongMousePath();
            _lastMousePos = Input.mousePosition;
        }
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        _painting = true;
        _paintState = !IsOn;
        _lastMousePos = e.position;

        SetOn(_paintState);
        PaintAtScreenPoint(e.position);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_painting) {
            SetOn(_paintState);
        }
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        // Не сбрасываем _painting, если мышь всё ещё зажата (защита от ложных PointerUp)
        if (!_painting) return;
        if (Input.GetMouseButton(0)) {
            return;
        }
        _painting = false;
    }

    private void OnDisable() => _painting = false;

    public void SetLocked(bool v)
    {
        locked = v;
        ApplyVisual();
    }

    public void SetOn(bool v)
    {
        if (locked) return;
        if (IsOn == v) return;
        IsOn = v;
        ApplyVisual();
        GetComponentInParent<SymbolGrid>()?.NotifyChanged();
    }

    public void Toggle() => SetOn(!IsOn);

    public void ClearError()
    {
        if (img) img.color = IsOn ? onColor : offColor;
    }

    public void SetError(bool on)
    {
        if (!img) return;
        img.color = on ? Mix(img.color, errorTint) : (IsOn ? onColor : offColor);
    }

    private void ApplyVisual()
    {
        if (img) img.color = locked
            ? Mix(offColor, new Color(0.6f, 0.6f, 0.6f, 0.6f))
            : (IsOn ? onColor : offColor);
    }

    private static Color Mix(Color a, Color b)
    {
        float t = b.a;
        return new Color(a.r + (b.r - a.r) * t,
                         a.g + (b.g - a.g) * t,
                         a.b + (b.b - a.b) * t, 1f);
    }

    // --- КЛЮЧ: рисуем по всей траектории между lastMousePos и текущей позицией ---
    private static void PaintAlongMousePath()
    {
        var cur = (Vector2)Input.mousePosition;
        var delta = cur - _lastMousePos;

        float dist = delta.magnitude;

        // если мышь почти не двигалась — просто красим в текущей точке
        if (dist < 0.5f) { 
            PaintAtScreenPoint(cur); 
            return; 
        }

        int steps = Mathf.CeilToInt(dist / SampleStep);
        float inv = 1f / steps;

        for (int i = 1; i <= steps; i++)
        {
            var p = Vector2.Lerp(_lastMousePos, cur, i * inv);
            PaintAtScreenPoint(p);
        }
    }

    // Рейкаст в UI и закраска верхнего GridPixel (ищем в родителях на случай попадания по дочерним объектам)
    private static void PaintAtScreenPoint(Vector2 screenPos)
    {
        if (EventSystem.current == null) {
            return;
        }

        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        _hits.Clear();
        EventSystem.current.RaycastAll(ped, _hits);


        for (int i = 0; i < _hits.Count; i++)
        {
            var go = _hits[i].gameObject;
            var px = go.GetComponentInParent<GridPixel>();
            if (px != null)
            {
                px.SetOn(_paintState);
                break;
            }
        }
    }
}