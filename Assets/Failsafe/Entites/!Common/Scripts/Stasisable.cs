using FMODUnity;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(StudioEventEmitter))]
public class Stasisable : MonoBehaviour
{
    private Rigidbody _rb;
    private Coroutine _startedCoroutine;
    [SerializeField] private FMODUnity.EventReference _stasisEnd;
    private Vector3 _objectVelocity;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }
    public void StartStasis(float duration)
    {
        _startedCoroutine ??= StartCoroutine(FreezeRigidbody(duration));
    }
    public void StartStasisWithInertion(float duration)
    {
        _startedCoroutine ??= StartCoroutine(FreezeRigidbodyWithInertion(duration));
    }

    private IEnumerator FreezeRigidbody(float duration)
    {
        _rb.isKinematic = true;
        _rb.constraints = RigidbodyConstraints.FreezeAll;

        yield return new WaitForSeconds(duration);

        SoundUtils3D.Play(this.gameObject, _stasisEnd);
        _rb.isKinematic = false;
        _rb.constraints = RigidbodyConstraints.None;
        _startedCoroutine = null;

    }

    IEnumerator FreezeRigidbodyWithInertion(float duration)
    {
        _objectVelocity = _rb.linearVelocity;
        _rb.isKinematic = true;
        _rb.constraints = RigidbodyConstraints.FreezeAll;

        yield return new WaitForSeconds(duration);

        SoundUtils3D.Play(this.gameObject, _stasisEnd);
        _rb.isKinematic = false;
        _rb.constraints = RigidbodyConstraints.None;
        _rb.AddForce(_objectVelocity, ForceMode.VelocityChange);
        _startedCoroutine = null;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_startedCoroutine != null)
        {
            _rb.isKinematic = true;
            _rb.constraints = RigidbodyConstraints.FreezeAll;

        }
    }
}
