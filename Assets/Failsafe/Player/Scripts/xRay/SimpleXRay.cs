using UnityEngine;

/// <summary>
/// Упрощённый XRay-эффект без логики питания.
/// Автоматически находит все Renderer'ы на объекте и его дочерних объектах.
/// Достаточно повесить на родителя — все дети с мешами будут подсвечены.
/// </summary>
public class SimpleXRay : MonoBehaviour
{
    [Header("X-Ray")]
    [SerializeField] private Material xRayMaterial;

    private Renderer[] renderers;
    private Material[][] originalMats;
    private bool isXRayEnabled = false;
    private bool isSuppressed = false;

    void Awake()
    {
        CacheRenderers();
    }

    /// <summary>
    /// Кэширует все Renderer'ы (включая дочерние объекты) и их оригинальные материалы.
    /// </summary>
    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        originalMats = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMats[i] = renderers[i].materials;
        }
    }

    /// <summary>
    /// Подавляет или разрешает XRay-эффект.
    /// При подавлении XRay принудительно выключается и не может быть включён.
    /// При снятии подавления XRay снова может быть активирован визором.
    /// </summary>
    /// <param name="suppressed">true = подавить (инвентарь), false = разрешить (мир)</param>
    public void SetSuppressed(bool suppressed)
    {
        isSuppressed = suppressed;

        // Если подавляем и XRay был включён — принудительно выключаем
        if (suppressed && isXRayEnabled)
        {
            isXRayEnabled = false;
            RestoreOriginalMaterials();
        }
    }

    /// <summary>
    /// Включает или выключает XRay-эффект на всех Renderer'ах.
    /// Не сработает, если XRay подавлен (объект в инвентаре).
    /// </summary>
    /// <param name="state">true = включить XRay, false = выключить</param>
    public void SetXRay(bool state)
    {
        if (renderers == null || renderers.Length == 0 || isXRayEnabled == state) return;

        // Блокируем включение, если объект в инвентаре
        if (state && isSuppressed) return;

        isXRayEnabled = state;

        if (state)
        {
            ApplyXRayMaterial();
        }
        else
        {
            RestoreOriginalMaterials();
        }
    }

    /// <summary>
    /// Применяет XRay материал ко всем Renderer'ам (заменяет все материалы).
    /// </summary>
    private void ApplyXRayMaterial()
    {
        if (xRayMaterial == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            // Заменяем каждый слот материала на XRay, чтобы покрыть все submesh'и
            Material[] mats = new Material[originalMats[i].Length];
            for (int j = 0; j < mats.Length; j++)
                mats[j] = xRayMaterial;

            renderers[i].materials = mats;
        }
    }

    /// <summary>
    /// Возвращает оригинальные материалы всем Renderer'ам.
    /// </summary>
    private void RestoreOriginalMaterials()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].materials = originalMats[i];
        }
    }
}
