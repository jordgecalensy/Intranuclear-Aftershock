using UnityEngine;

public class OnCanvasOn : MonoBehaviour
{
    
    // Update is called once per frame
    void Update()
    {
        if (this.gameObject.activeInHierarchy)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
