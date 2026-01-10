using System.Collections;
using System.Reflection;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Bridge для управления TMP_Dropdown через Physics.Raycast:
/// - открытие/закрытие (toggle) делается в TMPDropdownInteractable
/// - тут: навешиваем BoxCollider+Interactable на runtime items
/// - и "сдвигаем вниз" элементы в ScrollView/Content через Spacer (LayoutElement)
/// </summary>
public class TMPDropdownRaycastBridge : MonoBehaviour
{
    [Header("Auto scroll into view")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _viewport;
    [SerializeField] private float _padding = 8f;

    [Header("Dropdown")]
    [SerializeField] private TMP_Dropdown _dropdown;

    [Header("Raycast layer for runtime items")]
    [SerializeField] private LayerMask _uiLayer;

    [Header("Push-down via Spacer")]
    [Tooltip("Scroll View / Viewport / Content (там где VerticalLayoutGroup + ContentSizeFitter). Лучше назначить руками.")]
    [SerializeField] private RectTransform _contentRoot;

    [Tooltip("Имя создаваемой прокладки под dropdown.")]
    [SerializeField] private string _spacerName = "TMPDropdownSpacer";

    [Header("Spacer timing")]
    [SerializeField] private float _spacerHideDelay = 0.15f;

    [Header("Force open direction")]
    [SerializeField] private bool _forceOpenDown = true;
    [SerializeField] private float _downOffset = 0f; // обычно 0, если надо - небольшая подстройка


    private RectTransform _spacerRect;
    private LayoutElement _spacerLayout;

    private bool _isOpen;
    private Transform _runtimeList;

    // Чтобы не было двух открытых одновременно (иначе оверлейные листы будут конфликтовать)
    private static TMPDropdownRaycastBridge _opened;

    private Coroutine _hideSpacerRoutine;

    private void Reset()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
    }

    private void Awake()
    {
        // Это для снеп скрола
        if (_scrollRect == null) _scrollRect = GetComponentInParent<ScrollRect>();
        if (_viewport == null && _scrollRect != null) _viewport = _scrollRect.viewport;

        if (_dropdown == null) _dropdown = GetComponent<TMP_Dropdown>();

        // Лучше задавать руками, но если забыли — пробуем найти родителя с VerticalLayoutGroup - FROM GPT
        if (_contentRoot == null)
        {
            var vlg = GetComponentInParent<VerticalLayoutGroup>();
            if (vlg != null) _contentRoot = vlg.GetComponent<RectTransform>();
        }

        EnsureSpacer();
        SetSpacerHeight(0f);
    }

    private void OnDisable()
    {
        if (_opened == this) _opened = null;

        _isOpen = false;
        _runtimeList = null;

        HideSpacerWithDelay();
    }

    public bool IsOpen() => _isOpen;

    /// <summary>
    /// Вызывается ПОСЛЕ _dropdown.Show() из TMPDropdownInteractable.
    /// </summary>
    
    private float EstimateListViewportHeight()
    {
        if (_dropdown == null || _dropdown.template == null) return 0f;

        // В TMP_Dropdown template содержит Viewport, его высота = видимая часть выпадашки
        RectTransform viewport = null;

        var t = _dropdown.template.Find("Viewport");
        if (t != null) viewport = t as RectTransform;

        if (viewport == null)
        {
            var rectMasks = _dropdown.template.GetComponentsInChildren<RectMask2D>(true);
            if (rectMasks.Length > 0) viewport = rectMasks[0].transform as RectTransform;
        }

        if (viewport == null) return 0f;

        Canvas.ForceUpdateCanvases();
        float h = viewport.rect.height;
        return h > 0.01f ? h : 0f;
    }

    public void OnShowCalled()
    {
        if (_hideSpacerRoutine != null)
        {
            StopCoroutine(_hideSpacerRoutine);
            _hideSpacerRoutine = null;
        }
        // Один открыт за раз — так проще и надёжнее
        if (_opened != null && _opened != this)
            _opened.ForceClose();

        _opened = this;

        _isOpen = true;
        _runtimeList = null;

        EnsureSpacer();
        
        StartCoroutine(BuildAndPush());
    }

    /// <summary>
    /// Жёстко закрыть dropdown и убрать сдвиг.
    /// </summary>
    public void ForceClose()
    {
        if (_dropdown != null)
            _dropdown.Hide();

        _isOpen = false;
        _runtimeList = null;

        HideSpacerWithDelay();

        if (_opened == this) _opened = null;
    }

