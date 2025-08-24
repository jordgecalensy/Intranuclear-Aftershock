using UnityEngine;

/// <summary>
/// Позволяет свободно перемещаться по сцене в режиме noclip для отладки.
/// </summary>
public class NoclipController : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Основная скорость передвижения.")]
    [SerializeField] private float speed = 10.0f;

    [Tooltip("Множитель скорости при зажатом Shift.")]
    [SerializeField] private float speedMultiplier = 2.5f;

    [Tooltip("Чувствительность мыши.")]
    [SerializeField] private float mouseSensitivity = 2.0f;

    [Tooltip("Клавиша для включения/выключения режима noclip.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.V;

    private bool isNoclipActive = false;
    private float rotationX = 0;
    private float rotationY = 0;
    private CharacterController characterController;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        // Изначально noclip выключен, поэтому ничего не делаем с курсором
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isNoclipActive = !isNoclipActive;

            if (characterController != null)
            {
                characterController.enabled = !isNoclipActive;
            }

            if (isNoclipActive)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                // Сохраняем текущий поворот камеры при активации
                rotationY = transform.eulerAngles.y;
                rotationX = transform.eulerAngles.x;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        if (!isNoclipActive) return;

        // Управление мышью
        rotationY += Input.GetAxis("Mouse X") * mouseSensitivity;
        rotationX -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        rotationX = Mathf.Clamp(rotationX, -90, 90);

        transform.localEulerAngles = new Vector3(rotationX, rotationY, 0);

        // Перемещение
        float currentSpeed = speed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed *= speedMultiplier;
        }

        float moveForward = Input.GetAxis("Vertical") * currentSpeed * Time.deltaTime;
        float moveSideways = Input.GetAxis("Horizontal") * currentSpeed * Time.deltaTime;

        transform.position += transform.forward * moveForward;
        transform.position += transform.right * moveSideways;

        if (Input.GetKey(KeyCode.Space))
        {
            transform.position += Vector3.up * currentSpeed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.LeftControl))
        {
            transform.position += Vector3.down * currentSpeed * Time.deltaTime;
        }
    }
}
