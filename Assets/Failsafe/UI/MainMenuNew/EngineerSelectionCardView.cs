using System;
using System.Collections.Generic;
using Assets.Failsafe.Scripts.RandomGeneration;
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

        private readonly List<PerkBadge> _perkBadges = new();
        private Action<int> _selectionRequested;
        private Action _confirmationRequested;
        private int _engineerIndex;
        private bool _selectButtonSubscribed;
        private bool _confirmButtonSubscribed;
        private bool _selected;
        private bool _interactable;
        private bool _perkBadgeContainerConfigured;

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
    }
}
