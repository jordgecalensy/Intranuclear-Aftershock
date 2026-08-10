using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OnSelectEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    string _mainOriginalText;
    readonly Dictionary<TextMeshProUGUI, Color> _originalTextColors = new();
    Image _buttonBackground;
    Color _originalBackgroundColor;
    [SerializeField] bool _addArrow;
    [SerializeField] Color _targetColor;
    [SerializeField] TextMeshProUGUI _mainTextMeshProUGUI;
    [SerializeField] List<TextMeshProUGUI> _optionalTextsGO;


    private void Start()
    {
        _buttonBackground = GetComponent<Image>();

        _mainOriginalText = _mainTextMeshProUGUI.text;
        if (!_optionalTextsGO.Contains(_mainTextMeshProUGUI))
        {
            _optionalTextsGO.Add(_mainTextMeshProUGUI);
        }

        foreach (TextMeshProUGUI text in _optionalTextsGO)
        {
            _originalTextColors[text] = text.color;
        }

        _originalBackgroundColor = _buttonBackground.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("Pointer Entered");


        foreach (TextMeshProUGUI v in _optionalTextsGO)
        {
            Color targetColor = _targetColor;
            targetColor.a = _originalTextColors[v].a;
            v.color = targetColor;

        }
        if (_addArrow)
        {
            _mainTextMeshProUGUI.text = ">" + _mainOriginalText;
        }

        _buttonBackground.color = new Color(1, 1, 1, 1);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("Pointer Exited");


        foreach (TextMeshProUGUI v in _optionalTextsGO)
        {
            v.color = _originalTextColors[v];

        }

        _mainTextMeshProUGUI.text = _mainOriginalText;
        _buttonBackground.color = _originalBackgroundColor;

    }

}
