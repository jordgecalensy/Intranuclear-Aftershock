using System;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Failsafe.Inventory.Integration
{
    [DisallowMultipleComponent]
    public sealed class InventoryItemInfoPanel3D : MonoBehaviour
    {
        private const float MinimumAnchorScale = 0.000001f;

        [Header("Visual Roots")]
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Transform _modelAnchor;
        [SerializeField] private RectTransform _rotationArea;

        [Header("Text")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;

        [Header("Navigation")]
        [SerializeField] private Button _closeButton;

        [Header("3D Preview")]
        [SerializeField, Min(0.001f)] private float _previewCellSize = 0.18f;
        [SerializeField] private float _autoRotationSpeed = 20f;
        [SerializeField, Min(0.01f)] private float _dragRotationSpeed = 0.2f;
        [SerializeField, Range(0f, 89f)] private float _maximumPitch = 60f;

        public bool IsOpen =>
            _visualRoot != null && _visualRoot.activeSelf;

        public event Action CloseRequested;

        private Camera _eventCamera;
        private GameObject _previewRoot;
        private float _yaw;
        private float _pitch;
        private bool _draggingModel;
        private bool _initialized;

        private void Awake()
        {
            if (_visualRoot == null)
                _visualRoot = gameObject;

            SetVisible(false);
        }

        private void Update()
        {
            if (!IsOpen || _previewRoot == null)
                return;

            Mouse mouse = Mouse.current;

            if (mouse != null && _rotationArea != null)
            {
                Vector2 pointerPosition = mouse.position.ReadValue();

                if (mouse.leftButton.wasPressedThisFrame &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        _rotationArea,
                        pointerPosition,
                        _eventCamera))
                {
                    _draggingModel = true;
                }

                if (mouse.leftButton.wasReleasedThisFrame)
                    _draggingModel = false;

                if (_draggingModel && mouse.leftButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    _yaw -= delta.x * _dragRotationSpeed;
                    _pitch = Mathf.Clamp(
                        _pitch + delta.y * _dragRotationSpeed,
                        -_maximumPitch,
                        _maximumPitch);
                }
            }

            if (!_draggingModel)
                _yaw -= _autoRotationSpeed * Time.unscaledDeltaTime;

            ApplyPreviewRotation();
        }

        public void Initialize(Camera eventCamera)
        {
            _eventCamera = eventCamera;

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
                _closeButton.onClick.AddListener(HandleCloseClicked);
            }

            _initialized = true;
        }

        public bool TryShow(ItemData itemData, out string error)
        {
            if (!_initialized)
                Initialize(_eventCamera);

            if (itemData == null)
            {
                error = "ItemData is not assigned for the Info panel.";
                return false;
            }

            if (_visualRoot == null ||
                _modelAnchor == null ||
                _rotationArea == null)
            {
                error =
                    "Info panel requires Visual Root, Model Anchor and " +
                    "Rotation Area references.";

                return false;
            }

            if (_previewCellSize <= 0f)
            {
                error = "Info preview cell size must be greater than zero.";
                return false;
            }

            if (!ItemDataInventoryAdapter.TryCreateViewDefinition(
                    itemData,
                    out InventoryModelViewDefinition definition,
                    out error))
            {
                return false;
            }

            if (!TryGetAnchorScaleCompensation(
                    _modelAnchor,
                    out Vector3 scaleCompensation,
                    out error))
            {
                return false;
            }

            DestroyPreview();
            SetText(itemData);
            SetVisible(true);

            _previewRoot = new GameObject("Info Item Preview");
            _previewRoot.layer = _modelAnchor.gameObject.layer;
            _previewRoot.transform.SetParent(_modelAnchor, false);
            _previewRoot.transform.localScale = scaleCompensation;

            try
            {
                InventoryItemView3D preview =
                    _previewRoot.AddComponent<InventoryItemView3D>();

                preview.Initialize(
                    definition,
                    new InventoryGridSize(
                        itemData.InventoryWidth,
                        itemData.InventoryHeight),
                    _previewCellSize);
            }
            catch (Exception exception)
            {
                DestroyPreview();
                SetVisible(false);
                error = exception.Message;
                return false;
            }

            _yaw = 0f;
            _pitch = 0f;
            _draggingModel = false;
            ApplyPreviewRotation();
            error = null;
            return true;
        }

        public void Hide()
        {
            _draggingModel = false;
            DestroyPreview();
            SetVisible(false);
        }

        private void SetText(ItemData itemData)
        {
            if (_titleText != null)
            {
                _titleText.text = string.IsNullOrWhiteSpace(
                        itemData.DisplayName)
                    ? itemData.name
                    : itemData.DisplayName;
            }

            if (_descriptionText != null)
                _descriptionText.text = itemData.Description ?? string.Empty;
        }

        private void ApplyPreviewRotation()
        {
            if (_previewRoot == null)
                return;

            Vector3 verticalAxis = Vector3.forward;
            Vector3 horizontalAxis = Vector3.right;
            Transform previewParent = _previewRoot.transform.parent;

            if (_eventCamera != null && previewParent != null)
            {
                Transform cameraTransform = _eventCamera.transform;
                verticalAxis = previewParent
                    .InverseTransformDirection(cameraTransform.up)
                    .normalized;
                horizontalAxis = previewParent
                    .InverseTransformDirection(cameraTransform.right)
                    .normalized;
            }

            _previewRoot.transform.localRotation =
                Quaternion.AngleAxis(_pitch, horizontalAxis) *
                Quaternion.AngleAxis(_yaw, verticalAxis);
        }

        private static bool TryGetAnchorScaleCompensation(
            Transform anchor,
            out Vector3 scaleCompensation,
            out string error)
        {
            Vector3 scale = anchor.lossyScale;

            if (!IsUsableScale(scale.x) ||
                !IsUsableScale(scale.y) ||
                !IsUsableScale(scale.z))
            {
                scaleCompensation = Vector3.one;
                error =
                    $"Info model anchor '{anchor.name}' has a zero or invalid world scale.";

                return false;
            }

            scaleCompensation = new Vector3(
                1f / scale.x,
                1f / scale.y,
                1f / scale.z);

            error = null;
            return true;
        }

        private static bool IsUsableScale(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   Mathf.Abs(value) >= MinimumAnchorScale;
        }

        private void HandleCloseClicked()
        {
            CloseRequested?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            if (_visualRoot != null &&
                _visualRoot.activeSelf != visible)
            {
                _visualRoot.SetActive(visible);
            }
        }

        private void DestroyPreview()
        {
            if (_previewRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(_previewRoot);
            else
                DestroyImmediate(_previewRoot);

            _previewRoot = null;
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(HandleCloseClicked);

            DestroyPreview();
        }
    }
}
