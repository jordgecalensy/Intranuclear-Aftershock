using UnityEngine;
using UnityEngine.VFX;

public class LaserBeamController : MonoBehaviour
{
    private const string CarryObjectsLayer = "CarryObjects";
    private const string DestructibleLayer = "DestructableByEarthquake";

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

    [Header("Aim Inertia")]
    [SerializeField, Min(0.01f)] private float aimSmoothTime = 0.3f;
    [SerializeField, Min(0.1f)] private float maxAimPointSpeed = 20f;

    private Transform _origin;
    private Transform _target;
    private bool _initialized;

    private Vector3 _smoothedAimPoint;
    private Vector3 _aimVelocity;
    private Vector3 _currentDirection;
    private Vector3 _currentEndPoint;
    private RaycastHit _currentHit;
    private bool _hasCurrentHit;
    private int _effectiveRaycastMask;

    private readonly RaycastHit[] _raycastHits = new RaycastHit[32];

    private int _idStart, _idEnd, _idLen;
    private bool _hasStart, _hasEnd, _hasLen;

    public Vector3 CurrentDirection => _currentDirection;
    public Vector3 CurrentEndPoint => _currentEndPoint;

    public void Initialize(Transform origin, Transform target)
    {
        _origin = origin;
        _target = target;
        _initialized = (_origin != null && _target != null);

        if (_initialized)
        {
            _smoothedAimPoint = _target.position;
            _aimVelocity = Vector3.zero;
        }

        RefreshRaycastMask();

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

    public void SetMaximumLength(float length)
    {
        maxLength = Mathf.Max(0.1f, length);
    }

    public bool TryGetCurrentHit(out RaycastHit hit)
    {
        hit = _currentHit;
        return _hasCurrentHit && hit.collider != null;
    }

    private void OnEnable()
    {
        if (vfx != null) vfx.Play();
    }

    private void OnDisable()
    {
        _hasCurrentHit = false;
        if (vfx != null) vfx.Stop();
    }

    private void LateUpdate()
    {
        if (!_initialized || _origin == null || _target == null)
        {
            _hasCurrentHit = false;
            if (vfx != null) vfx.Stop();
            return;
        }

        Vector3 start = _origin.position;
        _smoothedAimPoint = SmoothAimPoint(
            _smoothedAimPoint,
            _target.position,
            ref _aimVelocity,
            aimSmoothTime,
            maxAimPointSpeed,
            Time.deltaTime);

        Vector3 dir = _smoothedAimPoint - start;
        float distanceToAimPoint = dir.magnitude;

        if (distanceToAimPoint < 1e-4f)
            dir = _origin.forward;
        else
            dir /= distanceToAimPoint;

        if (dir.sqrMagnitude < 1e-4f)
            return;

        _currentDirection = dir.normalized;
        float maxLen = Mathf.Max(0.1f, maxLength);

        float finalLen = maxLen;
        Vector3 finalEnd = start + _currentDirection * finalLen;

        if (TryFindFirstExternalHit(start, _currentDirection, maxLen, out RaycastHit hit))
        {
            _currentHit = hit;
            _hasCurrentHit = true;
            finalLen = hit.distance;
            finalEnd = hit.point;
        }
        else
        {
            _currentHit = default;
            _hasCurrentHit = false;
        }

        _currentEndPoint = finalEnd;

        Vector3 vfxStart = start;
        Vector3 vfxEnd   = finalEnd;

        if (parentToOrigin)
        {
            transform.position = start;
            transform.rotation = Quaternion.LookRotation(_currentDirection, Vector3.up);

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

    private bool TryFindFirstExternalHit(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit closestHit)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            _raycastHits,
            distance,
            _effectiveRaycastMask,
            QueryTriggerInteraction.Ignore);

        Transform ownerRoot = _origin != null ? _origin.root : null;
        float closestDistance = float.PositiveInfinity;
        closestHit = default;
        bool found = false;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = _raycastHits[index];
            Collider collider = hit.collider;

            if (collider == null || IsInHierarchy(collider.transform, ownerRoot))
                continue;

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            closestHit = hit;
            found = true;
        }

        return found;
    }

    private void RefreshRaycastMask()
    {
        _effectiveRaycastMask = BuildEffectiveRaycastMask(
            raycastMask.value,
            LayerMask.NameToLayer(CarryObjectsLayer),
            LayerMask.NameToLayer(DestructibleLayer));
    }

    private static bool IsInHierarchy(Transform candidate, Transform root)
    {
        return root != null && (candidate == root || candidate.IsChildOf(root));
    }

    public static Vector3 SmoothAimPoint(
        Vector3 current,
        Vector3 target,
        ref Vector3 velocity,
        float smoothTime,
        float maximumSpeed,
        float deltaTime)
    {
        if (deltaTime <= 0f)
            return current;

        return Vector3.SmoothDamp(
            current,
            target,
            ref velocity,
            Mathf.Max(0.01f, smoothTime),
            Mathf.Max(0.1f, maximumSpeed),
            deltaTime);
    }

    public static int BuildEffectiveRaycastMask(
        int baseMask,
        int carryObjectsLayer,
        int destructibleLayer)
    {
        int mask = AddLayerToMask(baseMask, carryObjectsLayer);
        return AddLayerToMask(mask, destructibleLayer);
    }

    private static int AddLayerToMask(int mask, int layer)
    {
        if (layer < 0 || layer > 31)
            return mask;

        return mask | (1 << layer);
    }

    private void OnValidate()
    {
        maxLength = Mathf.Max(0.1f, maxLength);
        aimSmoothTime = Mathf.Max(0.01f, aimSmoothTime);
        maxAimPointSpeed = Mathf.Max(0.1f, maxAimPointSpeed);
        RefreshRaycastMask();
    }

    private void OnDestroy()
    {
        if (vfx != null) vfx.Stop();
    }
}
