using System.Collections;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.EffectSystem.Effects;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class TestSlowEffect : MonoBehaviour
{
    [Header("Scope")]
    [SerializeField] private LifetimeScope _playerScopeOverride;

    [Inject] private IEffectManager _effectManager;
    [Inject] private PlayerMovementController _movementController;

    [Header("Slow Settings")]
    [SerializeField, Range(0.01f, 1f)] private float _multiplier = 0.4f;

    [SerializeField] private bool _unique = false;

    public bool ApplySlow;

    private SpeedMultiplierEffect _activeEffect;
    private bool _wasApplied;
    private bool _resolving;

    private void Start()
    {
        if (_effectManager == null || _movementController == null)
            StartCoroutine(ResolveWhenContainerReady());
    }

    private IEnumerator ResolveWhenContainerReady()
    {
        if (_resolving)
            yield break;

        _resolving = true;

        LifetimeScope scope =
            _playerScopeOverride != null
                ? _playerScopeOverride
                : GetComponentInParent<LifetimeScope>();

        if (scope == null)
            scope = LifetimeScope.Find<LifetimeScope>(gameObject.scene);

        if (scope == null)
        {
            Debug.LogError("[TestSlowEffect] Не найден LifetimeScope.");
            _resolving = false;
            yield break;
        }

        while (scope.Container == null)
            yield return null;

        var container = scope.Container;

        try
        {
            _effectManager ??= container.Resolve<IEffectManager>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TestSlowEffect] Не удалось Resolve<IEffectManager>: {e.Message}");
        }

        try
        {
            _movementController ??= container.Resolve<PlayerMovementController>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TestSlowEffect] Не удалось Resolve<PlayerMovementController>: {e.Message}");
        }

        _resolving = false;
    }

    private void Update()
    {
        if (_effectManager == null || _movementController == null)
            return;

        if (ApplySlow && !_wasApplied)
        {
            float duration = _unique ? 999999f : float.MaxValue;

            _activeEffect = new SpeedMultiplierEffect(
                _movementController,
                duration,
                _multiplier,
                SpeedStackPolicy.Strongest);

            _effectManager.ApplyEffect(_activeEffect);

            _wasApplied = true;

            Debug.Log($"[TestSlowEffect] Замедление ON x{_multiplier}");
        }
        else if (!ApplySlow && _wasApplied)
        {
            _activeEffect?.Dispose();
            _activeEffect = null;
            _wasApplied = false;

            Debug.Log("[TestSlowEffect] Замедление OFF");
        }
    }

    private void OnDisable()
    {
        if (!_wasApplied)
            return;

        _activeEffect?.Dispose();
        _activeEffect = null;
        _wasApplied = false;
    }
}