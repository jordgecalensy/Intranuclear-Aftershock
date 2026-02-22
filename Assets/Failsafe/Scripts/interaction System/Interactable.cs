using UnityEngine;

public abstract class Interactable : MonoBehaviour
{

    public void BaseInteract()
    {
        Interact();
    }

    public void OnHover()
    {
        Hover();
    }

    public void OnHoverExit()
    {
        HoverExit();
    }

    protected virtual void Interact()
    {
        //функция которую будут переопределять подклассы
    }

    protected virtual void Hover()
    {
        //можно добавить эффект наведения на кнопку
    }

    protected virtual void HoverExit()
    {
        //можно добавить эффект выхода из наведения на кнопку
    }
}
