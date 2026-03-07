using TMPro;
using UnityEngine;

public class PlayerScreenModalScript : MonoBehaviour
{
    /// <summary>Статический флаг для проверки из PlayerController (FSM, Tick) — открыт ли полноэкранный просмотр камеры.</summary>
    public static bool IsCameraFullScreen { get; private set; }

    [SerializeField] private TextMeshProUGUI _cameraIndexText;
    [SerializeField] private GameObject _screenPlate;
    private CameraScript[] cameraScript;
    private int currentCameraIndex = 0;
    private bool _isFullScreen = false;
    public bool IsFullScreen => _isFullScreen;

    private void Start()
    {
        _screenPlate.SetActive(false);
    }

    public void InFullScreen(CameraScript[] camera, int currentCameraIndex)
    {
        cameraScript = camera;
        this.currentCameraIndex = currentCameraIndex;
        cameraScript[currentCameraIndex].SetCameraActive(true);
        _cameraIndexText.text = $"CAM{currentCameraIndex + 1}";
        _screenPlate.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        _isFullScreen = true;
        IsCameraFullScreen = true;
    }

    public void ExitFullScreen()
    {
        IsCameraFullScreen = false;
        if (cameraScript != null)
        {
            cameraScript[currentCameraIndex].SetCameraActive(false);
        }
        _screenPlate.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _isFullScreen = false;
    }

    public void RotateCameraHorizontal(float horizontalInput)
    {
        if (cameraScript != null)
        {
            cameraScript[currentCameraIndex].RotateCamera(horizontalInput, 0f);
        }
    }

    public void RotateCameraVertical(float verticalInput)
    {
        if (cameraScript != null)
        {
            cameraScript[currentCameraIndex].RotateCamera(0f, verticalInput);
        }
    }

    public void NextCamera()
    {
        if (cameraScript != null)
        {
            cameraScript[currentCameraIndex].SetCameraActive(false);
            currentCameraIndex = (currentCameraIndex + 1) % cameraScript.Length;
            cameraScript[currentCameraIndex].SetCameraActive(true);
            _cameraIndexText.text = $"CAM{currentCameraIndex + 1}";
        }
    }

    public void PreviousCamera()
    {
        if (cameraScript != null)
        {
            cameraScript[currentCameraIndex].SetCameraActive(false);
            currentCameraIndex = (currentCameraIndex - 1 + cameraScript.Length) % cameraScript.Length;
            cameraScript[currentCameraIndex].SetCameraActive(true);
            _cameraIndexText.text = $"CAM{currentCameraIndex + 1}";
        }
    }

    public void ZoomCamera()
    {
        if (cameraScript != null)
        {
            cameraScript[currentCameraIndex].zoomCamera();
        }
    }
}
