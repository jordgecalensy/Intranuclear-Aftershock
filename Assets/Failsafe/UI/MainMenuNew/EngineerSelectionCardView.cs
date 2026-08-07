using System;
using System.Text;
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

        [Header("Perk colors")]
        [SerializeField] private Color _positivePerkColor =
            new Color(0.15f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color _negativePerkColor =
            new Color(0.9f, 0.2f, 0.15f, 1f);

        private Action<int> _selectionRequested;
        private int _engineerIndex;
        private bool _buttonSubscribed;

        public void Bind(
            EngineerBuild engineer,
            int engineerIndex,
            Action<int> selectionRequested)
        {
            _engineerIndex = engineerIndex;
            _selectionRequested = selectionRequested;

            EnsureButtonSubscribed();

            if (_operatorCodeText != null)
            {
                _operatorCodeText.text = engineer != null
                    ? engineer.OperatorCode
                    : "-- ---";
            }

            if (_nameText != null)
                _nameText.text = engineer?.Name ?? $"Engineer {engineerIndex + 1}";

            if (_perksText != null)
                _perksText.text = CreatePerksText(engineer);

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

            SetInteractable(engineer != null);
            SetSelected(false);
            gameObject.SetActive(engineer != null);
        }

        public void SetInteractable(bool interactable)
        {
            if (_selectButton != null)
                _selectButton.interactable = interactable;
        }

        public void SetSelected(bool selected)
        {
            if (_selectionHighlight != null)
                _selectionHighlight.SetActive(selected);
        }

        public void Hide()
        {
            _selectionRequested = null;
            SetSelected(false);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_buttonSubscribed && _selectButton != null)
                _selectButton.onClick.RemoveListener(HandleSelectionRequested);

            _buttonSubscribed = false;
        }

        private void EnsureButtonSubscribed()
        {
            if (_buttonSubscribed || _selectButton == null)
                return;

            _selectButton.onClick.AddListener(HandleSelectionRequested);
            _buttonSubscribed = true;
        }

        private void HandleSelectionRequested()
        {
            _selectionRequested?.Invoke(_engineerIndex);
        }

        private string CreatePerksText(EngineerBuild engineer)
        {
            if (engineer?.Perks == null || engineer.Perks.Count == 0)
                return "No perks";

            string positiveColor =
                ColorUtility.ToHtmlStringRGB(_positivePerkColor);
            string negativeColor =
                ColorUtility.ToHtmlStringRGB(_negativePerkColor);
            var result = new StringBuilder();

            for (int perkIndex = 0;
                 perkIndex < engineer.Perks.Count;
                 perkIndex++)
            {
                EngineerPerk perk = engineer.Perks[perkIndex];

                if (perk == null)
                    continue;

                if (result.Length > 0)
                    result.AppendLine();

                string color = perk.IsNegative
                    ? negativeColor
                    : positiveColor;
                string displayName =
                    perk.Definition != null &&
                    !string.IsNullOrWhiteSpace(perk.Definition.DisplayName)
                        ? perk.Definition.DisplayName
                        : !string.IsNullOrWhiteSpace(perk.RandomizationItem.Name)
                            ? perk.RandomizationItem.Name
                            : "Unknown perk";
                string cost = perk.Cost >= 0
                    ? $"+{perk.Cost}"
                    : perk.Cost.ToString();

                result.Append($"<color=#{color}>• {displayName} ({cost})</color>");
            }

            return result.Length > 0
                ? result.ToString()
                : "No perks";
        }
    }
}
