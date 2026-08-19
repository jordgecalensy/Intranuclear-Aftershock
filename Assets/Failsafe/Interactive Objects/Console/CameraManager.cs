using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Управление камерами для консоли наблюдения. Позволяет переключаться между камерами, отображать их на экране и открывать их на весь экран.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [SerializeField] private CameraScript[] cameraScript;
    [SerializeField] private TextMeshProUGUI cameraIndexText;
    [SerializeField] private RawImage cameraDisplay;
    [SerializeField] private int screenWidth = 378;
    [SerializeField] private int screenHeight = 320;
    [SerializeField] private Camera cameraConsole;
    public Camera CameraConsole => cameraConsole;
    private RenderTexture renderTexture;
    public RenderTexture RenderTexture => renderTexture;
    private int currentCameraIndex = 0;
    public int CurrentCameraIndex => currentCameraIndex;
    public CameraScript[] cameraScriptArray => cameraScript;
    private PlayerScreenScript playerScreenModalScript;

    private void Start()
    {
        playerScreenModalScript = FindAnyObjectByType<PlayerScreenScript>();
        Debug.Log(playerScreenModalScript); 
        renderTexture = new RenderTexture(screenWidth, screenHeight, 16);
        cameraDisplay.texture = renderTexture;
        if (cameraScript.Length > 0)
        {
            cameraScript[currentCameraIndex].SetCameraActive(renderTexture);
            UpdateCameraIndexText();
        }
        SetFullScreenCamera(false);
    }

    public void SetFullScreenCamera(bool on)
    {
        if (on)
        {
            cameraConsole.enabled = true;
            cameraConsole.gameObject.SetActive(true);
        }
        else
        {
            cameraConsole.enabled = false;
            cameraConsole.gameObject.SetActive(false);
        }
    }

    public void NextCamera()
    {
        cameraScript[currentCameraIndex].SetCameraInactive();
        currentCameraIndex = (currentCameraIndex + 1) % cameraScript.Length;
        cameraScript[currentCameraIndex].SetCameraActive(renderTexture);
        UpdateCameraIndexText();
    }

    public void PreviousCamera()
    {
        cameraScript[currentCameraIndex].SetCameraInactive();
        currentCameraIndex = (currentCameraIndex - 1 + cameraScript.Length) % cameraScript.Length;
        cameraScript[currentCameraIndex].SetCameraActive(renderTexture);
        UpdateCameraIndexText();
    }

    public void ToggleScreenCamera()
    {
        if (playerScreenModalScript != null)
        {
            if (PlayerScreenScript.IsCameraFullScreen)
            {
                playerScreenModalScript.ExitFullScreen();
            }
            else
            {
                playerScreenModalScript.InFullScreen(this);
            }
        }
    }

    public void ExitScreenCamera()
    {
        if (playerScreenModalScript != null && PlayerScreenScript.IsCameraFullScreen)
        {
            playerScreenModalScript.ExitFullScreen();
        }
    }

    public void EnterScreenCamera()
    {
        if (playerScreenModalScript != null && !PlayerScreenScript.IsCameraFullScreen)
        {
            playerScreenModalScript.InFullScreen(this);
        }
    }

    private void UpdateCameraIndexText()
    {
        cameraIndexText.text = $"CAM{currentCameraIndex + 1}";
    }
}
