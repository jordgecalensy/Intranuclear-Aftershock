using UnityEngine;
using UnityEngine.UI;

public class ConsoleButton : Interactable
{
    private Button _button;

    [SerializeField] private bool _isHover = true;

    [SerializeField] private GameObject _buttonImage; 
    [SerializeField] private GameObject _buttonHoverImage; 

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
        if (!_isHover) return;
        _buttonImage.SetActive(false);
        _buttonHoverImage.SetActive(true);
    }

    protected override void HoverExit()
    {
        if (!_isHover) return;
        _buttonHoverImage.SetActive(false);
        _buttonImage.SetActive(true);
    }
}
