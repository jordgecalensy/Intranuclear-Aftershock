using UnityEngine;
using UnityEngine.VFX;

public class LaserBeamController : MonoBehaviour
{
    [Header("VFX")]
    [SerializeField] private VisualEffect vfx;

    [Tooltip("Exposed Vector3 в VFX Graph")]
    [SerializeField] private string startPosProperty = "Beam_Start";
    [Tooltip("Exposed Vector3 в VFX Graph")]
    [SerializeField] private string endPosProperty   = "Beam_End";
    [Tooltip("Exposed float в VFX Graph (опц.)")]
    [SerializeField] private string lengthProperty   = "Beam_Length";

    [Header("Logic")]
    [SerializeField] private float maxLength = 30f;
    [SerializeField] private LayerMask raycastMask = ~0;
    [SerializeField] private bool parentToOrigin = true;

    private Transform _origin;
    private Transform _target;
    private bool _initialized;

    private int _idStart, _idEnd, _idLen;
    private bool _hasStart, _hasEnd, _hasLen;

    public void Initialize(Transform origin, Transform target)
    {
        _origin = origin;
        _target = target;
        _initialized = (_origin != null && _target != null);

        if (parentToOrigin && _origin != null)
            transform.SetParent(_origin, worldPositionStays: true);

        if (vfx == null)
            vfx = GetComponent<VisualEffect>();

        if (vfx != null)
        {
            // Подготовим IDs и проверим, что такие параметры реально существуют в графе
            _idStart = Shader.PropertyToID(startPosProperty);
            _idEnd   = Shader.PropertyToID(endPosProperty);
            _idLen   = Shader.PropertyToID(lengthProperty);

            _hasStart = !string.IsNullOrEmpty(startPosProperty) && vfx.HasVector3(_idStart);
            _hasEnd   = !string.IsNullOrEmpty(endPosProperty)   && vfx.HasVector3(_idEnd);
            _hasLen   = !string.IsNullOrEmpty(lengthProperty)   && vfx.HasFloat  (_idLen);

            vfx.Play(); // idempotent в Unity 6
        }
    }

    private void OnEnable()
    {
        if (vfx != null) vfx.Play();
    }

    private void OnDisable()
    {
        if (vfx != null) vfx.Stop();
    }

    private void Update()
    {
        if (!_initialized || _origin == null || _target == null)
        {
            if (vfx != null) vfx.Stop();
            return;
        }

        Vector3 start = _origin.position;
        Vector3 end   = _target.position;
        Vector3 dir   = end - start;
        float dist    = dir.magnitude;
        if (dist < 1e-4f) return;

        dir /= dist;
        float maxLen = Mathf.Min(dist, maxLength);

        // Рейкаст до препятствия
        float finalLen = maxLen;
        Vector3 finalEnd = start + dir * finalLen;

        if (Physics.Raycast(start, dir, out RaycastHit hit, maxLen, raycastMask, QueryTriggerInteraction.Ignore))
        {
            finalLen = hit.distance;
            finalEnd = hit.point;
        }

        // Локальные/мировые координаты для графа
        Vector3 vfxStart = start;
        Vector3 vfxEnd   = finalEnd;

        if (parentToOrigin)
        {
            transform.position = start;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            vfxStart = transform.InverseTransformPoint(start);
            vfxEnd   = transform.InverseTransformPoint(finalEnd);
        }

        if (vfx != null)
        {
            if (_hasStart) vfx.SetVector3(_idStart, vfxStart);
            if (_hasEnd)   vfx.SetVector3(_idEnd,   vfxEnd);
            if (_hasLen)   vfx.SetFloat  (_idLen,   finalLen);
        }
    }

    private void OnDestroy()
    {
        if (vfx != null) vfx.Stop();
    }
}