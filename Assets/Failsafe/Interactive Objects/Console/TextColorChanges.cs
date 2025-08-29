using TMPro;
using UnityEngine;

public class TextColorChanges : MonoBehaviour
{
    private TextMeshProUGUI _tmpText;
    private void Start()
    {
        _tmpText = GetComponent<TextMeshProUGUI>();
    }
    public void UpdateColor(string hexColor)
    {
        if (ColorUtility.TryParseHtmlString(hexColor, out Color newColor))
        {
            _tmpText.color = newColor;
        }
        else
        {
            Debug.LogError("Неверный формат hex цвета!");
        }
    }
}
