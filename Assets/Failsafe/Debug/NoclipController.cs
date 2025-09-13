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

    [Header("Камера от третьего лица")]
    [Tooltip("Клавиша для переключения вида от третьего лица.")]
    [SerializeField] private KeyCode thirdPersonToggleKey = KeyCode.T;

    [Tooltip("Трансформ камеры, которую нужно перемещать.")]
    [SerializeField] private Transform playerCameraTransform;

    [Tooltip("Смещение камеры в режиме от третьего лица.")]
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0, 1.5f, -4f);

    private bool isNoclipActive = false;
    private bool isThirdPersonActive = false;
    private Vector3 firstPersonLocalPosition;
    private float rotationX = 0;
    private float rotationY = 0;
    private CharacterController characterController;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (playerCameraTransform != null)
        {
            firstPersonLocalPosition = playerCameraTransform.localPosition;
        }
        // Изначально noclip выключен, поэтому ничего не делаем с курсором
    }

    private void Update()
    {
        HandleNoclipToggle();
        HandleThirdPersonToggle();

        if (isNoclipActive)
        {
            UpdateNoclipMovement();
        }
    }

    private void HandleNoclipToggle()
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
                // Если активируем noclip, отключаем вид от 3-го лица
                if (isThirdPersonActive)
                {
                    ToggleThirdPersonView(false);
                }

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
    }

    private void HandleThirdPersonToggle()
    {
        // Не позволяем переключать вид в режиме noclip
        if (isNoclipActive) return;

        if (Input.GetKeyDown(thirdPersonToggleKey))
        {
            isThirdPersonActive = !isThirdPersonActive;
            ToggleThirdPersonView(isThirdPersonActive);
        }
    }

    private void ToggleThirdPersonView(bool enable)
    {
        if (playerCameraTransform == null) return;

        if (enable)
        {
            // Сохраняем исходное положение, если еще не сохранено
            if (firstPersonLocalPosition == Vector3.zero && playerCameraTransform.localPosition != Vector3.zero)
            {
                firstPersonLocalPosition = playerCameraTransform.localPosition;
            }
            playerCameraTransform.localPosition = thirdPersonOffset;
        }
        else
        {
            playerCameraTransform.localPosition = firstPersonLocalPosition;
        }
    }

    private void UpdateNoclipMovement()
    {
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
