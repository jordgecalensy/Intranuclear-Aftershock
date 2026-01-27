using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
public class TMPTextBlockHeightProxy : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;          // дочерний TMP
    [SerializeField] private RectTransform _paddingSource; // опционально, если хочешь учитывать паддинги

    private LayoutElement _le;

    private void Awake()
    {
        _le = GetComponent<LayoutElement>();
        if (_text == null) _text = GetComponentInChildren<TMP_Text>(true);
    }

    private void LateUpdate()
    {
        if (_text == null) return;

        // заставляем TMP обновить свои размеры
        _text.ForceMeshUpdate();

        // PreferredHeight берём через LayoutUtility (самый стабильный способ)
        var textRT = _text.rectTransform;
        float preferred = LayoutUtility.GetPreferredHeight(textRT);

        // На всякий случай: если TMP почему-то отдаёт 0, берём bounds
        if (preferred <= 0.001f)
            preferred = _text.preferredHeight;

        _le.preferredHeight = preferred;
        _le.minHeight = preferred;
    }
}
