using UnityEngine;

public class ObjectXRay : MonoBehaviour
{
    [Header("Материалы X-Ray")]
    [SerializeField] private Material poweredXRayMaterial;
    [SerializeField] private Material unpoweredXRayMaterial;

    private Renderer rend;
    private Material[] originalMats;
    private bool isXRayEnabled = false;
    private bool isPowered = false;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            originalMats = rend.materials;
    }

    /// <summary>
    /// Включает или выключает XRay-эффект.
    /// </summary>
    /// <param name="state">true = включить XRay, false = выключить</param>
    public void SetXRay(bool state)
    {
        if (rend == null || isXRayEnabled == state) return;
        isXRayEnabled = state;

        if (state)
        {
            ApplyCurrentXRayMaterial();
        }
        else
        {
            // Возвращаем исходные материалы
            rend.materials = originalMats;
        }
    }

    /// <summary>
    /// Устанавливает состояние питания (для выбора нужного XRay материала).
    /// </summary>
    /// <param name="powered">true = питание есть, false = нет</param>
    public void SetPoweredState(bool powered)
    {
        if (isPowered == powered) return;
        isPowered = powered;

        // Если XRay включен, обновим материал под новое состояние питания
        if (isXRayEnabled)
            ApplyCurrentXRayMaterial();
    }

    /// <summary>
    /// Применяет нужный XRay материал в зависимости от состояния питания.
    /// </summary>
    private void ApplyCurrentXRayMaterial()
    {
        if (rend == null) return;

        Material overlay = isPowered ? poweredXRayMaterial : unpoweredXRayMaterial;

        if (overlay == null)
        {
            rend.materials = originalMats;
            return;
        }

        Material[] mats = new Material[originalMats.Length + 1];
        originalMats.CopyTo(mats, 0);
        mats[mats.Length - 1] = overlay;
        rend.materials = mats;
    }
}
