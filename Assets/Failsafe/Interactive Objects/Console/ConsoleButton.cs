using UnityEngine;
using UnityEngine.UI;

public class ConsoleButton : Interactable
{
    private Button _button;

    [SerializeField] private GameObject _buttonImage; // Название кнопки для отладки
    [SerializeField] private GameObject _buttonHoverImage; // Название кнопки для отладки

    private void Start()
    {
        _button = gameObject.GetComponent<Button>();
    }
    protected override void Interact()
    {
        _button.onClick.Invoke();
    }

    protected override void Hover()
    {
        _buttonImage.SetActive(false);
        _buttonHoverImage.SetActive(true);
    }

    protected override void HoverExit()
    {
        _buttonHoverImage.SetActive(false);
        _buttonImage.SetActive(true);
    }
}
