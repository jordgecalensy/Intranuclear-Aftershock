using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class WeaponController : MonoBehaviour
{
    [Header("Setup")]
    public Transform firePoint;
    public WeaponStrategy weaponStrategy;

    [Header("Events")]
    public UnityEvent OnReloadStart;
    public UnityEvent OnReloadComplete;
    public UnityEvent<int, int> OnAmmoChanged;

    private int _currentAmmo;
    private bool _isReloading;
    private float _nextFireTime;
    private Coroutine _reloadCoroutine;
    
    private Dictionary<string, object> _runtimeObjects = new Dictionary<string, object>();
    public bool IsReloading => _isReloading;
    public int CurrentAmmo => _currentAmmo;

    private void Start() => InitializeWeapon();

    public void InitializeWeapon()
    {
        if (weaponStrategy == null) return;
        if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);

        _currentAmmo = weaponStrategy.ammoConfig.maxAmmo;
        _isReloading = false;
        weaponStrategy.Initialize(this);
        
        UpdateAmmoUI();
    }

    public bool TryShoot(Vector3 target)
    {
        if (_isReloading) return false;

        // 1. Проверка обоймы: если пусто — идем на перезарядку
        if (_currentAmmo <= 0)
        {
            StartReload();
            return false;
        }

        // 2. Проверка скорострельности
        if (Time.time < _nextFireTime) return false; 

        // 3. Выстрел
        if (weaponStrategy.Fire(this, target))
        {
            _nextFireTime = Time.time + weaponStrategy.stats.fireRate;
            
            // Тратим патрон/заряд батареи ВСЕГДА
            _currentAmmo--;
            OnAmmoChanged?.Invoke(_currentAmmo, weaponStrategy.ammoConfig.maxAmmo);
            
            return true;
        }

        return false; 
    }

    public void StopShooting() => weaponStrategy?.StopFiring(this);

    public void StartReload()
    {
        if (!_isReloading && _currentAmmo < weaponStrategy.ammoConfig.maxAmmo)
        {
            _reloadCoroutine = StartCoroutine(ReloadRoutine());
        }
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
        if (weaponStrategy != null)
            OnAmmoChanged?.Invoke(_currentAmmo, weaponStrategy.ammoConfig.maxAmmo);
    }

    public T GetRuntimeObject<T>(string key) where T : class
    {
        if (_runtimeObjects.TryGetValue(key, out object val)) return val as T;
        return null;
    }
    public void SetRuntimeObject(string key, object obj) => _runtimeObjects[key] = obj;
    public void ClearRuntimeObject(string key) => _runtimeObjects.Remove(key);
}