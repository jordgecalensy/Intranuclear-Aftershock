using UnityEngine;

public class LightScript : MonoBehaviour
{
    private Light _light;
    private void Start()
    {
        _light = GetComponent<Light>();
    }
    public void OnLight()
    {
        _light.enabled = true;
    }

    public void OffLight()
    {
        _light.enabled = false;
    }
}
