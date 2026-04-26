using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Button button;

    bool holding;

    void Start()
    {
        button = gameObject.GetComponent<Button>();
    }

    void Update()
    {
        if (holding)
            button.onClick.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        holding = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        holding = false;
    }
}