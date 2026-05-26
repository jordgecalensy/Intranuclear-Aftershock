using FMODUnity;
using UnityEngine;

public class PhysicalInteractionsSfx : MonoBehaviour
{
    private bool _rollingCheck = false;
    private Rigidbody _rb;
    
    [Header("the magnitude at which it is triggered RollingObject")]
    [SerializeField] private float _magnitude = 0.1f; // Порог чувствительности
    [Header("SFX effect")]
    [SerializeField] private EventReference _dropObject;
    [SerializeField] private EventReference _rollingObject;

    private void OnEnable()
    {
        _rb = GetComponent<Rigidbody>();
    }
    private void OnCollisionEnter(Collision collision)
    {
        SoundUtils3D.Play(gameObject, _dropObject);
    }
    private void OnCollisionStay(Collision collision)
    {
        float angularVelocity = _rb.angularVelocity.magnitude;

        if (angularVelocity > _magnitude)
        {
            if (_rollingCheck) return;
            SoundUtils3D.Play(gameObject, _rollingObject);
            _rollingCheck = true;
        }
    }
    private void OnCollisionExit(Collision collision)
    {
        _rollingCheck = false;
    }
}
