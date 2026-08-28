using UnityEngine;
using UnityEngine.UI;

namespace Failsafe.Inventory.Presentation
{
    [DisallowMultipleComponent]
    public sealed class InventoryQuickBarPresentationLayout3D : MonoBehaviour
    {
        [Header("Player HUD Layout")]
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private RectTransform _slotsRoot;
        [SerializeField] private string _itemPreviewChildPath = "ItemPreview";
        [SerializeField] private string _assignedStateChildPath = "Assigned";

        [Header("3D Preview")]
        [SerializeField, Range(64, 512)]
        private int _previewTextureHeight = 256;

        [SerializeField, Range(0f, 0.5f)]
        private float _slotPaddingRatio = 0.08f;

        [SerializeField] private float _modelDepthOffset;

        [Header("Animator")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _showTriggerName = "Show";
        [SerializeField] private string _hideTriggerName = "Hide";
        [SerializeField, Min(0f)] private float _autoHideDelay = 3.5f;

        [Header("Validation")]
        [SerializeField, Range(0f, 0.25f)]
        private float _maximumSlotAspectError = 0.08f;

        private RawImage[] _itemPreviewImages;
        private Transform[] _assignedStateRoots;
        private InventoryQuickBarPreviewStage3D _previewStage;
        private int _showTriggerHash;
        private int _hideTriggerHash;
        private float _hideAt;
        private bool _isPresentationEnabled;
        private bool _isShownOrShowing;
        private bool _animatorConfigurationChecked;
        private bool _animatorConfigurationValid;

        public bool TryValidate(int expectedSlotCount, out string error)
        {
            if (expectedSlotCount <= 0)
            {
                error = "Expected quick-slot count must be greater than zero.";
                return false;
            }

            if (_slotsRoot == null)
            {
                error = "Quick-bar slots root is not assigned.";
                return false;
            }

            if (!IsSameOrChildOf(
                    _slotsRoot,
                    GetVisualRoot().transform))
            {
                error = "Quick-bar slots root must be inside the visual root.";
                return false;
            }

            if (_slotsRoot.childCount < expectedSlotCount)
            {
                error = $"Quick-bar slots root must have at least " +
                        $"{expectedSlotCount} direct children, but it has " +
                        $"{_slotsRoot.childCount}.";
                return false;
            }

            return TryCacheSlots(expectedSlotCount, out error);
        }

        public void SetVisible(bool visible)
        {
            GameObject visualRoot = GetVisualRoot();

            if (visible)
            {
                _isPresentationEnabled = true;

                if (!visualRoot.activeSelf)
                    visualRoot.SetActive(true);

                _previewStage?.SetVisible(true);
                RequestReveal();
                return;
            }

            _isPresentationEnabled = false;
            _isShownOrShowing = false;
            ResetAnimatorTriggers();
            _previewStage?.SetVisible(false);

            if (visualRoot.activeSelf)
                visualRoot.SetActive(false);
        }

        public void RequestReveal()
        {
            if (!_isPresentationEnabled ||
                !TryResolveAnimator(out Animator animator))
            {
                return;
            }

            _hideAt = Time.time + _autoHideDelay;

            if (_isShownOrShowing)
                return;

            animator.ResetTrigger(_hideTriggerHash);
            animator.SetTrigger(_showTriggerHash);
            _isShownOrShowing = true;
        }

        private void Update()
        {
            if (!_isPresentationEnabled ||
                !_isShownOrShowing ||
                Time.time < _hideAt ||
                !TryResolveAnimator(out Animator animator))
            {
                return;
            }

            animator.ResetTrigger(_showTriggerHash);
            animator.SetTrigger(_hideTriggerHash);
            _isShownOrShowing = false;
        }

        private bool TryResolveAnimator(out Animator animator)
        {
            if (_animator == null)
                _animator = GetVisualRoot().GetComponent<Animator>();

            animator = _animator;

            if (animator == null || !animator.isActiveAndEnabled)
                return false;

            if (_animatorConfigurationChecked)
                return _animatorConfigurationValid;

            _showTriggerHash = Animator.StringToHash(
                _showTriggerName ?? string.Empty);

            _hideTriggerHash = Animator.StringToHash(
                _hideTriggerName ?? string.Empty);

            bool hasShowTrigger = false;
            bool hasHideTrigger = false;

            foreach (AnimatorControllerParameter parameter in
                     animator.parameters)
            {
                if (parameter.type !=
                    AnimatorControllerParameterType.Trigger)
                {
                    continue;
                }

                if (parameter.nameHash == _showTriggerHash)
                    hasShowTrigger = true;

                if (parameter.nameHash == _hideTriggerHash)
                    hasHideTrigger = true;
            }

            _animatorConfigurationValid =
                hasShowTrigger && hasHideTrigger;

            _animatorConfigurationChecked = true;

            if (!_animatorConfigurationValid)
            {
                Debug.LogWarning(
                    $"Quick-bar Animator must contain Trigger parameters " +
                    $"'{_showTriggerName}' and '{_hideTriggerName}'. " +
                    "The quick bar will remain static.",
                    this);
            }

            return _animatorConfigurationValid;
        }

        private void ResetAnimatorTriggers()
        {
            if (!TryResolveAnimator(out Animator animator))
                return;

            animator.ResetTrigger(_showTriggerHash);
            animator.ResetTrigger(_hideTriggerHash);
        }

        public bool TryAttachPresenterRoot(
            Transform presenterRoot,
            int expectedSlotCount,
            out string error)
        {
            if (presenterRoot == null)
            {
                error = "Quick-bar presenter root is null.";
                return false;
            }

            if (!TryValidate(expectedSlotCount, out error))
                return false;

            if (!TryEnsurePreviewStage(
                    expectedSlotCount,
                    presenterRoot.gameObject.layer,
                    out error))
            {
                return false;
            }

            _previewStage.AttachPresenterRoot(presenterRoot);

            if (!TryApplyPreviewAtlas(out error))
                return false;

            error = null;
            return true;
        }

        public bool TryApplySlotPose(
            int slotIndex,
            Transform target,
            float sourceCellSize,
            out string error)
        {
            if (_previewStage == null)
            {
                error = "Quick-bar preview stage is not initialized.";
                return false;
            }

            return _previewStage.TryApplySlotPose(
                slotIndex,
                target,
                sourceCellSize,
                _modelDepthOffset,
                out error);
        }

        public void SetSlotState(int slotIndex, bool isSelected)
        {
            if (_assignedStateRoots == null ||
                slotIndex < 0 ||
                slotIndex >= _assignedStateRoots.Length)
            {
                return;
            }

            Transform assignedRoot = _assignedStateRoots[slotIndex];

            if (assignedRoot != null &&
                assignedRoot.gameObject.activeSelf != isSelected)
            {
                assignedRoot.gameObject.SetActive(isSelected);
            }
        }

        private bool TryCacheSlots(
            int expectedSlotCount,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(_itemPreviewChildPath))
            {
                error = "Item-preview child path is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_assignedStateChildPath))
            {
                error = "Assigned-state child path is empty.";
                return false;
            }

            Canvas.ForceUpdateCanvases();

            RawImage[] previewImages =
                new RawImage[expectedSlotCount];

            Transform[] assignedRoots =
                new Transform[expectedSlotCount];

            for (int index = 0; index < expectedSlotCount; index++)
            {
                RectTransform slot =
                    _slotsRoot.GetChild(index) as RectTransform;

                if (slot == null)
                {
                    error = $"Quick slot {index + 1} does not use RectTransform.";
                    return false;
                }

                float width = Mathf.Abs(slot.rect.width);
                float height = Mathf.Abs(slot.rect.height);

                if (width <= Mathf.Epsilon || height <= Mathf.Epsilon)
                {
                    error = $"Quick slot {index + 1} must have a non-zero UI size.";
                    return false;
                }

                float aspectError = Mathf.Abs(width - height) /
                                    Mathf.Max(width, height);

                if (aspectError > _maximumSlotAspectError)
                {
                    error = $"Quick slot {index + 1} must be approximately " +
                            $"square. Its UI size is {width:F1} x {height:F1}.";
                    return false;
                }

                Transform previewRoot =
                    slot.Find(_itemPreviewChildPath);

                if (previewRoot == null ||
                    !previewRoot.TryGetComponent(
                        out RawImage previewImage))
                {
                    error = $"Quick slot {index + 1} must contain a RawImage " +
                            $"at '{_itemPreviewChildPath}'.";
                    return false;
                }

                Transform assignedRoot =
                    slot.Find(_assignedStateChildPath);

                if (assignedRoot == null)
                {
                    error = $"Quick slot {index + 1} must contain an object " +
                            $"at '{_assignedStateChildPath}'.";
                    return false;
                }

                previewImages[index] = previewImage;
                assignedRoots[index] = assignedRoot;
            }

            _itemPreviewImages = previewImages;
            _assignedStateRoots = assignedRoots;
            error = null;
            return true;
        }

        private bool TryEnsurePreviewStage(
            int slotCount,
            int itemLayer,
            out string error)
        {
            if (_previewStage != null &&
                _previewStage.Matches(
                    slotCount,
                    itemLayer,
                    _previewTextureHeight,
                    _slotPaddingRatio))
            {
                error = null;
                return true;
            }

            DisposePreviewStage();

            try
            {
                _previewStage = new InventoryQuickBarPreviewStage3D(
                    GetInstanceID(),
                    slotCount,
                    itemLayer,
                    _previewTextureHeight,
                    _slotPaddingRatio);
            }
            catch (System.Exception exception)
            {
                DisposePreviewStage();
                error = $"Could not create the quick-bar preview stage: " +
                        exception.Message;
                return false;
            }

            error = null;
            return true;
        }

        private bool TryApplyPreviewAtlas(out string error)
        {
            if (_previewStage == null || _itemPreviewImages == null)
            {
                error = "Quick-bar preview resources are not initialized.";
                return false;
            }

            for (int index = 0;
                 index < _itemPreviewImages.Length;
                 index++)
            {
                RawImage previewImage = _itemPreviewImages[index];

                if (previewImage == null)
                    continue;

                previewImage.texture = _previewStage.Texture;
                previewImage.material = null;
                previewImage.uvRect =
                    _previewStage.GetSlotUvRect(index);
            }

            error = null;
            return true;
        }

        private void DisposePreviewStage()
        {
            if (_itemPreviewImages != null && _previewStage != null)
            {
                foreach (RawImage previewImage in _itemPreviewImages)
                {
                    if (previewImage != null &&
                        previewImage.texture == _previewStage.Texture)
                    {
                        previewImage.texture = null;
                    }
                }
            }

            _previewStage?.Dispose();
            _previewStage = null;
        }

        private GameObject GetVisualRoot()
        {
            return _visualRoot != null
                ? _visualRoot
                : gameObject;
        }

        private static bool IsSameOrChildOf(
            Transform target,
            Transform expectedParent)
        {
            return target == expectedParent ||
                   target.IsChildOf(expectedParent);
        }

        private void OnValidate()
        {
            _itemPreviewImages = null;
            _assignedStateRoots = null;
            _animatorConfigurationChecked = false;
            _animatorConfigurationValid = false;
            _autoHideDelay = Mathf.Max(0f, _autoHideDelay);
        }

        private void OnDestroy()
        {
            DisposePreviewStage();
        }
    }
}
