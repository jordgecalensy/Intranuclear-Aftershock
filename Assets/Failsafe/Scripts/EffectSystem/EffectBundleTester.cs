using System;
using System.Collections;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class EffectBundleTester : MonoBehaviour
{
    private enum TargetMode
    {
        Raycast = 0,
        DirectTarget = 1
    }

    [Header("VContainer")]
    [Tooltip("Можно указать GameSceneLifetimeScope. Если пусто — попробует найти сам.")]
    [SerializeField] private LifetimeScope _scopeOverride;

    [Header("Target Mode")]
    [SerializeField] private TargetMode _targetMode = TargetMode.Raycast;

    [Header("Raycast Target")]
    [SerializeField] private Camera _camera;
    [SerializeField] private float _range = 100f;
    [SerializeField] private LayerMask _hitMask = ~0;

    [Header("Direct Target")]
    [Tooltip("Сюда укажи Player root. Например объект игрока с PlayerController / CharacterController.")]
    [SerializeField] private GameObject _directTargetRoot;

    [Tooltip("Можно указать конкретный collider игрока. Если пусто — найдёт сам внутри Direct Target Root.")]
    [SerializeField] private Collider _directTargetCollider;

    [Tooltip("Если Direct Target Root пустой, попробует найти объект с тегом Player.")]
    [SerializeField] private bool _tryFindPlayerByTag = true;

    [Header("Effect Context")]
    [SerializeField] private float _contextPower = 1f;

    [Header("Bundles")]
    [SerializeField] private EffectBundle _bundle1;
    [SerializeField] private EffectBundle _bundle2;
    [SerializeField] private EffectBundle _bundle3;
    [SerializeField] private EffectBundle _bundle4;

    [Header("Keys")]
    [SerializeField] private KeyCode _bundle1Key = KeyCode.Alpha1;
    [SerializeField] private KeyCode _bundle2Key = KeyCode.Alpha2;
    [SerializeField] private KeyCode _bundle3Key = KeyCode.Alpha3;
    [SerializeField] private KeyCode _bundle4Key = KeyCode.Alpha4;

    [Header("Debug")]
    [SerializeField] private bool _logHit = true;

    private IEffectApplicationService _effectApplicationService;
    private bool _resolving;

    [Inject]
    public void Construct(IEffectApplicationService effectApplicationService)
    {
        _effectApplicationService = effectApplicationService;
    }

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Start()
    {
        if (_effectApplicationService == null)
            StartCoroutine(ResolveWhenContainerReady());
    }

    private IEnumerator ResolveWhenContainerReady()
    {
        if (_resolving)
            yield break;

        _resolving = true;

        LifetimeScope scope =
            _scopeOverride ??
            GetComponentInParent<LifetimeScope>() ??
            LifetimeScope.Find<LifetimeScope>(gameObject.scene);

        if (scope == null)
        {
            EffectLog.Error(EffectLog.Bundle,
                "[EffectBundleTester] LifetimeScope не найден. Укажи GameSceneLifetimeScope в поле Scope Override.",
                this);

            _resolving = false;
            yield break;
        }

        while (scope.Container == null)
            yield return null;

        try
        {
            _effectApplicationService = scope.Container.Resolve<IEffectApplicationService>();
        }
        catch (Exception e)
        {
            EffectLog.Error(EffectLog.Bundle,
                $"[EffectBundleTester] Не удалось Resolve<IEffectApplicationService>. Проверь регистрацию EffectApplicationService в GameSceneLifetimeScope. {e.Message}",
                this);
        }

        _resolving = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(_bundle1Key))
            ApplyBundle(_bundle1, "Bundle 1");

        if (Input.GetKeyDown(_bundle2Key))
            ApplyBundle(_bundle2, "Bundle 2");

        if (Input.GetKeyDown(_bundle3Key))
            ApplyBundle(_bundle3, "Bundle 3");

        if (Input.GetKeyDown(_bundle4Key))
            ApplyBundle(_bundle4, "Bundle 4");
    }

    private void ApplyBundle(EffectBundle bundle, string label)
    {
        if (bundle == null)
        {
            EffectLog.Warning(EffectLog.Bundle, $"[EffectBundleTester] {label} is not assigned.", this);
            return;
        }

        if (_effectApplicationService == null)
        {
            EffectLog.Error(EffectLog.Bundle,
                "[EffectBundleTester] IEffectApplicationService ещё не найден. Проверь Scope Override / GameSceneLifetimeScope.",
                this);

            return;
        }

        switch (_targetMode)
        {
            case TargetMode.Raycast:
                ApplyByRaycast(bundle, label);
                break;

            case TargetMode.DirectTarget:
                ApplyToDirectTarget(bundle, label);
                break;

            default:
                EffectLog.Error(EffectLog.Bundle, $"[EffectBundleTester] Unknown target mode: {_targetMode}", this);
                break;
        }
    }

    private void ApplyByRaycast(EffectBundle bundle, string label)
    {
        if (_camera == null)
        {
            EffectLog.Error(EffectLog.Bundle, "[EffectBundleTester] Camera is not assigned.", this);
            return;
        }

        Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, _range, _hitMask))
        {
            EffectLog.Info(EffectLog.Bundle, $"[EffectBundleTester] {label}: no hit.", this);
            return;
        }

        Vector3 direction = hit.point - ray.origin;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = ray.direction;

        direction.Normalize();

        var context = new EffectContext(
            gameObject,
            hit.collider,
            hit.point,
            hit.normal,
            direction,
            _contextPower);

        _effectApplicationService.Apply(bundle, context);

        if (_logHit)
        {
            EffectLog.Info(EffectLog.Bundle,
                $"[EffectBundleTester] Applied {label} by raycast to {hit.collider.name} at {hit.point}",
                hit.collider);
        }
    }

    private void ApplyToDirectTarget(EffectBundle bundle, string label)
    {
        Collider targetCollider = ResolveDirectTargetCollider();

        if (targetCollider == null)
        {
            EffectLog.Error(EffectLog.Bundle,
                "[EffectBundleTester] Direct Target Collider не найден. Укажи Direct Target Root игрока или конкретный collider игрока.",
                this);

            return;
        }

        Vector3 point = targetCollider.bounds.center;
        Vector3 normal = Vector3.up;

        Vector3 direction = point - transform.position;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        direction.Normalize();

        var context = new EffectContext(
            gameObject,
            targetCollider,
            point,
            normal,
            direction,
            _contextPower);

        _effectApplicationService.Apply(bundle, context);

        if (_logHit)
        {
            EffectLog.Info(EffectLog.Bundle,
                $"[EffectBundleTester] Applied {label} directly to {targetCollider.name}",
                targetCollider);
        }
    }

    private Collider ResolveDirectTargetCollider()
    {
        if (_directTargetCollider != null)
            return _directTargetCollider;

        if (_directTargetRoot == null && _tryFindPlayerByTag)
            TryFindPlayerByTag();

        if (_directTargetRoot == null)
            return null;

        Collider collider = FindBestCollider(_directTargetRoot);

        if (collider != null)
            _directTargetCollider = collider;

        return _directTargetCollider;
    }

    private void TryFindPlayerByTag()
    {
        try
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                _directTargetRoot = player;
        }
        catch
        {
            // Если тега Player нет в проекте, Unity кинет exception.
            // Для тестера это не критично.
        }
    }

    private static Collider FindBestCollider(GameObject root)
    {
        if (root == null)
            return null;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        if (colliders == null || colliders.Length == 0)
            return null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null)
                continue;

            if (!collider.enabled)
                continue;

            if (collider.isTrigger)
                continue;

            return collider;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null)
                continue;

            if (!collider.enabled)
                continue;

            return collider;
        }

        return colliders[0];
    }
}