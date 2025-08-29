using UnityEngine;
using UnityEngine.UI;

public class ConsoleButton : Interactable
{
    private Button _button;

    private void Start()
    {
        _button = gameObject.GetComponent<Button>();
    }
    protected override void Interact()
    {
        _button.onClick.Invoke();
    }
}
