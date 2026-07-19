using Failsafe.Scripts.EffectSystem;
using FMODUnity;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Items
{
    public enum EffectItemTargetMode
    {
        Self,
        Raycast
    }

    [DisallowMultipleComponent]
    public class EffectItemUsable : MonoBehaviour, IUsable
    {
        [Header("Target")]
        [SerializeField] private EffectItemTargetMode _targetMode = EffectItemTargetMode.Self;

        [Tooltip("Для Self-эффектов. Если пусто, скрипт попробует найти игрока по родителю.")]
        [SerializeField] private Transform _selfTargetRoot;

        [Tooltip("Для Self-эффектов. Если пусто, скрипт попробует найти CharacterController/Collider на Self Target Root.")]
        [SerializeField] private Collider _selfTargetCollider;

        [Tooltip("Для Raycast-режима. Если пусто, используется Camera.main или HandlePoint/transform предмета.")]
        [SerializeField] private Transform _aimOrigin;

        [Header("Effect Context")]
        [SerializeField] private float _power = 1f;

        [Tooltip("Source в EffectContext. Если пусто, source = item.gameObject.")]
        [SerializeField] private GameObject _sourceOverride;

        [Header("Use Result")]
        [SerializeField] private UsageType _usageType = UsageType.ClickToUse;
        [SerializeField] private ItemState _itemStateAfterSuccessfulUse = ItemState.Hold;

        [Tooltip("Если ItemData.Type = Consumable, успешное использование вернёт ItemUseResult.Consumed.")]
        [SerializeField] private bool _consumeConsumableOnSuccessfulUse = true;

        [Header("Raycast")]
        [Tooltip("Если false, промах по Raycast не тратит заряд и не применяет эффекты.")]
        [SerializeField] private bool _allowUseWithoutRaycastHit = false;

        [Header("Debug")]
        [SerializeField] private bool _log;

        private Item _item;
        private IEffectApplicationService _effects;
        private bool _alternativeModeActive;

        [Inject]
        public void Construct(IEffectApplicationService effects)
        {
            _effects = effects;
        }

        private void Awake()
        {
            if (_item == null)
                _item = GetComponent<Item>();
        }

        public void ParseItem(Item item_object)
        {
            _item = item_object;

            if (_item == null)
                _item = GetComponent<Item>();
        }

        public ItemUseResult Use()
        {
            if (!ResolveItem())
                return FailedResult();

            if (_item.ItemData == null)
            {
                if (_log)
                    Debug.LogWarning("[EffectItemUsable] ItemData is null.", this);

                return FailedResult();
            }

            EffectBundle bundle = GetSelectedBundle();

            if (bundle == null)
            {
                if (_log)
                    Debug.LogWarning($"[EffectItemUsable] {name}: selected EffectBundle is null.", this);

                PlayOneShot(_item.ItemData.EmptyUseSFX);
                return FailedResult();
            }

            if (!TryBuildContext(out EffectContext context))
            {
                if (_log)
                    Debug.LogWarning($"[EffectItemUsable] {name}: cannot build EffectContext.", this);

                PlayOneShot(_item.ItemData.EmptyUseSFX);
                return FailedResult();
            }

            if (!ResolveEffectsIfNeeded())
            {
                if (_log)
                    Debug.LogWarning($"[EffectItemUsable] {name}: IEffectApplicationService not found.", this);

                PlayOneShot(_item.ItemData.EmptyUseSFX);
                return FailedResult();
            }

            if (!_item.TryUseEnergy())
            {
                if (_log)
                    Debug.Log($"[EffectItemUsable] {name}: not enough energy.", this);

                PlayOneShot(_item.ItemData.EmptyUseSFX);
                return FailedResult();
            }

            _effects.Apply(bundle, context);

            PlayOneShot(_item.ItemData.UseSFX);

            return SuccessfulResult();
        }

        public void AltMode()
        {
            _alternativeModeActive = !_alternativeModeActive;

            if (ResolveItem() && _item.ItemData != null)
                PlayOneShot(_item.ItemData.ModeSwitchSFX);

        }

        public void GetItemUseDelays(out float startDelay, out float useDelay)
        {
            startDelay = 0f;
            useDelay = 0.2f;

            if (!ResolveItem())
                return;

            if (_item.ItemData == null)
                return;

            startDelay = Mathf.Max(0f, _item.ItemData.StartUseDelay);
            useDelay = Mathf.Max(0f, _item.ItemData.UseDelay);
        }

        private bool ResolveItem()
        {
            if (_item != null)
                return true;

            _item = GetComponent<Item>();

            return _item != null;
        }

        private EffectBundle GetSelectedBundle()
        {
            if (_item == null || _item.ItemData == null)
                return null;

            if (_alternativeModeActive && _item.ItemData.AlternativeModeEffects != null)
                return _item.ItemData.AlternativeModeEffects;

            return _item.ItemData.DefaultModeEffects;
        }

        private bool TryBuildContext(out EffectContext context)
        {
            context = default;

            return _targetMode switch
            {
                EffectItemTargetMode.Self => TryBuildSelfContext(out context),
                EffectItemTargetMode.Raycast => TryBuildRaycastContext(out context),
                _ => false
            };
        }

        private bool TryBuildSelfContext(out EffectContext context)
        {
            context = default;

            Transform targetRoot = ResolveSelfTargetRoot();

            if (targetRoot == null)
                return false;

            Collider targetCollider = ResolveSelfTargetCollider(targetRoot);

            if (targetCollider == null)
                return false;

            GameObject source = ResolveSourceObject();

            Vector3 point = targetCollider.bounds.center;
            Vector3 direction = targetRoot.position - source.transform.position;

            if (direction.sqrMagnitude < 0.0001f)
                direction = targetRoot.forward;

            direction.Normalize();

            context = new EffectContext(
                source,
                targetCollider,
                point,
                Vector3.up,
                direction,
                _power);

            return true;
        }

        private bool TryBuildRaycastContext(out EffectContext context)
        {
            context = default;

            if (_item == null || _item.ItemData == null)
                return false;

            Transform origin = ResolveAimOrigin();

            if (origin == null)
                return false;

            float range = Mathf.Max(0.01f, _item.ItemData.UseRange);
            LayerMask mask = _item.ItemData.UseMask;

            Ray ray = new Ray(origin.position, origin.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                context = new EffectContext(
                    ResolveSourceObject(),
                    hit.collider,
                    hit.point,
                    hit.normal,
                    ray.direction,
                    _power);

                return true;
            }

            if (!_allowUseWithoutRaycastHit)
                return false;

            Vector3 point = origin.position + origin.forward * range;

            context = new EffectContext(
                ResolveSourceObject(),
                null,
                point,
                Vector3.up,
                origin.forward,
                _power);

            return true;
        }

        private Transform ResolveSelfTargetRoot()
        {
            if (_selfTargetRoot != null)
                return _selfTargetRoot;

            Transform current = transform;

            while (current != null)
            {
                if (current.CompareTag("Player"))
                    return current;

                if (current.GetComponent<CharacterController>() != null)
                    return current;

                current = current.parent;
            }

            if (_item != null && _item.transform.root != null)
                return _item.transform.root;

            return transform.root;
        }

        private Collider ResolveSelfTargetCollider(Transform targetRoot)
        {
            if (_selfTargetCollider != null)
                return _selfTargetCollider;

            if (targetRoot == null)
                return null;

            CharacterController characterController =
                targetRoot.GetComponent<CharacterController>() ??
                targetRoot.GetComponentInChildren<CharacterController>(true);

            if (characterController != null)
                return characterController;

            Collider[] colliders = targetRoot.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];

                if (collider == null)
                    continue;

                if (_item != null && collider.transform.IsChildOf(_item.transform))
                    continue;

                if (!collider.enabled)
                    continue;

                return collider;
            }

            return colliders.Length > 0 ? colliders[0] : null;
        }

        private Transform ResolveAimOrigin()
        {
            if (_aimOrigin != null)
                return _aimOrigin;

            if (Camera.main != null)
                return Camera.main.transform;

            if (_item != null && _item.HandlePoint != null)
                return _item.HandlePoint;

            return transform;
        }

        private GameObject ResolveSourceObject()
        {
            if (_sourceOverride != null)
                return _sourceOverride;

            if (_item != null)
                return _item.gameObject;

            return gameObject;
        }

        private bool ResolveEffectsIfNeeded()
        {
            if (_effects != null)
                return true;

            LifetimeScope scope = GetComponentInParent<LifetimeScope>();

            if (scope == null && gameObject.scene.IsValid())
                scope = LifetimeScope.Find<LifetimeScope>(gameObject.scene);

            if (scope == null || scope.Container == null)
                return false;

            try
            {
                _effects = scope.Container.Resolve<IEffectApplicationService>();
            }
            catch
            {
                _effects = null;
            }

            return _effects != null;
        }

        private ItemUseResult SuccessfulResult()
        {
            if (_item != null &&
                _item.ItemData != null &&
                _consumeConsumableOnSuccessfulUse &&
                _item.ItemData.Type == ItemType.Consumable)
            {
                return ItemUseResult.Consumed;
            }

            return new ItemUseResult
            {
                UsageType = _usageType,
                ItemStateAfterUse = _itemStateAfterSuccessfulUse
            };
        }

        private ItemUseResult FailedResult()
        {
            return new ItemUseResult
            {
                UsageType = _usageType,
                ItemStateAfterUse = ItemState.Hold
            };
        }

        private void PlayOneShot(EventReference eventReference)
        {
            if (eventReference.IsNull)
                return;

            RuntimeManager.PlayOneShot(
                eventReference,
                transform.position);
        }
    }
}
