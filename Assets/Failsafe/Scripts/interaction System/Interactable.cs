using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    public void BaseInteract()
    {
        Interact();
    }

    public void BaseInteract(PlayerInteractionContext context)
    {
        Interact(context);
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
        // Старый вариант без контекста.
    }

    protected virtual void Interact(PlayerInteractionContext context)
    {
        // По умолчанию вызываем старый Interact(),
        // чтобы не ломать уже существующие Interactable.
        Interact();
    }

    protected virtual void Hover()
    {
        // Можно добавить эффект наведения.
    }

    protected virtual void HoverExit()
    {
        // Можно добавить эффект выхода из наведения.
    }
}