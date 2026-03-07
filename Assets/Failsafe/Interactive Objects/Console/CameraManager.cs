using TMPro;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private CameraScript[] cameraScript;
    [SerializeField] private TextMeshProUGUI cameraIndexText;
    private int currentCameraIndex = 0;
    private PlayerScreenModalScript playerScreenModalScript;

    private void Start()
    {
        playerScreenModalScript = FindAnyObjectByType<PlayerScreenModalScript>();
        if (cameraScript.Length > 0)
        {
            cameraScript[currentCameraIndex].SetCameraActive(true);
            UpdateCameraIndexText();
        }
    }

    public void NextCamera()
    {
        cameraScript[currentCameraIndex].SetCameraActive(false);
        currentCameraIndex = (currentCameraIndex + 1) % cameraScript.Length;
        cameraScript[currentCameraIndex].SetCameraActive(true);
        UpdateCameraIndexText();
    }

    public void PreviousCamera()
    {
        cameraScript[currentCameraIndex].SetCameraActive(false);
        currentCameraIndex = (currentCameraIndex - 1 + cameraScript.Length) % cameraScript.Length;
        cameraScript[currentCameraIndex].SetCameraActive(true);
        UpdateCameraIndexText();
    }

    public void FullScreenCamera()
    {
        Debug.Log("Attempting to enter full screen mode for camera: " + currentCameraIndex);
        if (playerScreenModalScript != null)
        {
            playerScreenModalScript.InFullScreen(cameraScript, currentCameraIndex);
        }
    }

    private void UpdateCameraIndexText()
    {
        cameraIndexText.text = $"CAM{currentCameraIndex + 1}";
    }
}
