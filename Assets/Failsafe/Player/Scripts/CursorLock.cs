using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CursorLock : MonoBehaviour
{
    [SerializeField] private bool _lockCursor = true;

    [SerializeField] private Camera fpsCamera;        // перетащи свою FPS-камеру
    [SerializeField] private LayerMask uiLayerMask = 14;

    private EventSystem eventSystem;
    private GameObject lastHoveredObject = null;


    private void Start()
    {
        if (_lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        eventSystem = EventSystem.current;
        if (fpsCamera == null) fpsCamera = Camera.main;
    }

    private void Update()
    {
        print("Hover test");
        // Симулируем указатель точно в центре экрана (кроссхейр)
        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = new Vector2(Screen.width / 2f, Screen.height / 2f)
        };

        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        // Берём первый попавшийся UI-элемент
        Debug.Log("Hovering check: " + results.Count + " hits");
        GameObject currentHovered = null;
        if (results.Count > 0)
        {
            currentHovered = results[1].gameObject;
            Debug.Log("Hovering over: " + currentHovered.name);
        }

        // === HOVER ЛОГИКА ===
        if (currentHovered != lastHoveredObject)
        {
            // Выходим из предыдущей кнопки
            if (lastHoveredObject != null)
            {
                ExecuteEvents.Execute(lastHoveredObject, pointerData, ExecuteEvents.pointerExitHandler);
            }

            // Входим в новую кнопку
            if (currentHovered != null)
            {
                ExecuteEvents.Execute(currentHovered, pointerData, ExecuteEvents.pointerEnterHandler);
            }

            lastHoveredObject = currentHovered;
        }

        // === КЛИК (левая кнопка мыши) ===
        if (Input.GetMouseButtonDown(0) && currentHovered != null)
        {
            ExecuteEvents.Execute(currentHovered, pointerData, ExecuteEvents.pointerClickHandler);
            // или ExecuteEvents.Execute(currentHovered, pointerData, ExecuteEvents.pointerDownHandler);
        }
    }

}
