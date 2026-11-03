using System.Collections.Generic;
using Failsafe.Scripts.Damage;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider))]
public class MeleeHitbox : MonoBehaviour
{
    [Tooltip("Слои коллайдеров, которым оружие может нанести урон.")]
    [SerializeField] private LayerMask _targetLayers;

    [Tooltip("Твёрдые препятствия, которые останавливают взмах. Триггеры игнорируются.")]
    [SerializeField] private LayerMask _obstacleLayers;

    [Tooltip("Дополнительные электрические эффекты при попадании. Физический урон задаётся в Enemy Config.")]
    [SerializeField] private EffectBundle _electricEffects;

    [Tooltip("Уязвимая точка, достижение порога которой отключает электрические эффекты оружия.")]
    [SerializeField] private WeakPoint _electricDisableWeakPoint;

    // Аргумент: насколько раньше текущей позы произошёл контакт, в секундах.
    public event System.Action<float> ObstacleHit;

    private readonly HashSet<object> _hitTargets = new HashSet<object>();
    private readonly Collider[] _overlaps = new Collider[32];
    private CapsuleCollider _collider;
    private Enemy _owner;
    private IEffectApplicationService _effects;
    private EffectApplicationServiceResolver _effectResolver;
    private bool _electricEffectsDisabled;
    private bool _swingActive;
    private bool _windowOpen;
    private bool _closeRequested;
    private bool _hasPreviousPose;
    private Vector3 _previousCenter;
    private Quaternion _previousRotation;
    private float _previousRadius;
    private float _previousHalfSegment;
    private float _previousSampleTime;

    private bool CanHit => _swingActive && _windowOpen && isActiveAndEnabled &&
        _owner != null && _owner.isActiveAndEnabled;

    [Inject]
    public void Construct(IEffectApplicationService effects)
    {
        _effects = effects;
        _effectResolver?.Set(effects);
    }

    private void Awake()
    {
        _collider = GetComponent<CapsuleCollider>();
        _collider.isTrigger = true;
        _collider.enabled = false;
        _owner = GetComponentInParent<Enemy>();
        _effectResolver = new EffectApplicationServiceResolver(gameObject, _effects);
        if (_electricDisableWeakPoint != null)
        {
            _electricDisableWeakPoint.ThresholdReached += DisableElectricEffects;
            _electricEffectsDisabled = _electricDisableWeakPoint.IsThresholdReached;
        }
    }

    private void OnDestroy()
    {
        if (_electricDisableWeakPoint != null)
            _electricDisableWeakPoint.ThresholdReached -= DisableElectricEffects;
    }

    private void DisableElectricEffects() => _electricEffectsDisabled = true;

    public void BeginSwing()
    {
        EndSwing();
        _hitTargets.Clear();
        _swingActive = true;
    }

    public void OpenWindow()
    {
        if (!_swingActive || _windowOpen || !isActiveAndEnabled || _owner == null || !_owner.isActiveAndEnabled)
            return;

        _windowOpen = true;
        _closeRequested = false;
        _hasPreviousPose = false;
        _collider.enabled = true;
        // Контакты проверяются в LateUpdate, после обновления костей Animator.
    }

    public void CloseWindow()
    {
        // Сначала проверяем последний участок взмаха за этот кадр.
        _closeRequested = _windowOpen;
    }

    public void EndSwing()
    {
        _windowOpen = false;
        _swingActive = false;
        _closeRequested = false;
        _hasPreviousPose = false;
        if (_collider != null) _collider.enabled = false;
    }

    private void OnDisable() => EndSwing();

    private void LateUpdate()
    {
        if (!CanHit) return;
        ScanMotion();
        if (_closeRequested)
        {
            _windowOpen = false;
            _closeRequested = false;
            _collider.enabled = false;
        }
    }

