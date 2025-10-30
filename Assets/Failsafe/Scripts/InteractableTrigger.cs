using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableTrigger : MonoBehaviour
{
    private Interactable interactable;

    private void Awake()
    {
        // Получаем ссылку на компонент Interactable, который есть на этом объекте
        interactable = GetComponent<Interactable>();
    }

    // Если используешь обычную физику — Rigidbody + Collider (не IsTrigger)
    private void OnCollisionEnter(Collision collision)
    {
        // Проверяем, есть ли у объекта физика
        if (collision.rigidbody != null)
        {
            interactable.BaseInteract();
        }
    }

    // Если используешь "триггерный" вариант (IsTrigger = true)
    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody != null)
        {
            interactable.BaseInteract();
        }
    }
}
