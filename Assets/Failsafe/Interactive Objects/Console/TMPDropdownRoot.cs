using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TMPDropdownRoot : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform _header;
    [SerializeField] private RectTransform _listContainer;
    [SerializeField] private RectTransform _itemsContent;
    [SerializeField] private RectTransform _viewport;
    [SerializeField] private CanvasGroup _listCanvasGroup;

    [Header("Texts")]
    [SerializeField] private TMP_Text _dataLabel;     // Header/Data
    [SerializeField] private TMP_Text _summaryLabel;  // Header/Summary
    [SerializeField] private TMP_Text _bodyLabel;     // Text (TMP) (1)

    [Header("Layout")]
    [SerializeField] private LayoutElement _rootLayout;
    [SerializeField] private LayoutElement _listLayout;
    [SerializeField] private float _maxListHeight = 260f;
    [SerializeField] private bool _useMaxListHeight = true;

    [Header("Images")]
    [SerializeField] private Image _imageClosed;
    [SerializeField] private Image _imageOpened;

    public int SelectedIndex { get; private set; } = -1;
    private bool _isOpen;

    private void Awake()
    {
        if (_rootLayout == null) _rootLayout = GetComponent<LayoutElement>();
        if (_listLayout == null && _listContainer != null) _listLayout = _listContainer.GetComponent<LayoutElement>();
        if (_listCanvasGroup == null && _listContainer != null) _listCanvasGroup = _listContainer.GetComponent<CanvasGroup>();

        _isOpen = false;
        ApplyCanvasGroup(false);
        UpdateImages();   
        UpdateHeights();
        ForceRebuild();
    }

    public void Toggle() => SetOpenState(!_isOpen);

    public void SetOpenState(bool open)
    {
        if (_isOpen == open) return;
        _isOpen = open;

        ApplyCanvasGroup(_isOpen);
        UpdateImages();  
        UpdateHeights();
        ForceRebuild();
    }

    private void ApplyCanvasGroup(bool visible)
    {
        if (_listCanvasGroup == null) return;
        _listCanvasGroup.alpha = visible ? 1f : 0f;
        _listCanvasGroup.blocksRaycasts = visible;
        _listCanvasGroup.interactable = visible;
    }

    // === JSON binding helpers ===
    public void SetHeader(string data, string summary)
    {
        if (_dataLabel != null) _dataLabel.text = data ?? "";
        if (_summaryLabel != null) _summaryLabel.text = summary ?? "";
    }

    public void SetBodyText(string body)
    {
        if (_bodyLabel != null) _bodyLabel.text = body ?? "";
        // После смены текста надо пересчитать layout
        UpdateHeights();
        ForceRebuild();
    }

    // Вызываем из инсталера после SetText'ов
    public void RebuildNow()
    {
        UpdateHeights();
        ForceRebuild();
    }

    public void Select(int index)
    {
        SelectedIndex = index;
        // если нужно: закрывать после выбора
        // SetOpenState(false);
    }

    private void UpdateImages()
    {
        if (_imageClosed != null)
            _imageClosed.enabled = !_isOpen;

        if (_imageOpened != null)
            _imageOpened.enabled = _isOpen;
    }

    private void UpdateHeights()
    {
        float headerH = GetRectHeight(_header);

        float listH = 0f;
        if (_isOpen)
        {
            if (_itemsContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_itemsContent);

            listH = _itemsContent != null ? _itemsContent.rect.height : 0f;

            if (_useMaxListHeight)
                listH = Mathf.Min(listH, _maxListHeight);
        }

        if (_listLayout != null) _listLayout.preferredHeight = listH;
        if (_rootLayout != null) _rootLayout.preferredHeight = headerH + listH;

        if (_viewport != null)
            _viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, listH);
    }

    private static float GetRectHeight(RectTransform rt)
    {
        if (rt == null) return 0f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        return rt.rect.height;
    }

    private void ForceRebuild()
    {
        var self = transform as RectTransform;
        if (self != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(self);

        var parent = transform.parent as RectTransform;
        if (parent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
    }
}
