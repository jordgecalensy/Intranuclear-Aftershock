using UnityEngine;

public class VisionMode : MonoBehaviour
{
    public Camera playerCamera;           // Ссылка на камеру игрока
    public KeyCode toggleKey = KeyCode.V; // Клавиша переключения режима
    private bool specialVision = false;   // Включён ли режим

    private int defaultMask;  // Маска по умолчанию
    private int specialMask;  // Маска с "доп. объектами"

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        // Запоминаем стандартную маску
        defaultMask = playerCamera.cullingMask;

        // Создаём маску: обычные слои + SpecialObjects
        int specialLayer = 1 << LayerMask.NameToLayer("SpecialObjects");
        specialMask = defaultMask | specialLayer;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            specialVision = !specialVision;

            if (specialVision)
                playerCamera.cullingMask = specialMask;
            else
                playerCamera.cullingMask = defaultMask;
        }
    }
}