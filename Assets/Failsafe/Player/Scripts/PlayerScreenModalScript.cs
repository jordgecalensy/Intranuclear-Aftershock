using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerScreenModalScript : MonoBehaviour
{
    /// <summary>Статический флаг для проверки из PlayerController (FSM, Tick) — открыт ли полноэкранный просмотр камеры.</summary>
    public static bool IsCameraFullScreen { get; private set; }

    [SerializeField] private TextMeshProUGUI _cameraIndexText;
    [SerializeField] private GameObject _screenPlate;
    [SerializeField] private RawImage _cameraDisplay;
    private CameraManager _cameraManager;
    private bool _isFullScreen = false;
    public bool IsFullScreen => _isFullScreen;

    private void Start()
    {
        _screenPlate.SetActive(false);
        IsCameraFullScreen = false;
    }

    public void InFullScreen(CameraManager cameraManager)
    {
        _cameraManager = cameraManager;
        _cameraDisplay.texture = _cameraManager.RenderTexture;
        _cameraIndexText.text = $"CAM{_cameraManager.CurrentCameraIndex + 1}";
        _screenPlate.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        _isFullScreen = true;
        IsCameraFullScreen = true;
    }

    public void ExitFullScreen()
    {
        IsCameraFullScreen = false;
        _screenPlate.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _isFullScreen = false;
    }

    public void RotateCameraHorizontal(float horizontalInput)
    {
        if (_cameraManager.cameraScriptArray != null)
        {
            _cameraManager.cameraScriptArray[_cameraManager.CurrentCameraIndex].RotateCamera(horizontalInput, 0f);
        }
    }

    public void RotateCameraVertical(float verticalInput)
    {
        if (_cameraManager.cameraScriptArray != null)
        {
            _cameraManager.cameraScriptArray[_cameraManager.CurrentCameraIndex].RotateCamera(0f, verticalInput);
        }
    }

    public void NextCamera()
    {
        _cameraManager.NextCamera();
    }

    public void PreviousCamera()
    {
        _cameraManager.PreviousCamera();
    }

    public void ZoomCamera()
    {
        if (_cameraManager.cameraScriptArray != null)
        {
            _cameraManager.cameraScriptArray[_cameraManager.CurrentCameraIndex].zoomCamera();
        }
    }
}