    private void ScanMotion()
    {
        Vector3 scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        int axis = _collider.direction;
        Vector3 localAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        float radiusScale = Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]);
        float radius = _collider.radius * radiusScale;
        float halfSegment = Mathf.Max(0f, _collider.height * scale[axis] * 0.5f - radius);
        Vector3 center = transform.TransformPoint(_collider.center);
        Quaternion rotation = transform.rotation;

        if (!_hasPreviousPose)
        {
            _previousCenter = center;
            _previousRotation = rotation;
            _previousRadius = radius;
            _previousHalfSegment = halfSegment;
            _previousSampleTime = Time.time;
            _hasPreviousPose = true;
        }

        // Учитываем и перенос, и поворот длинной дубинки между кадрами.
        float travel = Vector3.Distance(_previousCenter, center) +
            Quaternion.Angle(_previousRotation, rotation) * Mathf.Deg2Rad * Mathf.Max(_previousHalfSegment, halfSegment) +
            Mathf.Abs(halfSegment - _previousHalfSegment);
        float stepSize = Mathf.Max(0.001f, Mathf.Min(_previousRadius, radius) * 0.5f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(travel / stepSize));
        float elapsed = Time.time - _previousSampleTime;

        Physics.SyncTransforms();
        for (int step = 1; step <= steps && CanHit; step++)
        {
            float fraction = step / (float)steps;
            Vector3 sampleCenter = Vector3.Lerp(_previousCenter, center, fraction);
            Vector3 offset = Quaternion.Slerp(_previousRotation, rotation, fraction) * localAxis *
                Mathf.Lerp(_previousHalfSegment, halfSegment, fraction);
            CheckContacts(sampleCenter - offset, sampleCenter + offset,
                Mathf.Lerp(_previousRadius, radius, fraction), elapsed * (1f - fraction));
        }

        _previousCenter = center;
        _previousRotation = rotation;
        _previousRadius = radius;
        _previousHalfSegment = halfSegment;
        _previousSampleTime = Time.time;
    }

    private void CheckContacts(Vector3 pointA, Vector3 pointB, float radius, float secondsBeforeCurrentPose)
    {
        int mask = _targetLayers.value | _obstacleLayers.value;
        Collider[] contacts = _overlaps;
        int count = Physics.OverlapCapsuleNonAlloc(pointA, pointB, radius, contacts, mask, QueryTriggerInteraction.Ignore);
        if (count == contacts.Length)
        {
            // Не теряем стену в плотном скоплении коллайдеров.
            contacts = Physics.OverlapCapsule(pointA, pointB, radius, mask, QueryTriggerInteraction.Ignore);
            count = contacts.Length;
        }

        // Если контакты произошли в одной пробе, препятствие имеет приоритет.
        // На порядок OnTriggerEnter и порядок массива Physics полагаться нельзя.
        for (int i = 0; i < count; i++)
        {
            Collider other = contacts[i];
            if (other.transform.IsChildOf(_owner.transform) ||
                (_obstacleLayers.value & (1 << other.gameObject.layer)) == 0) continue;

            EndSwing();
            ObstacleHit?.Invoke(secondsBeforeCurrentPose);
            return;
        }

        for (int i = 0; i < count && CanHit; i++) TryHit(contacts[i]);
    }

    private void TryHit(Collider other)
    {
        if (!CanHit) return;
        if (other.isTrigger || (_targetLayers.value & (1 << other.gameObject.layer)) == 0 ||
            other.transform.IsChildOf(_owner.transform))
            return;
        if (!DamageTargetResolver.TryResolve(other, out var target) || !_hitTargets.Add(target.Identity))
            return;

        Vector3 point = other.ClosestPoint(transform.position);
        Vector3 direction = _owner.transform.forward;
        var damage = new DamageInfo(_owner.EnemyConfig.Damage, DamageType.Physical,
            DamageApplicationKind.Contact, _owner.gameObject,
            point, direction);
        DamageResistanceUtility.ApplyDamage(target, damage);

        if (_electricEffectsDisabled || _electricEffects == null || other == null) return;
        _effectResolver.TryResolve(Time.unscaledTime, false);
        if (_effectResolver.Service == null)
        {
            Debug.LogWarning("MeleeHitbox: effect application service not found; electric effects were not applied.", this);
            return;
        }

        _effectResolver.Service.Apply(_electricEffects, new EffectContext(
            _owner.gameObject, other, point, -direction, direction));
    }
}
