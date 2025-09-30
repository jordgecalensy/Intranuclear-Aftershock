using UnityEngine;

[DisallowMultipleComponent]
public class OutlineEffect : MonoBehaviour
{
    [Tooltip("Материал обводки (например, Unlit с жёлтым цветом)")]
    public Material outlineMaterial;
    [Range(0f, 0.2f)]
    public float outlineWidth = 0.03f;
    [Tooltip("Если у объекта SkinnedMeshRenderer: запекать меш каждый кадр (нужно для анимации).")]
    public bool bakeSkinnedMeshEveryFrame = false;

    private GameObject outlineObject;
    private Mesh outlineMesh;
    private MeshFilter outlineMF;
    private MeshRenderer outlineMR;
    private MeshFilter originalMF;
    private SkinnedMeshRenderer skinned;

    void Awake()
    {
        originalMF = GetComponent<MeshFilter>();
        skinned = GetComponent<SkinnedMeshRenderer>();
    }

    void Start()
    {
        if (outlineMaterial == null)
        {
            Debug.LogWarning($"[OutlineEffect] Outline material not set on '{name}'");
            return;
        }

        outlineObject = new GameObject(name + "_Outline");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;

        outlineMF = outlineObject.AddComponent<MeshFilter>();
        outlineMR = outlineObject.AddComponent<MeshRenderer>();
        outlineMR.sharedMaterial = outlineMaterial;

        if (originalMF != null && originalMF.sharedMesh != null)
        {
            outlineMF.sharedMesh = originalMF.sharedMesh;
        }
        else if (skinned != null)
        {
            outlineMesh = new Mesh();
            outlineMF.sharedMesh = outlineMesh;
            skinned.BakeMesh(outlineMesh);
        }
        else
        {
            Debug.LogWarning($"[OutlineEffect] No MeshFilter or SkinnedMeshRenderer on '{name}' — outline disabled.");
            Destroy(outlineObject);
            outlineObject = null;
            return;
        }

        // Простое увеличение масштаба для "обводки".
        outlineObject.transform.localScale = Vector3.one * (1f + outlineWidth);

        // По умолчанию скрываем
        outlineObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (outlineObject == null) return;

        if (skinned != null && bakeSkinnedMeshEveryFrame)
        {
            // обновляем меш при анимации
            skinned.BakeMesh(outlineMesh);
        }
    }

    public void SetVisible(bool state)
    {
        if (outlineObject != null)
            outlineObject.SetActive(state);
    }
}
