using System.Collections;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.EffectSystem.Effects;
using Failsafe.PlayerMovements.Controllers;

public class TestSlowEffect : MonoBehaviour
{
    [Header("Scope (опционально)")]
    [Tooltip("Укажи PlayerLifetimeScope игрока. Если пусто — возьмём у родителя или корневой скоуп сцены.")]
    [SerializeField] private LifetimeScope _playerScopeOverride;

    // если объект под нужным скоупом — эти поля могут заинжектиться автоматически
    [Inject] private IEffectManager _effectManager;
    [Inject] private PlayerMovementController _movementController;

    [Header("Slow Settings")]
    [SerializeField, Range(0.1f, 1f)] private float _multiplier = 0.4f;

    [Tooltip("Активировать замедление игрока")]
    public bool ApplySlow;

    private SlowMovementEffect _activeEffect;
    private bool _wasApplied;
    private bool _resolving;

    private void Start()
    {
        // Если DI не сработал (скрипт вне скоупа игрока) — пробуем вручную.
        if (_effectManager == null || _movementController == null)
            StartCoroutine(ResolveWhenContainerReady());
    }

    private IEnumerator ResolveWhenContainerReady()
    {
        if (_resolving) yield break;
        _resolving = true;

        LifetimeScope scope = _playerScopeOverride
                              ?? GetComponentInParent<LifetimeScope>()                    // скоуп родителя (обычно PlayerLifetimeScope)
                              ?? LifetimeScope.Find<LifetimeScope>(gameObject.scene);     // корневой скоуп сцены

        if (scope == null)
        {
            Debug.LogError("[TestSlowEffect] Не найден LifetimeScope. Повесь скрипт под PlayerLifetimeScope или укажи его в поле.");
            _resolving = false;
            yield break;
        }

        // Ждём, пока контейнер будет построен в Awake() своего скоупа
        while (scope.Container == null)
            yield return null;

        var c = scope.Container;

        try { _effectManager ??= c.Resolve<IEffectManager>(); }
        catch (System.Exception e)
        {
            Debug.LogError($"[TestSlowEffect] Resolve<IEffectManager> не удался. Проверь регистрацию EffectManager как EntryPoint в PlayerLifetimeScope. {e.Message}");
        }

        try { _movementController ??= c.Resolve<PlayerMovementController>(); }
        catch (System.Exception e)
        {
            Debug.LogError($"[TestSlowEffect] Resolve<PlayerMovementController> не удался. Убедись, что в PlayerLifetimeScope есть builder.Register<PlayerMovementController>(Lifetime.Scoped). {e.Message}");
        }

        _resolving = false;
    }

    private void Update()
    {
        if (_effectManager == null || _movementController == null) return;

        if (ApplySlow && !_wasApplied)
        {
            _activeEffect = new SlowMovementEffect(_movementController, float.MaxValue, _multiplier, unique: false);
            _effectManager.ApplyEffect(_activeEffect);
            _wasApplied = true;
            Debug.Log($"[TestSlowEffect] Замедление ON (x{_multiplier})");
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
        if (_wasApplied) { _activeEffect?.Dispose(); _activeEffect = null; _wasApplied = false; }
    }
}