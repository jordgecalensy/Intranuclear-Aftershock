using UnityEngine;
using System.Collections;

public class Scaneble : MonoBehaviour
{
    private Renderer[] _renderers;
    private Material _scanMaterial;
    private Coroutine _startedCoroutine;

    private void OnEnable()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }
    public void StasisHit(float duration, Material material)
    {
        _scanMaterial = material;
        _startedCoroutine ??= StartCoroutine(DurationScan(duration));
    }
    private IEnumerator DurationScan(float duration)
    {
        ApplyScanMaterial();
        yield return new WaitForSeconds(duration);
        _startedCoroutine = null;
        RemoveScanMaterial();
        Scaneble scaneble = GetComponent<Scaneble>();
        Destroy(scaneble);
    }
    private void ApplyScanMaterial()
    {
        foreach (Renderer renderer in _renderers)
        {
            Material[] mats = new Material[renderer.materials.Length + 1];
            renderer.materials.CopyTo(mats, 0);
            mats[mats.Length - 1] = _scanMaterial;
            renderer.materials = mats;
        }
    }

    private void RemoveScanMaterial()
    {
        foreach (Renderer renderer in _renderers)
        {
            Material[] mats = new Material[renderer.materials.Length - 1];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = renderer.materials[i];
            }
            renderer.materials = mats;
        }
    }
}
