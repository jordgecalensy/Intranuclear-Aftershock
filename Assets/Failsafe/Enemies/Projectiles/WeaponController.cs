using System.Collections;
using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using UnityEngine.Events;
using VContainer;

public class WeaponController : MonoBehaviour
{
    [Header("Setup")]
    public Transform firePoint;
    public WeaponStrategy weaponStrategy;

    [Header("Events")]
    public UnityEvent OnReloadStart;
    public UnityEvent OnReloadComplete;
    public UnityEvent<int, int> OnAmmoChanged;

    [Inject] private IEffectApplicationService _effects;

    private int _currentAmmo;
    private bool _isReloading;
    private float _nextFireTime;
    private Coroutine _reloadCoroutine;

    private readonly Dictionary<string, object> _runtimeObjects = new();

    public bool IsReloading => _isReloading;
    public int CurrentAmmo => _currentAmmo;
    public IEffectApplicationService Effects => _effects;

    private void Start()
    {
        InitializeWeapon();
    }

    public void InitializeWeapon()
    {
        if (weaponStrategy == null)
            return;

        if (_reloadCoroutine != null)
            StopCoroutine(_reloadCoroutine);

        if (weaponStrategy.ammoConfig == null)
        {
            Debug.LogError($"[{nameof(WeaponController)}] AmmoConfig is not assigned.", this);
            return;
        }

        _currentAmmo = weaponStrategy.ammoConfig.maxAmmo;
        _isReloading = false;

        weaponStrategy.Initialize(this);

        UpdateAmmoUI();
    }

    public bool TryShoot(Vector3 target)
    {
        if (_isReloading)
            return false;

        if (weaponStrategy == null)
            return false;

        if (weaponStrategy.ammoConfig == null)
        {
            Debug.LogError($"[{nameof(WeaponController)}] AmmoConfig is not assigned.", this);
            return false;
        }

        if (_currentAmmo <= 0)
        {
            StartReload();
            return false;
        }

        if (Time.time < _nextFireTime)
            return false;

        if (!weaponStrategy.Fire(this, target))
            return false;

        _nextFireTime = Time.time + weaponStrategy.stats.fireRate;

        _currentAmmo--;
        OnAmmoChanged?.Invoke(_currentAmmo, weaponStrategy.ammoConfig.maxAmmo);

        return true;
    }

    public void StopShooting()
    {
        weaponStrategy?.StopFiring(this);
    }

    public void StartReload()
    {
        if (weaponStrategy == null || weaponStrategy.ammoConfig == null)
            return;

        if (!_isReloading && _currentAmmo < weaponStrategy.ammoConfig.maxAmmo)
            _reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        _isReloading = true;

        weaponStrategy.StopFiring(this);
        OnReloadStart?.Invoke();

        yield return new WaitForSeconds(weaponStrategy.ammoConfig.reloadTime);

        _currentAmmo = weaponStrategy.ammoConfig.maxAmmo;
        _isReloading = false;

        OnReloadComplete?.Invoke();
        UpdateAmmoUI();
    }

    private void UpdateAmmoUI()
    {
        if (weaponStrategy != null && weaponStrategy.ammoConfig != null)
            OnAmmoChanged?.Invoke(_currentAmmo, weaponStrategy.ammoConfig.maxAmmo);
    }

    public T GetRuntimeObject<T>(string key) where T : class
    {
        if (_runtimeObjects.TryGetValue(key, out object value))
            return value as T;

        return null;
    }

    public T GetRuntimeValue<T>(string key)
    {
        if (_runtimeObjects.TryGetValue(key, out object value) && value is T typed)
            return typed;

        return default;
    }

    public void SetRuntimeObject(string key, object obj)
    {
        _runtimeObjects[key] = obj;
    }

    public void ClearRuntimeObject(string key)
    {
        _runtimeObjects.Remove(key);
    }
}