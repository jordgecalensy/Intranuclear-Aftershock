using System;
using Failsafe.Scripts.Damage;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

[DisallowMultipleComponent]
public sealed class BorzoiGrenadeLauncher : MonoBehaviour
{
    private static readonly int GrenadesSpent = Animator.StringToHash("GrenadesSpent");

    [SerializeField] private Animator _animator;
    [SerializeField] private FragGrenadeObject _grenadePrefab;
    [SerializeField] private BorzoiFlashGrenade _flashGrenadePrefab;
    [SerializeField] private ThrowGrenadeData _throwData;
    [SerializeField] private Transform _launchPoint;
    [SerializeField] private WeakPoint _beltWeakPoint;
    [SerializeField, Min(1)] private int _grenadesPerBelt = 3;
    [SerializeField, Min(0)] private int _flashGrenadesPerBelt = 1;
    [SerializeField, Min(0f)] private float _minRange = 8f;
    [SerializeField, Min(0f)] private float _maxRange = 13f;
    [SerializeField, Min(0f)] private float _throwCooldown = 1.4f;
    [SerializeField, Min(0.1f)] private float _flightTime = 0.85f;
    [SerializeField, Min(0.1f)] private float _retreatSpeed = 3.5f;
    [SerializeField] private EffectBundle _defensiveMeleeEffects;
    [SerializeField, Min(1f)] private float _spentMeleeDamageMultiplier = 1.6f;
    [SerializeField, Range(0.1f, 1f)] private float _spentMeleeCooldownMultiplier = 0.65f;

    private int _remainingGrenades;
    private bool _hasReloaded;
    private Collider[] _ownerColliders;
    private IEffectApplicationService _effects;

    public bool IsReady =>
        isActiveAndEnabled && _animator != null && _grenadePrefab != null &&
        _throwData != null && _launchPoint != null;
    public bool NeedsReload => _remainingGrenades == 0 && !_hasReloaded;
    public bool IsDepleted => _remainingGrenades == 0 && _hasReloaded;
    public bool HasGrenades => IsReady && _remainingGrenades > 0;
    public float PreferredRange => Mathf.Lerp(_minRange, _maxRange, 0.3f);
    public float RetreatSpeed => _retreatSpeed;
    public float ThrowCooldown => _throwCooldown;
    public EffectBundle DefensiveMeleeEffects => HasGrenades ? _defensiveMeleeEffects : null;
    public float MeleeDamageMultiplier => IsDepleted ? _spentMeleeDamageMultiplier : 1f;
    public float MeleeCooldownMultiplier => IsDepleted ? _spentMeleeCooldownMultiplier : 1f;

    private int BeltSize => Mathf.Max(1, _grenadesPerBelt);

    public event Action ReleaseRequested;
    public event Action ThrowFinished;
    public event Action ReloadFinished;

    [Inject]
    public void Construct(IEffectApplicationService effects) => _effects = effects;

    private void Awake()
    {
        _remainingGrenades = BeltSize;
        _ownerColliders = GetComponentsInChildren<Collider>();
        if (_beltWeakPoint != null)
            _beltWeakPoint.ThresholdReached += DetonateBelt;
        if (!IsReady)
            Debug.LogError("Borzoi grenade launcher is missing a prefab, animator, throw data, or launch point.", this);
    }

    private void OnEnable()
    {
        if (_animator != null)
            _animator.SetBool(GrenadesSpent, IsDepleted);
    }

    private void OnDestroy()
    {
        if (_beltWeakPoint != null)
            _beltWeakPoint.ThresholdReached -= DetonateBelt;
    }

    // Animation Events on the throw and reload clips.
    public void ReleaseGrenade() => ReleaseRequested?.Invoke();
    public void FinishGrenadeThrow() => ThrowFinished?.Invoke();
    public void FinishGrenadeReload() => ReloadFinished?.Invoke();

    public bool IsTooClose(Vector3 target) =>
        (target - transform.position).sqrMagnitude < _minRange * _minRange;

    public bool IsInRange(Vector3 target)
    {
        float distanceSquared = (target - transform.position).sqrMagnitude;
        return distanceSquared >= _minRange * _minRange && distanceSquared <= _maxRange * _maxRange;
    }

    public bool CanPlanThrow(Vector3 target) => TryPlanThrow(target, out _, out _);

    private bool TryPlanThrow(Vector3 target, out BaseGrеnadeObject prefab, out Vector3 velocity)
    {
        prefab = _grenadePrefab;
        velocity = default;
        if (!HasGrenades || !IsInRange(target))
            return false;

        int flashCount = Mathf.Clamp(_flashGrenadesPerBelt, 0, BeltSize - 1);
        bool isFlash = _flashGrenadePrefab != null && _remainingGrenades > BeltSize - flashCount;
        if (isFlash)
            prefab = _flashGrenadePrefab;

        float flightTime = Mathf.Clamp(_flightTime, 0.1f, Mathf.Max(0.1f, _throwData.GrenadeTimer - 0.1f));
        return BorzoiGrenadeAim.TryFindVelocity(_launchPoint.position, target, prefab.ExplosionRadius,
            flightTime, transform, isFlash, out velocity);
    }

    public bool TryThrow(Vector3 target)
    {
        if (!TryPlanThrow(target, out var prefab, out Vector3 velocity))
            return false;

        Vector3 origin = _launchPoint.position;
        var grenade = SpawnGrenade(prefab, origin, _launchPoint.rotation);
        if (!grenade.TryGetComponent(out Rigidbody body))
        {
            Debug.LogError("Borzoi grenade prefab needs a Rigidbody.", prefab);
            Destroy(grenade.gameObject);
            return false;
        }

        grenade.gameObject.AddComponent<BorzoiGrenadeFlight>().Launch(body, _ownerColliders, velocity);
        grenade.ActivesionGranade(_throwData, false);
        if (_throwData.ThrowGrendeSfx.Guid != Guid.Empty)
            SoundUtils3D.Play(grenade.gameObject, _throwData.ThrowGrendeSfx);

        _remainingGrenades--;
        if (IsDepleted)
            _animator.SetBool(GrenadesSpent, true);
        return true;
    }

    public void CompleteReload()
    {
        if (!NeedsReload)
            return;

        _hasReloaded = true;
        _remainingGrenades = BeltSize;
        if (_beltWeakPoint != null)
            _beltWeakPoint.gameObject.SetActive(false);
    }

    private void DetonateBelt()
    {
        if (_hasReloaded || _grenadePrefab == null)
            return;

        _remainingGrenades = 0;
        Transform belt = _beltWeakPoint.transform;
        belt.gameObject.SetActive(false);
        var grenade = SpawnGrenade(_grenadePrefab, belt.position, belt.rotation);
        grenade.Explosion(GetComponent<CapsuleCollider>());
    }

    private BaseGrеnadeObject SpawnGrenade(BaseGrеnadeObject prefab, Vector3 position, Quaternion rotation)
    {
        var grenade = Instantiate(prefab, position, rotation);
        if (grenade.gameObject.scene != gameObject.scene)
            SceneManager.MoveGameObjectToScene(grenade.gameObject, gameObject.scene);
        grenade.Construct(_effects);
        return grenade;
    }
}
