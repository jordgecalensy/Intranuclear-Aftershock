using System;
using System.Collections.Generic;
using Assets.Failsafe.Scripts.RandomGeneration;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Integration;
using Failsafe.Inventory.Presentation;
using Failsafe.Scripts.EffectSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Failsafe.UI.MainMenuNew
{
    public sealed class EngineerSelectionCardView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text _operatorCodeText;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _perksText;
        [SerializeField] private TMP_Text _budgetText;
        [SerializeField] private TMP_Text _equipmentRemainderText;

        [Header("Selection")]
        [SerializeField] private Button _selectButton;
        [SerializeField] private GameObject _selectionHighlight;
        [SerializeField] private GameObject _selectionLine;
        [SerializeField] private GameObject _confirmArea;
        [SerializeField] private Button _confirmButton;

        [Header("Perk colors")]
        [SerializeField] private Color _positivePerkColor =
            new Color(0.15f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color _negativePerkColor =
            new Color(0.9f, 0.2f, 0.15f, 1f);
        [SerializeField] private Color _perkLabelColor = Color.black;
        [SerializeField, Min(1f)] private float _perkBadgeHeight = 38f;
        [SerializeField, Min(0f)] private float _perkBadgeSpacing = 8f;

        [Header("Starting item preview")]
        [SerializeField] private RectTransform _equipmentSlotsRoot;
        [SerializeField, Range(64, 512)]
        private int _equipmentPreviewTextureHeight = 256;
        [SerializeField, Range(0f, 0.5f)]
        private float _equipmentPreviewSlotPadding = 0.08f;
        [SerializeField] private float _equipmentPreviewModelDepthOffset;
        [SerializeField] private Color _equipmentQuantityColor = Color.white;

        private readonly List<PerkBadge> _perkBadges = new();
        private readonly List<StartingItemPreviewSlot>
            _startingItemPreviewSlots = new();
        private Action<int> _selectionRequested;
        private Action _confirmationRequested;
        private int _engineerIndex;
        private bool _selectButtonSubscribed;
        private bool _confirmButtonSubscribed;
        private bool _selected;
        private bool _interactable;
        private bool _perkBadgeContainerConfigured;
        private InventoryQuickBarPreviewStage3D _startingItemPreviewStage;
        private GameObject _startingItemPresenterRoot;

        public void Bind(
            EngineerBuild engineer,
            int engineerIndex,
            Action<int> selectionRequested,
            Action confirmationRequested)
        {
            _engineerIndex = engineerIndex;
            _selectionRequested = selectionRequested;
            _confirmationRequested = confirmationRequested;

            EnsureButtonsSubscribed();

            if (_operatorCodeText != null)
            {
                _operatorCodeText.text = engineer != null
                    ? engineer.OperatorCode
                    : "-- ---";
            }

            if (_nameText != null)
                _nameText.text = engineer?.Name ?? $"Engineer {engineerIndex + 1}";

            RenderPerkBadges(engineer);
            RenderStartingItems(engineer);

            if (_budgetText != null)
            {
                _budgetText.text = engineer != null
                    ? $"Budget: {engineer.TotalWeight}"
                    : "Budget: -";
            }

            if (_equipmentRemainderText != null)
            {
                _equipmentRemainderText.text = engineer != null
                    ? $"Equipment points: {engineer.RemainingWeight}"
                    : "Equipment points: -";
            }

            SetSelected(false);
            SetInteractable(engineer != null);
            gameObject.SetActive(engineer != null);
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;

            if (_selectButton != null)
                _selectButton.interactable = interactable;

            UpdateConfirmButtonState();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;

            if (_selectionHighlight != null)
                _selectionHighlight.SetActive(selected);

            if (_selectionLine != null)
                _selectionLine.SetActive(selected);

            if (_confirmArea != null)
                _confirmArea.SetActive(selected);

            UpdateConfirmButtonState();
        }

        public void Hide()
        {
            _selectionRequested = null;
            _confirmationRequested = null;
            SetSelected(false);
            SetVisiblePerkBadgeCount(0);
            ClearStartingItemPreview();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_selectButtonSubscribed && _selectButton != null)
                _selectButton.onClick.RemoveListener(HandleSelectionRequested);

            if (_confirmButtonSubscribed && _confirmButton != null)
                _confirmButton.onClick.RemoveListener(HandleConfirmationRequested);

            _selectButtonSubscribed = false;
            _confirmButtonSubscribed = false;
            ClearStartingItemPreview();
        }

        private void EnsureButtonsSubscribed()
        {
            if (!_selectButtonSubscribed && _selectButton != null)
            {
                _selectButton.onClick.AddListener(HandleSelectionRequested);
                _selectButtonSubscribed = true;
            }

            if (!_confirmButtonSubscribed && _confirmButton != null)
            {
                _confirmButton.onClick.AddListener(HandleConfirmationRequested);
                _confirmButtonSubscribed = true;
            }
        }

        private void HandleSelectionRequested()
        {
            _selectionRequested?.Invoke(_engineerIndex);
        }

        private void HandleConfirmationRequested()
        {
            if (_selected && _interactable)
                _confirmationRequested?.Invoke();
        }

        private void UpdateConfirmButtonState()
        {
            if (_confirmButton != null)
                _confirmButton.interactable = _selected && _interactable;
        }

        private void RenderPerkBadges(EngineerBuild engineer)
        {
            if (_perksText == null)
                return;

            ConfigurePerkBadgeContainer();
            int visibleBadgeCount = 0;

            if (engineer?.Perks != null)
            {
                for (int perkIndex = 0;
                     perkIndex < engineer.Perks.Count;
                     perkIndex++)
                {
                    EngineerPerk perk = engineer.Perks[perkIndex];

                    if (perk == null)
                        continue;

                    EnsurePerkBadgeCount(visibleBadgeCount + 1);
                    PerkBadge badge = _perkBadges[visibleBadgeCount];
                    badge.Root.SetActive(true);
                    badge.Background.color = perk.IsNegative
                        ? _negativePerkColor
                        : _positivePerkColor;
                    badge.Label.text = ResolvePerkDisplayName(perk)
                        .ToUpperInvariant();
                    visibleBadgeCount++;
                }
            }

            SetVisiblePerkBadgeCount(visibleBadgeCount);
        }

        private void ConfigurePerkBadgeContainer()
        {
            if (_perkBadgeContainerConfigured || _perksText == null)
                return;

            _perksText.text = string.Empty;
            _perksText.raycastTarget = false;
            _perksText.enabled = false;
            _perkBadgeContainerConfigured = true;
        }

        private void EnsurePerkBadgeCount(int requiredCount)
        {
            while (_perkBadges.Count < requiredCount)
                _perkBadges.Add(CreatePerkBadge(_perkBadges.Count));
        }

        private PerkBadge CreatePerkBadge(int badgeIndex)
        {
            var root = new GameObject(
                $"PerkBadge {badgeIndex + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            root.layer = gameObject.layer;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(_perksText.rectTransform, false);
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(
                0f,
                -badgeIndex * (_perkBadgeHeight + _perkBadgeSpacing));
            rootRect.sizeDelta = new Vector2(0f, _perkBadgeHeight);

            Image background = root.GetComponent<Image>();
            background.raycastTarget = false;

            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = gameObject.layer;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rootRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(-12f, -4f);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = _perksText.font;
            label.fontSharedMaterial = _perksText.fontSharedMaterial;
            label.fontSize = _perksText.fontSize;
            label.fontSizeMax = _perksText.fontSize;
            label.fontSizeMin = Mathf.Min(14f, _perksText.fontSize);
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.color = _perkLabelColor;
            label.raycastTarget = false;
            label.text = string.Empty;

            return new PerkBadge(root, background, label);
        }

        private void SetVisiblePerkBadgeCount(int visibleCount)
        {
            for (int badgeIndex = 0;
                 badgeIndex < _perkBadges.Count;
                 badgeIndex++)
            {
                _perkBadges[badgeIndex].Root.SetActive(
                    badgeIndex < visibleCount);
            }
        }

        private static string ResolvePerkDisplayName(EngineerPerk perk)
        {
            if (perk?.Definition != null &&
                !string.IsNullOrWhiteSpace(perk.Definition.DisplayName))
            {
                return perk.Definition.DisplayName;
            }

            return !string.IsNullOrWhiteSpace(perk?.RandomizationItem.Name)
                ? perk.RandomizationItem.Name
                : "Unknown perk";
        }

        private void RenderStartingItems(EngineerBuild engineer)
        {
            ClearStartingItemPreview();

            if (!TryEnsureStartingItemPreviewSlots(out string error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning(
                        $"[EngineerSelection] Starting item preview is " +
                        $"unavailable on '{name}': {error}",
                        this);
                }

                return;
            }

            List<PerkStartingItemGrant> grants =
                CollectStartingItemGrants(engineer);

            if (grants.Count == 0)
                return;

            int visibleCount = Mathf.Min(
                grants.Count,
                _startingItemPreviewSlots.Count);

            try
            {
                _startingItemPreviewStage =
                    new InventoryQuickBarPreviewStage3D(
                        GetInstanceID(),
                        _startingItemPreviewSlots.Count,
                        gameObject.layer,
                        _equipmentPreviewTextureHeight,
                        _equipmentPreviewSlotPadding);

                _startingItemPresenterRoot = new GameObject(
                    $"Starting Item Preview Models [{GetInstanceID()}]");
                _startingItemPresenterRoot.hideFlags = HideFlags.DontSave;
                _startingItemPresenterRoot.layer =
                    _startingItemPreviewStage.ItemLayer;

                _startingItemPreviewStage.AttachPresenterRoot(
                    _startingItemPresenterRoot.transform);

                ApplyStartingItemPreviewAtlas();

                for (int slotIndex = 0;
                     slotIndex < visibleCount;
                     slotIndex++)
                {
                    RenderStartingItem(
                        slotIndex,
                        grants[slotIndex]);
                }

                _startingItemPreviewStage.SetVisible(true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[EngineerSelection] Could not build starting item " +
                    $"preview on '{name}': {exception.Message}",
                    this);
                ClearStartingItemPreview();
                return;
            }

            if (grants.Count > _startingItemPreviewSlots.Count)
            {
                Debug.LogWarning(
                    $"[EngineerSelection] Engineer '{engineer?.Name}' has " +
                    $"{grants.Count} starting item entries, but the card " +
                    $"contains only {_startingItemPreviewSlots.Count} slots.",
                    this);
            }
        }

        private static List<PerkStartingItemGrant>
            CollectStartingItemGrants(EngineerBuild engineer)
        {
            var result = new List<PerkStartingItemGrant>();

            if (engineer?.Perks == null)
                return result;

            for (int perkIndex = 0;
                 perkIndex < engineer.Perks.Count;
                 perkIndex++)
            {
                PerkStartingItemGrant[] grants =
                    engineer.Perks[perkIndex]?.Definition?.StartingItems;

                if (grants == null)
                    continue;

                for (int grantIndex = 0;
                     grantIndex < grants.Length;
                     grantIndex++)
                {
                    PerkStartingItemGrant grant = grants[grantIndex];

                    if (grant?.Item != null)
                        result.Add(grant);
                }
            }

            return result;
        }

        private bool TryEnsureStartingItemPreviewSlots(out string error)
        {
            if (_startingItemPreviewSlots.Count > 0)
            {
                error = null;
                return true;
            }

            if (_equipmentSlotsRoot == null)
                _equipmentSlotsRoot = FindEquipmentSlotsRoot();

            if (_equipmentSlotsRoot == null)
            {
                error = "EquipmentGrid was not found below the card.";
                return false;
            }

            for (int childIndex = 0;
                 childIndex < _equipmentSlotsRoot.childCount;
                 childIndex++)
            {
                if (_equipmentSlotsRoot.GetChild(childIndex) is not
                    RectTransform slotRoot)
                {
                    continue;
                }

                _startingItemPreviewSlots.Add(
                    CreateStartingItemPreviewSlot(slotRoot, childIndex));
            }

            if (_startingItemPreviewSlots.Count == 0)
            {
                error = "EquipmentGrid contains no UI slots.";
                return false;
            }

            error = null;
            return true;
        }

        private RectTransform FindEquipmentSlotsRoot()
        {
            RectTransform[] descendants =
                GetComponentsInChildren<RectTransform>(true);

            for (int index = 0; index < descendants.Length; index++)
            {
                RectTransform candidate = descendants[index];

                if (candidate != null &&
                    candidate != transform &&
                    candidate.name == "EquipmentGrid")
                {
                    return candidate;
                }
            }

            return null;
        }

        private StartingItemPreviewSlot CreateStartingItemPreviewSlot(
            RectTransform slotRoot,
            int slotIndex)
        {
            var previewObject = new GameObject(
                $"StartingItemPreview {slotIndex + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            previewObject.layer = gameObject.layer;

            RectTransform previewRect =
                previewObject.GetComponent<RectTransform>();
            previewRect.SetParent(slotRoot, false);
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.anchoredPosition = Vector2.zero;
            previewRect.sizeDelta = new Vector2(-8f, -8f);

            RawImage preview = previewObject.GetComponent<RawImage>();
            preview.raycastTarget = false;
            preview.color = Color.white;
            preview.gameObject.SetActive(false);

            var quantityObject = new GameObject(
                "Quantity",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            quantityObject.layer = gameObject.layer;

            RectTransform quantityRect =
                quantityObject.GetComponent<RectTransform>();
            quantityRect.SetParent(slotRoot, false);
            quantityRect.anchorMin = Vector2.zero;
            quantityRect.anchorMax = Vector2.one;
            quantityRect.anchoredPosition = Vector2.zero;
            quantityRect.sizeDelta = new Vector2(-6f, -4f);

            TextMeshProUGUI quantity =
                quantityObject.GetComponent<TextMeshProUGUI>();
            quantity.font = _perksText != null ? _perksText.font : null;
            quantity.fontSize = 18f;
            quantity.alignment = TextAlignmentOptions.BottomRight;
            quantity.color = _equipmentQuantityColor;
            quantity.raycastTarget = false;
            quantity.text = string.Empty;
            quantity.gameObject.SetActive(false);

            return new StartingItemPreviewSlot(preview, quantity);
        }

        private void ApplyStartingItemPreviewAtlas()
        {
            for (int slotIndex = 0;
                 slotIndex < _startingItemPreviewSlots.Count;
                 slotIndex++)
            {
                StartingItemPreviewSlot slot =
                    _startingItemPreviewSlots[slotIndex];

                slot.Preview.texture = _startingItemPreviewStage.Texture;
                slot.Preview.material = null;
                slot.Preview.uvRect =
                    _startingItemPreviewStage.GetSlotUvRect(slotIndex);
            }
        }

        private void RenderStartingItem(
            int slotIndex,
            PerkStartingItemGrant grant)
        {
            StartingItemPreviewSlot slot =
                _startingItemPreviewSlots[slotIndex];

            if (!ItemDataInventoryAdapter.TryCreateViewDefinition(
                    grant.Item,
                    out InventoryModelViewDefinition definition,
                    out string error))
            {
                Debug.LogWarning(
                    $"[EngineerSelection] Could not preview starting " +
                    $"item '{grant.Item.name}': {error}",
                    grant.Item);
                return;
            }

            var viewObject = new GameObject(
                $"Starting Item [{grant.Item.name}]");
            viewObject.layer = _startingItemPreviewStage.ItemLayer;
            viewObject.transform.SetParent(
                _startingItemPresenterRoot.transform,
                false);

            InventoryItemView3D view =
                viewObject.AddComponent<InventoryItemView3D>();
            view.Initialize(
                definition,
                new InventoryGridSize(1, 1),
                0.84f);

            if (!_startingItemPreviewStage.TryApplySlotPose(
                    slotIndex,
                    view.transform,
                    1f,
                    _equipmentPreviewModelDepthOffset,
                    out error))
            {
                throw new InvalidOperationException(error);
            }

            slot.Preview.gameObject.SetActive(true);
            slot.Quantity.text = grant.Quantity > 1
                ? grant.Quantity.ToString()
                : string.Empty;
            slot.Quantity.gameObject.SetActive(grant.Quantity > 1);
        }

        private void ClearStartingItemPreview()
        {
            for (int slotIndex = 0;
                 slotIndex < _startingItemPreviewSlots.Count;
                 slotIndex++)
            {
                StartingItemPreviewSlot slot =
                    _startingItemPreviewSlots[slotIndex];

                if (slot.Preview != null)
                {
                    slot.Preview.texture = null;
                    slot.Preview.gameObject.SetActive(false);
                }

                if (slot.Quantity != null)
                {
                    slot.Quantity.text = string.Empty;
                    slot.Quantity.gameObject.SetActive(false);
                }
            }

            _startingItemPreviewStage?.Dispose();
            _startingItemPreviewStage = null;
            _startingItemPresenterRoot = null;
        }

        private sealed class PerkBadge
        {
            public PerkBadge(
                GameObject root,
                Image background,
                TMP_Text label)
            {
                Root = root;
                Background = background;
                Label = label;
            }

            public GameObject Root { get; }
            public Image Background { get; }
            public TMP_Text Label { get; }
        }

        private sealed class StartingItemPreviewSlot
        {
            public StartingItemPreviewSlot(
                RawImage preview,
                TMP_Text quantity)
            {
                Preview = preview;
                Quantity = quantity;
            }

            public RawImage Preview { get; }
            public TMP_Text Quantity { get; }
        }
    }
}
