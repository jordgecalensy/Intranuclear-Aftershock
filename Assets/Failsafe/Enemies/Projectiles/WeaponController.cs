using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class WeaponController : MonoBehaviour
{
    [Header("Setup")]
    public Transform firePoint;            // Откуда вылетает
    public WeaponStrategy weaponStrategy;  // Сюда перетаскиваем файл стратегии

    [Header("Events")]
    public UnityEvent OnReloadStart;       // Подпиши сюда анимацию перезарядки
    public UnityEvent OnReloadComplete;
    public UnityEvent<int, int> OnAmmoChanged; // Для UI

    // Состояние
    private int _currentAmmo;
    private bool _isReloading;
    private float _nextFireTime;
    private Coroutine _reloadCoroutine;
    
    // Хранилище временных объектов (чтобы не гадить в ScriptableObject)
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

    public void TryShoot(Vector3 targetPoint)
    {
        if (_isReloading || weaponStrategy == null) return;

        // Если патроны кончились — перезарядка
        if (_currentAmmo <= 0 && !weaponStrategy.ammoConfig.infiniteAmmo)
        {
            StartReload();
            weaponStrategy.StopFiring(this);
            return;
        }

        // Скорострельность
        if (Time.time < _nextFireTime && weaponStrategy.stats.fireRate > 0) return;

        // Выстрел через стратегию
        if (weaponStrategy.Fire(this, targetPoint))
        {
            // Тратим патроны (если это не лазер с continuous damage)
            if (weaponStrategy.stats.fireRate > 0 && !weaponStrategy.ammoConfig.infiniteAmmo)
            {
                _currentAmmo--;
                _nextFireTime = Time.time + weaponStrategy.stats.fireRate;
                UpdateAmmoUI();
            }
        }
    }

    public void StopShooting() => weaponStrategy?.StopFiring(this);

    public void StartReload()
    {
        if (!_isReloading && _currentAmmo < weaponStrategy.ammoConfig.maxAmmo)
            _reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        _isReloading = true;
        weaponStrategy.StopFiring(this); // Убираем лазер
        
        OnReloadStart?.Invoke(); // Включаем анимацию
        
        yield return new WaitForSeconds(weaponStrategy.ammoConfig.reloadTime);

        _currentAmmo = weaponStrategy.ammoConfig.maxAmmo;
        _isReloading = false;
        
        OnReloadComplete?.Invoke(); // Выключаем анимацию
        UpdateAmmoUI();
    }

    private void UpdateAmmoUI()
    {
        if (weaponStrategy != null)
            OnAmmoChanged?.Invoke(_currentAmmo, weaponStrategy.ammoConfig.maxAmmo);
    }

    // Утилиты для хранения данных сессии
    public T GetRuntimeObject<T>(string key) where T : class
    {
        if (_runtimeObjects.TryGetValue(key, out object val)) return val as T;
        return null;
    }
    public void SetRuntimeObject(string key, object obj) => _runtimeObjects[key] = obj;
    public void ClearRuntimeObject(string key) => _runtimeObjects.Remove(key);
}