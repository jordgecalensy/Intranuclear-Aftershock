using System.Collections;
using UnityEngine;

public sealed class ExtinguisherCarryable : MonoBehaviour, ICarryUsable, IInsertable
{
    [Header("Extinguish")]
    [SerializeField] private LayerMask fireMask;
    [SerializeField] private float range = 6f;
    [SerializeField] private float coneAngleDeg = 25f;
    [SerializeField] private float extinguishPerSec = 2.0f;

    [Header("Charge")]
    [Tooltip("Сколько секунд непрерывного распыления доступно.")]
    [SerializeField] private float maxChargeSeconds = 12f;
    [Tooltip("Сколько секунд заряда тратится за 1 секунду распыления.")]
    [SerializeField] private float consumptionPerSecond = 1f;
    [Tooltip("Можно ли пополнить (например, заменой баллона).")]
    [SerializeField] private bool rechargeable = false;

    [Header("FX")]
    [SerializeField] private ParticleSystem sprayFx;
    private Rigidbody rb;

    // runtime
    private readonly Collider[] _hits = new Collider[64];
    private Transform _cam;
    private bool _using;
    private float _chargeLeft;
    private bool _isGrabbed = false;

    // --- публичные геттеры для UI/логики ---
    public float ChargeLeftSeconds => _chargeLeft;
    public float ChargeMaxSeconds => maxChargeSeconds;
    public float Charge01 => maxChargeSeconds > 0f ? Mathf.Clamp01(_chargeLeft / maxChargeSeconds) : 0f;
    public bool IsEmpty => _chargeLeft <= 0.0001f;
    public bool IsGrabbed => _isGrabbed;

    private void Awake()
    {
        _chargeLeft = Mathf.Max(0f, maxChargeSeconds);
        rb = GetComponent<Rigidbody>();
    }

    public void OnGrabbed(Transform playerCamera)
    {
        _cam = playerCamera;
        _isGrabbed = true;
    }

    public void OnUseStart()
    {
        if (IsEmpty) return;

        _using = true;
        if (sprayFx && !sprayFx.isPlaying) sprayFx.Play();

        // 👉 здесь можно дернуть событие для FMOD (Start распыления)
    }

    public void UseTick(float dt)
    {
        if (!_using || _cam == null) return;

        // тратим заряд
        if (!IsEmpty)
        {
            _chargeLeft = Mathf.Max(0f, _chargeLeft - consumptionPerSecond * dt);
        }

        if (IsEmpty)
        {
            OnUseStop();
            // 👉 здесь можно дернуть событие для FMOD (звук пустого баллона)
            return;
        }

        var origin = _cam.position;
        var fwd    = _cam.forward;

        int count = Physics.OverlapSphereNonAlloc(
            origin + fwd * (range * 0.5f),
            range * 0.5f,
            _hits,
            fireMask,
            QueryTriggerInteraction.Collide);

        float cosLimit = Mathf.Cos(coneAngleDeg * Mathf.Deg2Rad);

        for (int i = 0; i < count; i++)
        {
            var col = _hits[i];
            if (!col) continue;

            Vector3 dir = (col.bounds.center - origin).normalized;
            if (Vector3.Dot(fwd, dir) < cosLimit) continue;

            var fire = col.GetComponentInParent<FireAreaAdvanced>();
            if (fire == null) continue;

            fire.intensity = Mathf.Max(0f, fire.intensity - extinguishPerSec * dt);

            if (fire.intensity < 0.5f)
                fire.maxRadius = Mathf.Max(fire.initialRadius, fire.maxRadius - (extinguishPerSec * 0.5f) * dt);

            if (fire.intensity <= 0.01f)
                Destroy(fire.gameObject);
        }
    }

    public void OnUseStop()
    {
        _using = false;
        if (sprayFx && sprayFx.isPlaying)
            sprayFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // 👉 здесь можно дернуть событие для FMOD (Stop распыления)
    }

    public void OnDropped()
    {
        OnUseStop();
        _cam = null;
        _isGrabbed = false;
    }

    // --- Пополнение (если понадобится) ---
    public bool TryRefill(float seconds)
    {
        if (!rechargeable || seconds <= 0f) return false;
        _chargeLeft = Mathf.Min(maxChargeSeconds, _chargeLeft + seconds);
        return true;
    }

    public bool TryFullRefill()
    {
        if (!rechargeable) return false;
        _chargeLeft = maxChargeSeconds;
        return true;
    }

    // --- Вставка/извлечение в/из держатель ---

    IEnumerator Move(Vector3 targetPos, Quaternion targetRot, float speed)
    {
        while ((Vector3.Distance(rb.position, targetPos) > 0.001f ||
               Quaternion.Angle(rb.rotation, targetRot) > 0.1f) &&
               IsGrabbed == false)
        {
            rb.MovePosition(
                Vector3.MoveTowards(rb.position, targetPos, speed * Time.fixedDeltaTime)
            );
            rb.MoveRotation(
                Quaternion.RotateTowards(rb.rotation, targetRot, speed * Time.fixedDeltaTime * 360)
            );
            yield return new WaitForFixedUpdate();
        }
        if (IsGrabbed == false) rb.position = targetPos;
    }

    public void OnInserted(Transform holderTransform, float speed)
    {
        OnDropped();
        rb.isKinematic = true;
        StartCoroutine(Move(holderTransform.position, holderTransform.rotation, speed));
        TryFullRefill();
    }

    public void OnEjected()
    {
        rb.isKinematic = false;
    }
}