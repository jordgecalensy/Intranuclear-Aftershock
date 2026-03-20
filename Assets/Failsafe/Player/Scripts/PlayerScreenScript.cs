using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class PlayerScreenScript : MonoBehaviour
{
    // [SerializeField] private TextMeshProUGUI _cameraIndexText;
    // [SerializeField] private GameObject _screenPlate;
    // [SerializeField] private RawImage _cameraDisplay;
    public static bool IsCameraFullScreen { get; private set; }
    [SerializeField] private Camera PlayerCamera;
    private CameraManager _cameraManager;
    private bool use = false;
    private bool zoom = false;
    private Vector2 movement = Vector2.zero;

    [Inject] private InputHandler _inputHandler;

    private void Update()
    {
        // use = _inputHandler.GrabLedgeTrigger.IsPressed;
        zoom = _inputHandler.ZoomTriggered;
        movement = _inputHandler.MovementInput;

        if (IsCameraFullScreen)
        {
            if (zoom)
            {
                ZoomCamera(true);
            }
            else
            {
                ZoomCamera(false);
            }
            if (movement != Vector2.zero)
            {
                RotateCameraHorizontal(movement.x);
                RotateCameraVertical(movement.y);
            }
        }

        // if (use && IsCameraFullScreen)
        // {
        //     ExitFullScreen();
        // }
    }

    public void InFullScreen(CameraManager cameraManager)
    {
        _cameraManager = cameraManager;
        // _cameraDisplay.texture = _cameraManager.RenderTexture;
        // _cameraIndexText.text = $"CAM{_cameraManager.CurrentCameraIndex + 1}";
        // _screenPlate.SetActive(true);
        cameraManager.SetFullScreenCamera(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        IsCameraFullScreen = true;
    }

    public void ExitFullScreen()
    {
        // _screenPlate.SetActive(false);
        if (_cameraManager != null)
        {
            _cameraManager.SetFullScreenCamera(false);
        }
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        IsCameraFullScreen = false;
    }

    // public bool IsEnterFullScreen(out Camera camera)
    // {
    //     if (IsCameraFullScreen)
    //     {
    //         camera = ConsoleCamera;
    //         return true;
    //     }
    //     else
    //     {
    //         camera = null;
    //         return false;
    //     }
    // }

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

    public void ZoomCamera(bool zoomIn)
    {
        if (_cameraManager.cameraScriptArray != null)
        {
            _cameraManager.cameraScriptArray[_cameraManager.CurrentCameraIndex].zoomCamera(zoomIn);
        }
    }
}
