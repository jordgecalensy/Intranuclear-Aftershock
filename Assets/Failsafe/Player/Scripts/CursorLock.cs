using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class CursorLock : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool _lockCursor = true;
    
    private EventSystem _eventSystem;
    private GameObject _lastHoveredObject = null;

    private void Start()
    {
        // Настройка состояния курсора при старте
        if (_lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Первичный поиск EventSystem
        _eventSystem = EventSystem.current;
    }

    private void Update()
    {
        // 1. Проверяем наличие EventSystem (если вдруг его не было на старте)
        if (_eventSystem == null)
        {
            _eventSystem = EventSystem.current;
            if (_eventSystem == null) return; 
        }

        // 2. Создаем данные виртуального указателя в центре экрана
        PointerEventData pointerData = new PointerEventData(_eventSystem)
        {
            position = new Vector2(Screen.width / 2f, Screen.height / 2f)
        };

        // 3. Выполняем Raycast по элементам интерфейса
        List<RaycastResult> results = new List<RaycastResult>();
        _eventSystem.RaycastAll(pointerData, results);

        // 4. Определяем объект под "прицелом" (самый верхний — индекс 0)
        GameObject currentHovered = (results.Count > 0) ? results[0].gameObject : null;

        // 5. Обрабатываем наведение (Hover) и нажатия (Click)
        HandleHover(currentHovered, pointerData);
        HandleClick(currentHovered, pointerData);
    }

    private void HandleHover(GameObject current, PointerEventData data)
    {
        // Если объект под прицелом изменился
        if (current != _lastHoveredObject)
        {
            // Уводим "курсор" со старого объекта
            if (_lastHoveredObject != null)
            {
                ExecuteEvents.Execute(_lastHoveredObject, data, ExecuteEvents.pointerExitHandler);
            }

            // Наводим "курсор" на новый объект
            if (current != null)
            {
                ExecuteEvents.Execute(current, data, ExecuteEvents.pointerEnterHandler);
            }

            _lastHoveredObject = current;
        }
    }

    private void HandleClick(GameObject current, PointerEventData data)
    {
        if (current == null) return;

        // Нажали левую кнопку мыши
        if (Input.GetMouseButtonDown(0))
        {
            ExecuteEvents.Execute(current, data, ExecuteEvents.pointerDownHandler);
        }
        
        // Отпустили левую кнопку мыши
        if (Input.GetMouseButtonUp(0))
        {
            ExecuteEvents.Execute(current, data, ExecuteEvents.pointerUpHandler);
            // Выполняем само действие клика
            ExecuteEvents.Execute(current, data, ExecuteEvents.pointerClickHandler);
        }
    }
}
