using FMODUnity;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(StudioEventEmitter))]
public class Stasisable : MonoBehaviour
{
    private Rigidbody _rb;
    private Renderer[] _renderers;
    private Coroutine _startedCoroutine;
    private Enemy _enemy;
    [SerializeField] private FMODUnity.EventReference _stasisEnd;
    [SerializeField] private Material _stasisMaterial;
    private Vector3 _objectVelocity;

    void Start()
    {
        _enemy = GetComponentInParent<Enemy>();
        if(_enemy == null)
            _rb = GetComponent<Rigidbody>();
        _renderers = GetComponentsInChildren<Renderer>();
    }
    public void StasisHit(float duration, bool defaultMode)
    {
        if (_enemy != null)
            _enemy.DisableState(duration);

        _startedCoroutine ??= StartCoroutine(FreezeRigidbody(duration, defaultMode));
    }

    private IEnumerator FreezeRigidbody(float duration, bool defaultMode)
    {
        ApplyStasisMaterial();

        if (_enemy == null)
        {
            _objectVelocity = _rb.linearVelocity;
            _rb.isKinematic = true;
            _rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        yield return new WaitForSeconds(duration);

        SoundUtils3D.Play(this.gameObject, _stasisEnd);
        if (_enemy == null)
        {
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.None;
            if (!defaultMode)
                _rb.AddForce(_objectVelocity, ForceMode.VelocityChange);
        }

        _startedCoroutine = null;
        RemoveStasisMaterial();
    }

    private void ApplyStasisMaterial()
    {
        foreach (Renderer renderer in _renderers) {
            Material[] mats = new Material[renderer.materials.Length + 1];
            renderer.materials.CopyTo(mats, 0);
            mats[mats.Length - 1] = _stasisMaterial;
            renderer.materials = mats;
        }
    }

    private void RemoveStasisMaterial() 
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
