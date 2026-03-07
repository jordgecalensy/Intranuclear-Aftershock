using UnityEngine;

public class CameraScript : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _cameraRotationPoint;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float verticalUpRotationLimit = 45f;
    [SerializeField] private float verticalDownRotationLimit = -45f;
    [SerializeField] private float horizontalLeftRotationLimit = 90f;
    [SerializeField] private float horizontalRightRotationLimit = -90f;
    [SerializeField] private float zoom = 10f;
    private float defaultFOV;
    private bool isZoomed = false;

    private void Start()
    {
        defaultFOV = _camera.fieldOfView;
    }

    public void SetCameraActive(bool isActive)
    {
        _camera.gameObject.SetActive(isActive);
    }

    public void RotateCamera(float horizontalInput, float verticalInput)
    {
        if (!_camera.gameObject.activeInHierarchy || _cameraRotationPoint == null) return;

        float delta = rotationSpeed * Time.deltaTime;
        float horizontalDelta = horizontalInput * delta;
        float verticalDelta = verticalInput * delta;

        Vector3 euler = _cameraRotationPoint.eulerAngles;
        float vertical = NormalizeAngle(euler.x) - verticalDelta;
        float horizontal = NormalizeAngle(euler.y) + horizontalDelta;

        vertical = Mathf.Clamp(vertical, verticalDownRotationLimit, verticalUpRotationLimit);
        horizontal = Mathf.Clamp(horizontal, horizontalRightRotationLimit, horizontalLeftRotationLimit);

        _cameraRotationPoint.eulerAngles = new Vector3(vertical, horizontal, 0f);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public void zoomCamera()
    {
        if (!_camera.gameObject.activeInHierarchy) return;

        if (isZoomed)
        {
            _camera.fieldOfView = defaultFOV;
            isZoomed = false;
        }
        else
        {
            _camera.fieldOfView = zoom;
            isZoomed = true;
        }
    }
}
