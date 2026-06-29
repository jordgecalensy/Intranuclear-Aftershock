using System.Collections;
using System.Collections.Generic;
using Failsafe.PlayerMovements;
using FMODUnity;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class Stasisable : MonoBehaviour
{
    [Header("Visual / Audio")]
    [SerializeField] private EventReference _stasisEnd;
    [SerializeField] private Material _stasisMaterial;

    [Header("Behaviour")]
    [Tooltip("Замораживать Rigidbody, если он есть.")]
    [SerializeField] private bool _freezeRigidbody = true;

    [Tooltip("Отключать Enemy через Enemy.DisableState(duration), если Enemy найден.")]
    [SerializeField] private bool _disableEnemyState = true;

    [Tooltip("Останавливать DamageObstacle через DamageObstacle.SetStasis.")]
    [SerializeField] private bool _freezeDamageObstacle = true;

    [Tooltip("Если объект находится в PlayerLifetimeScope, блокировать управление игроком через PlayerControlBlocker.")]
    [SerializeField] private bool _blockPlayerControls = true;

    [Header("Player Control Blocks")]
    [SerializeField] private PlayerControlBlock _playerBlocks =
        PlayerControlBlock.Movement |
        PlayerControlBlock.Look |
        PlayerControlBlock.Jump |
        PlayerControlBlock.Crouch |
        PlayerControlBlock.Sprint |
        PlayerControlBlock.Interaction |
        PlayerControlBlock.Shooting |
        PlayerControlBlock.Inventory |
        PlayerControlBlock.ItemUse |
        PlayerControlBlock.Visor;

    private Rigidbody _rb;
    private Renderer[] _renderers;
    private Enemy _enemy;
    private DamageObstacle _damageObstacle;
    private PlayerControlBlocker _playerControlBlocker;

    private bool _isInStasis;
    private bool _restoreVelocityOnExit;

    private bool _storedIsKinematic;
    private bool _storedUseGravity;
    private RigidbodyConstraints _storedConstraints;
    private Vector3 _storedVelocity;
    private Vector3 _storedAngularVelocity;

    private Coroutine _legacyStasisCoroutine;

    private readonly Dictionary<Renderer, Material[]> _originalMaterials = new();

    private void Awake()
    {
        ResolveComponents();
    }

    private void Start()
    {
        ResolveComponents();
    }

    public void ApplyStasis(float duration, bool restoreVelocityOnExit, GameObject source)
    {
        duration = Mathf.Max(0f, duration);

        ResolveComponents();

        _restoreVelocityOnExit = _restoreVelocityOnExit || restoreVelocityOnExit;

        if (_disableEnemyState && _enemy != null)
            _enemy.DisableState(duration);

        if (_isInStasis)
            return;

        _isInStasis = true;

        ApplyStasisMaterial();
        ApplyRigidbodyStasis();
        ApplyDamageObstacleStasis();
        ApplyPlayerControlBlocks();
    }

    public void ClearStasis(bool restoreVelocityOnExit, GameObject source)
    {
        bool shouldRestoreVelocity = _restoreVelocityOnExit || restoreVelocityOnExit;

        _restoreVelocityOnExit = false;

        if (!_isInStasis)
            return;

        _isInStasis = false;

        PlayStasisEndSound();

        ClearPlayerControlBlocks();
        ClearDamageObstacleStasis();
        ClearRigidbodyStasis(shouldRestoreVelocity);
        RemoveStasisMaterial();
    }

    public void StasisHit(float duration, bool defaultMode)
    {
        if (_legacyStasisCoroutine != null)
            StopCoroutine(_legacyStasisCoroutine);

        bool restoreVelocityOnExit = !defaultMode;

        ApplyStasis(
            duration,
            restoreVelocityOnExit,
            source: null);

        _legacyStasisCoroutine = StartCoroutine(
            LegacyStasisRoutine(duration, restoreVelocityOnExit));
    }

    private IEnumerator LegacyStasisRoutine(float duration, bool restoreVelocityOnExit)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        ClearStasis(
            restoreVelocityOnExit,
            source: null);

        _legacyStasisCoroutine = null;
    }

    private void ResolveComponents()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();

        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(true);

        if (_enemy == null)
        {
            _enemy = GetComponent<Enemy>() ??
                     GetComponentInParent<Enemy>() ??
                     GetComponentInChildren<Enemy>(true);
        }

        if (_damageObstacle == null)
        {
            _damageObstacle = GetComponent<DamageObstacle>() ??
                              GetComponentInParent<DamageObstacle>() ??
                              GetComponentInChildren<DamageObstacle>(true);
        }

        if (_playerControlBlocker == null)
            TryResolvePlayerControlBlocker();
    }

    private void TryResolvePlayerControlBlocker()
    {
        LifetimeScope scope = GetComponentInParent<LifetimeScope>();

        if (scope == null)
            return;

        if (scope.Container == null)
            return;

        try
        {
            _playerControlBlocker = scope.Container.Resolve<PlayerControlBlocker>();
        }
        catch
        {
            _playerControlBlocker = null;
        }
    }

    private void ApplyRigidbodyStasis()
    {
        if (!_freezeRigidbody)
            return;

        if (_rb == null)
            return;

        _storedIsKinematic = _rb.isKinematic;
        _storedUseGravity = _rb.useGravity;
        _storedConstraints = _rb.constraints;

#if UNITY_6000_0_OR_NEWER
        _storedVelocity = _rb.linearVelocity;
        _rb.linearVelocity = Vector3.zero;
#else
        _storedVelocity = _rb.velocity;
        _rb.velocity = Vector3.zero;
#endif

        _storedAngularVelocity = _rb.angularVelocity;
        _rb.angularVelocity = Vector3.zero;

        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    private void ClearRigidbodyStasis(bool restoreVelocityOnExit)
    {
        if (!_freezeRigidbody)
            return;

        if (_rb == null)
            return;

        _rb.isKinematic = _storedIsKinematic;
        _rb.useGravity = _storedUseGravity;
        _rb.constraints = _storedConstraints;

        if (restoreVelocityOnExit && !_storedIsKinematic)
        {
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = _storedVelocity;
#else
            _rb.velocity = _storedVelocity;
#endif
            _rb.angularVelocity = _storedAngularVelocity;
        }
    }

    private void ApplyDamageObstacleStasis()
    {
        if (!_freezeDamageObstacle)
            return;

        if (_damageObstacle == null)
            return;

        _damageObstacle.SetStasis(true);
    }

    private void ClearDamageObstacleStasis()
    {
        if (!_freezeDamageObstacle)
            return;

        if (_damageObstacle == null)
            return;

        _damageObstacle.SetStasis(false);
    }

    private void ApplyPlayerControlBlocks()
    {
        if (!_blockPlayerControls)
            return;

        if (_playerControlBlocker == null)
            return;

        _playerControlBlocker.AddLock(
            PlayerControlLockIds.Stasis,
            _playerBlocks);
    }

    private void ClearPlayerControlBlocks()
    {
        if (_playerControlBlocker == null)
            return;

        _playerControlBlocker.RemoveLock(PlayerControlLockIds.Stasis);
    }

    private void ApplyStasisMaterial()
    {
        if (_stasisMaterial == null)
            return;

        if (_renderers == null)
            return;

        foreach (Renderer renderer in _renderers)
        {
            if (renderer == null)
                continue;

            if (_originalMaterials.ContainsKey(renderer))
                continue;

            Material[] original = renderer.sharedMaterials;
            _originalMaterials[renderer] = original;

            Material[] withStasis = new Material[original.Length + 1];

            for (int i = 0; i < original.Length; i++)
                withStasis[i] = original[i];

            withStasis[withStasis.Length - 1] = _stasisMaterial;

            renderer.sharedMaterials = withStasis;
        }
    }

    private void RemoveStasisMaterial()
    {
        foreach (KeyValuePair<Renderer, Material[]> pair in _originalMaterials)
        {
            if (pair.Key == null)
                continue;

            pair.Key.sharedMaterials = pair.Value;
        }

        _originalMaterials.Clear();
    }

    private void PlayStasisEndSound()
    {
        if (_stasisEnd.IsNull)
            return;

        SoundUtils3D.Play(gameObject, _stasisEnd);
    }

    private void OnDisable()
    {
        if (_legacyStasisCoroutine != null)
        {
            StopCoroutine(_legacyStasisCoroutine);
            _legacyStasisCoroutine = null;
        }

        if (!_isInStasis)
            return;

        _restoreVelocityOnExit = false;
        _isInStasis = false;

        ClearPlayerControlBlocks();
        ClearDamageObstacleStasis();
        ClearRigidbodyStasis(false);
        RemoveStasisMaterial();
    }
}