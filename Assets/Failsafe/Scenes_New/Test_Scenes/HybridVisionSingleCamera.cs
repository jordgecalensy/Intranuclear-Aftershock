using UnityEngine;
using System.Collections.Generic;

public class HybridVisionSingleCamera : MonoBehaviour
{
    public Camera playerCamera;              // основная камера игрока
    public KeyCode toggleKey = KeyCode.V;    // кнопка переключения
    public string specialLayerName = "SpecialObjects"; // слой для особых объектов
    public string specialTag = "Special";    // тег для аутлайна

    private bool specialVision = false;
    private int defaultMask;
    private int specialMask;

    private List<OutlineEffect> outlines = new List<OutlineEffect>();

    void Start()
    {
        if (playerCamera == null) 
            playerCamera = Camera.main;

        // запоминаем маску по умолчанию
        defaultMask = playerCamera.cullingMask;

        // получаем ID слоя
        int specialLayer = LayerMask.NameToLayer(specialLayerName);
        if (specialLayer == -1)
        {
            Debug.LogError($"[HybridVision] Слой '{specialLayerName}' не найден! Создай его в Inspector → Layers.");
            return;
        }

        // маска с дополнительным слоем
        specialMask = defaultMask | (1 << specialLayer);

        // собираем все объекты с тегом Special
        RefreshOutlines();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            specialVision = !specialVision;

            // переключаем видимость слоя
            playerCamera.cullingMask = specialVision ? specialMask : defaultMask;

            // включаем/выключаем аутлайн
            foreach (var o in outlines)
                if (o != null) o.SetVisible(specialVision);
        }
    }

    public void RefreshOutlines()
    {
        outlines.Clear();
        GameObject[] taggedObjects;
        try
        {
            taggedObjects = GameObject.FindGameObjectsWithTag(specialTag);
        }
        catch
        {
            Debug.LogWarning($"[HybridVision] Тег '{specialTag}' не найден. Создай его в Inspector → Tags.");
            return;
        }

        foreach (var go in taggedObjects)
        {
            var oe = go.GetComponent<OutlineEffect>();
            if (oe != null) outlines.Add(oe);
        }
    }
}