    private IEnumerator BuildAndPush()
    {
        // TMP создаёт overlay-список к концу кадра
        yield return null;
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();

        // 1) Берём runtime list именно этого TMP_Dropdown (самый надёжный способ)
        _runtimeList = TryGetRuntimeListFromTMP();

        // 2) Высота для "сдвига вниз"
        float listHeight = 0f;

        if (_runtimeList != null)
        {
            // (а) навесить интеракты/коллайдеры на items
            var toggles = _runtimeList.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                var go = toggles[i].gameObject;

                // чтобы Physics.Raycast ловил
                go.layer = MaskToLayer(_uiLayer);

                var col = go.GetComponent<BoxCollider>();
                if (col == null) col = go.AddComponent<BoxCollider>();

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    col.center = Vector3.zero;
                    col.size = new Vector3(rt.rect.width, rt.rect.height, 1f);
                }

                var interactable = go.GetComponent<TMPDropdownItemInteractable>();
                if (interactable == null) interactable = go.AddComponent<TMPDropdownItemInteractable>();
                interactable.Init(_dropdown, i, this);
            }

            // (б) берём высоту именно "видимой" части выпадашки (Viewport)
            listHeight = GetDropdownViewportHeight(_runtimeList);

            // fallback если вдруг 0
            if (listHeight <= 0.01f)
                listHeight = GetListHeightSafe(_runtimeList);
        }

        // 3) Сдвиг вниз через spacer
        SetSpacerHeight(listHeight);

        if (_forceOpenDown)
            ForceRuntimeListOpenDown();
    }

    /// <summary>
    /// Получить runtime list из приватного поля TMP_Dropdown.m_Dropdown (GameObject).
    /// Это гарантирует, что мы взяли список именно этого dropdown, даже когда их несколько.
    /// </summary>
    private Transform TryGetRuntimeListFromTMP()
    {
        if (_dropdown == null) return null;

        var f = typeof(TMP_Dropdown).GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) return null;

        var go = f.GetValue(_dropdown) as GameObject;
        return go != null ? go.transform : null;
    }

    /// <summary>
    /// Предпочтительная высота для push-down: высота Viewport внутри Dropdown List.
    /// </summary>
    private float GetDropdownViewportHeight(Transform listRoot)
    {
        if (listRoot == null) return 0f;

        RectTransform viewport = null;

        // Обычно так:
        // Dropdown List
        //  └─ Viewport (RectMask2D/Mask)
        //      └─ Content
        var viewportT = listRoot.Find("Viewport");
        if (viewportT != null) viewport = viewportT as RectTransform;

        // Fallback: ищем объект с Mask/RectMask2D
        if (viewport == null)
        {
            var rectMasks = listRoot.GetComponentsInChildren<RectMask2D>(true);
            if (rectMasks.Length > 0) viewport = rectMasks[0].transform as RectTransform;
        }
        if (viewport == null)
        {
            var masks = listRoot.GetComponentsInChildren<Mask>(true);
            if (masks.Length > 0) viewport = masks[0].transform as RectTransform;
        }

        if (viewport == null) return 0f;

        Canvas.ForceUpdateCanvases();
        float h = viewport.rect.height;
        return h > 0.01f ? h : 0f;
    }

    /// <summary>
    /// Fallback если Viewport не нашли/0: высота корня списка.
    /// </summary>
    private float GetListHeightSafe(Transform list)
    {
        var rt = list as RectTransform;
        if (rt == null) rt = list.GetComponent<RectTransform>();
        if (rt == null) return 0f;

        Canvas.ForceUpdateCanvases();

        float preferred = LayoutUtility.GetPreferredHeight(rt);
        if (preferred > 0.01f) return preferred;

        float h = rt.rect.height;
        return h > 0.01f ? h : 0f;
    }

    /// <summary>
    /// Создаёт spacer сразу после dropdown в Content (если ещё нет).
    /// </summary>
    private void EnsureSpacer()
    {
        if (_contentRoot == null) return;

        int myIndex = transform.GetSiblingIndex();

        // если уже есть spacer сразу после нас — используем его
        if (myIndex + 1 < _contentRoot.childCount)
        {
            var next = _contentRoot.GetChild(myIndex + 1);
            if (next != null && next.name == _spacerName)
            {
                _spacerRect = next as RectTransform;
                _spacerLayout = next.GetComponent<LayoutElement>();
                if (_spacerLayout == null) _spacerLayout = next.gameObject.AddComponent<LayoutElement>();
                return;
            }
        }

        // иначе создаём новый
        var spacerGO = new GameObject(_spacerName, typeof(RectTransform), typeof(LayoutElement));
        spacerGO.layer = gameObject.layer;

        _spacerRect = spacerGO.GetComponent<RectTransform>();
        _spacerLayout = spacerGO.GetComponent<LayoutElement>();

        _spacerRect.SetParent(_contentRoot, false);
        _spacerRect.SetSiblingIndex(myIndex + 1);

        // Важно: растянуть по ширине Content, чтобы не было "уезда" и странного центрирования
        _spacerRect.anchorMin = new Vector2(0f, 1f);
        _spacerRect.anchorMax = new Vector2(1f, 1f);
        _spacerRect.pivot = new Vector2(0.5f, 1f);
        _spacerRect.offsetMin = Vector2.zero;
        _spacerRect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Ставит высоту spacer. Делаем и через LayoutElement, и через RectTransform — так надёжнее.
    /// </summary>
    private void SetSpacerHeight(float h)
    {
        float height = Mathf.Max(0f, h);

        if (_spacerLayout != null)
        {
            _spacerLayout.minHeight = height;          // важно: min = height
            _spacerLayout.preferredHeight = height;
            _spacerLayout.flexibleHeight = 0f;
        }

        if (_spacerRect != null)
        {
            _spacerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        RebuildContent();
    }
    private void HideSpacerWithDelay()
    {
        if (_hideSpacerRoutine != null)
            StopCoroutine(_hideSpacerRoutine);

        _hideSpacerRoutine = StartCoroutine(HideSpacerAfterDelay());
    }

    private IEnumerator HideSpacerAfterDelay()
    {
        if (_spacerHideDelay > 0f)
            yield return new WaitForSeconds(_spacerHideDelay);

        SetSpacerHeight(0f);
        _hideSpacerRoutine = null;
    }

    private void RebuildContent()
    {
        if (_contentRoot == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
        Canvas.ForceUpdateCanvases();
    }

    private int MaskToLayer(LayerMask mask)
    {
        int v = mask.value;
        for (int i = 0; i < 32; i++)
            if ((v & (1 << i)) != 0)
                return i;
        return 0;
    }

    private void ForceRuntimeListOpenDown()
    {
        if (_runtimeList == null) return;

        var listRT = _runtimeList as RectTransform;
        if (listRT == null) listRT = _runtimeList.GetComponent<RectTransform>();
        if (listRT == null) return;

        var headerRT = transform as RectTransform;
        if (headerRT == null) return;

        // list создаётся в отдельном Canvas (root canvas), поэтому работаем в world-space
        Canvas.ForceUpdateCanvases();

        // world corners header
        Vector3[] headerWorld = new Vector3[4];
        headerRT.GetWorldCorners(headerWorld);

        // header bottom edge
        Vector3 headerBottomLeft = headerWorld[0]; // bottom-left
        Vector3 headerBottomRight = headerWorld[3]; // bottom-right

        // текущие corners list
        Vector3[] listWorld = new Vector3[4];
        listRT.GetWorldCorners(listWorld);

        // list top edge сейчас:
        Vector3 listTopLeft = listWorld[1]; // top-left

        // хотим: listTopLeft.y == headerBottomLeft.y (+offset вниз)
        float targetTopY = headerBottomLeft.y + _downOffset;
        float deltaY = targetTopY - listTopLeft.y;

        // смещаем список в мире по Y
        listRT.position += new Vector3(0f, deltaY, 0f);

        // по X можно тоже выровнять к левому краю header (обычно TMP и так совпадает)
        // float deltaX = headerBottomLeft.x - listTopLeft.x;
        // listRT.position += new Vector3(deltaX, 0f, 0f);

        Canvas.ForceUpdateCanvases();
    }
    public void PreOpenScroll()
    {
        if (_scrollRect == null || _viewport == null) return;

        var headerRT = transform as RectTransform;
        if (headerRT == null) return;

        // Прогнозируем высоту будущего списка (без runtime Dropdown List)
        float listHeight = EstimateListViewportHeight();
        if (listHeight <= 0.01f) return;

        Canvas.ForceUpdateCanvases();

        Vector3[] view = new Vector3[4];
        Vector3[] header = new Vector3[4];
        _viewport.GetWorldCorners(view);
        headerRT.GetWorldCorners(header);

        float viewTop = view[1].y;
        float viewBottom = view[0].y;

        float headerTop = header[1].y;
        float headerBottom = header[0].y;

        // Мы хотим уместить блок: [bottom .. top], где top = headerTop, bottom = headerBottom - listHeight
        float blockTop = headerTop;
        float blockBottom = headerBottom - listHeight;

        float contentH = _scrollRect.content.rect.height;
        float viewH = _viewport.rect.height;
        float scrollable = contentH - viewH;
        if (scrollable <= 0.01f) return;

        float needUp = (viewBottom + _padding) - blockBottom;   // >0 => блок торчит вниз
        float needDown = blockTop - (viewTop - _padding);       // >0 => блок торчит вверх

        float pos = _scrollRect.verticalNormalizedPosition;

        if (needUp > 0f)
        {
            float delta = needUp / scrollable;
            pos = Mathf.Clamp01(pos - delta);
        }
        else if (needDown > 0f)
        {
            float delta = needDown / scrollable;
            pos = Mathf.Clamp01(pos + delta);
        }
        else
        {
            return;
        }

        _scrollRect.verticalNormalizedPosition = pos;
        Canvas.ForceUpdateCanvases();
    }
}
