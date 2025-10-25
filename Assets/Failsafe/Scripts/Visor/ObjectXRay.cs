using UnityEngine;

public class ObjectXRay : MonoBehaviour
{
    [SerializeField] private Material xrayMaterial;

    private Renderer rend;
    private Material[] originalMats;
    private bool isXRay = false;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            originalMats = rend.materials;
    }

    public void SetXRay(bool state)
    {
        if (rend == null || isXRay == state) return;
        isXRay = state;

        if (state)
        {
            // добавляем XRay поверх оригинальных
            Material[] mats = new Material[originalMats.Length + 1];
            originalMats.CopyTo(mats, 0);
            mats[mats.Length - 1] = xrayMaterial;
            rend.materials = mats;
        }
        else
        {
            // возвращаем как было
            rend.materials = originalMats;
        }
    }
}