using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;
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

    // runtime
    private readonly Collider[] _hits = new Collider[64];
    private readonly HashSet<FireAreaAdvanced> _affectedFires = new();
    private Transform _cam;
    private bool _using;
    private float _chargeLeft;

    // --- публичные геттеры для UI/логики ---
    public float ChargeLeftSeconds => _chargeLeft;
    public float ChargeMaxSeconds => maxChargeSeconds;
    public float Charge01 => maxChargeSeconds > 0f ? Mathf.Clamp01(_chargeLeft / maxChargeSeconds) : 0f;
    public bool IsEmpty => _chargeLeft <= 0.0001f;

    private void Awake()
    {
        _chargeLeft = Mathf.Max(0f, maxChargeSeconds);
    }

    public void OnGrabbed(Transform playerCamera)
    {
        _cam = playerCamera;
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
        _affectedFires.Clear();

        for (int i = 0; i < count; i++)
        {
            var col = _hits[i];
            if (!col) continue;

            Vector3 dir = (col.bounds.center - origin).normalized;
            if (Vector3.Dot(fwd, dir) < cosLimit) continue;

            var fire = col.GetComponentInParent<FireAreaAdvanced>();
            if (fire == null || !_affectedFires.Add(fire)) continue;

            fire.AddExtinguishImpulse(extinguishPerSec * dt);
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

    public void OnInserted()
    {
        
    }

    public void OnEjected()
    {
        
    }

    // IEnumerator MoveWithDelay(Vector3 targetPos, Quaternion targetRot, float speed, float delayTime)
    // {
    //     float timer = 0f;
    //     while (timer < delayTime)
    //     {
    //         if (IsGrabbed)
    //             yield break;
    //         timer += Time.fixedDeltaTime;
    //         yield return new WaitForFixedUpdate();
    //     }
    //     while ((Vector3.Distance(rb.position, targetPos) > 0.001f ||
    //            Quaternion.Angle(rb.rotation, targetRot) > 0.1f) &&
    //            IsGrabbed == false)
    //     {
    //         rb.MovePosition(
    //             Vector3.MoveTowards(rb.position, targetPos, speed * Time.fixedDeltaTime)
    //         );
    //         rb.MoveRotation(
    //             Quaternion.RotateTowards(rb.rotation, targetRot, speed * Time.fixedDeltaTime * 360)
    //         );
    //         yield return new WaitForFixedUpdate();
    //     }
    //     if (IsGrabbed == false) rb.position = targetPos;
    // }

    // public void OnInserted(Transform holderTransform, float speed, float delayTime)
    // {
    //     OnDropped();
    //     rb.isKinematic = true;
    //     StartCoroutine(MoveWithDelay(holderTransform.position, holderTransform.rotation, speed, delayTime));
    //     TryFullRefill();
    // }

    // public void OnEjected()
    // {
    //     rb.isKinematic = false;
    // }
}
