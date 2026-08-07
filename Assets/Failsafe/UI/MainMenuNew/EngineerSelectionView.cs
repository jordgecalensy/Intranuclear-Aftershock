using Assets.Failsafe.Scripts.RandomGeneration;
using Cysharp.Threading.Tasks;
using Failsafe.Scripts.SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Failsafe.UI.MainMenuNew
{
    public sealed class EngineerSelectionView : MonoBehaviour
    {
        [Header("Generation")]
        [SerializeField] private EngineerGenerationConfig _generationConfig;

        [Header("Screens")]
        [SerializeField] private GameObject _mainMenuRoot;
        [SerializeField] private GameObject _selectionWindowRoot;

        [Header("Engineer cards")]
        [SerializeField] private EngineerSelectionCardView[] _cards;

        [Header("Actions")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _rerollButton;

        [Header("Optional debug information")]
        [SerializeField] private TMP_Text _seedText;
        [SerializeField] private TMP_Text _errorText;

        private EngineerBuildGenerator _generator;
        private EngineerSelectionState _selectionState;
        private IRunSessionCoordinator _runSessionCoordinator;
        private bool _startingRun;

        [Inject]
        public void Construct(
            EngineerBuildGenerator generator,
            EngineerSelectionState selectionState,
            IRunSessionCoordinator runSessionCoordinator)
        {
            _generator = generator;
            _selectionState = selectionState;
            _runSessionCoordinator = runSessionCoordinator;
        }

        public void OpenForNewRun()
        {
            if (_startingRun)
                return;

            ShowSelectionWindow();
            GenerateOffers();
        }

        public void RegenerateOffers()
        {
            if (_startingRun ||
                (_runSessionCoordinator != null &&
                 _runSessionCoordinator.IsBusy))
            {
                return;
            }

            GenerateOffers();
        }

        public void ConfirmSelection()
        {
            if (_startingRun ||
                _runSessionCoordinator == null ||
                _runSessionCoordinator.IsBusy)
            {
                return;
            }

            if (_selectionState == null || !_selectionState.HasSelection)
            {
                SetError("Select an engineer before starting the run.");
                return;
            }

            StartSelectedRunAsync().Forget();
        }

        private void GenerateOffers()
        {
            SetError(null);
            _selectionState?.Clear();
            HideCards();
            SetInteraction(false);

            if (!TryValidateSetup(out string setupError))
            {
                SetInteraction(true);
                SetError(setupError);
                return;
            }

            if (!_generator.TryGenerateForNewRun(
                    _generationConfig,
                    out EngineerGenerationResult result,
                    out string generationError))
            {
                SetInteraction(true);
                SetError(generationError);
                return;
            }

            _selectionState.SetOffers(result);
            BindCards(result);
            SetSelectedCard(-1);
            SetInteraction(true);

            if (_seedText != null)
                _seedText.text = $"Seed: {result.Seed}";
        }

        public void Close()
        {
            if (_startingRun)
                return;

            _selectionState?.Clear();
            SetSelectedCard(-1);
            SetInteraction(true);

            if (_selectionWindowRoot != null)
                _selectionWindowRoot.SetActive(false);

            if (_mainMenuRoot != null)
                _mainMenuRoot.SetActive(true);
        }

        private void HandleEngineerSelected(int engineerIndex)
        {
            if (_startingRun)
            {
                return;
            }

            if (!_selectionState.TrySelectEngineer(
                    engineerIndex,
                    out string selectionError))
            {
                SetError(selectionError);
                return;
            }

            SetSelectedCard(engineerIndex);
            SetInteraction(true);
            SetError(null);
        }

        private async UniTaskVoid StartSelectedRunAsync()
        {
            _startingRun = true;
            SetInteraction(false);
            SetError(null);

            RunSaveOperationResult result =
                await _runSessionCoordinator.StartNewRunAsync();

            if (this == null)
                return;

            if (!result.Succeeded)
            {
                _startingRun = false;
                SetInteraction(true);
                SetError(result.Error);
            }
        }

        private bool TryValidateSetup(out string error)
        {
            if (_generator == null ||
                _selectionState == null ||
                _runSessionCoordinator == null)
            {
                error =
                    "Engineer selection dependencies were not injected. " +
                    "Check MainMenuLifetimeScope and its RootLifetimeScope parent.";
                return false;
            }

            if (_generationConfig == null)
            {
                error = "EngineerGenerationConfig is not assigned.";
                return false;
            }

            if (_cards == null ||
                _cards.Length < _generationConfig.EngineerCount)
            {
                error =
                    $"The selection window needs at least " +
                    $"{_generationConfig.EngineerCount} engineer cards.";
                return false;
            }

            if (_confirmButton == null || _rerollButton == null)
            {
                error = "Confirm Button and Reroll Button must be assigned.";
                return false;
            }

            for (int cardIndex = 0;
                 cardIndex < _generationConfig.EngineerCount;
                 cardIndex++)
            {
                if (_cards[cardIndex] != null)
                    continue;

                error = $"Engineer card {cardIndex + 1} is not assigned.";
                return false;
            }

            error = null;
            return true;
        }

        private void BindCards(EngineerGenerationResult result)
        {
            for (int cardIndex = 0; cardIndex < _cards.Length; cardIndex++)
            {
                EngineerSelectionCardView card = _cards[cardIndex];

                if (card == null)
                    continue;

                if (cardIndex >= result.Engineers.Count)
                {
                    card.Hide();
                    continue;
                }

                card.Bind(
                    result.Engineers[cardIndex],
                    cardIndex,
                    HandleEngineerSelected);
            }
        }

        private void HideCards()
        {
            if (_cards == null)
                return;

            for (int cardIndex = 0; cardIndex < _cards.Length; cardIndex++)
                _cards[cardIndex]?.Hide();
        }

        private void SetSelectedCard(int selectedCardIndex)
        {
            if (_cards == null)
                return;

            for (int cardIndex = 0; cardIndex < _cards.Length; cardIndex++)
            {
                _cards[cardIndex]?.SetSelected(
                    cardIndex == selectedCardIndex);
            }
        }

        private void SetCardsInteractable(bool interactable)
        {
            if (_cards == null)
                return;

            for (int cardIndex = 0; cardIndex < _cards.Length; cardIndex++)
                _cards[cardIndex]?.SetInteractable(interactable);
        }

        private void SetInteraction(bool interactable)
        {
            SetCardsInteractable(interactable);

            if (_confirmButton != null)
            {
                _confirmButton.interactable =
                    interactable &&
                    _selectionState != null &&
                    _selectionState.HasSelection;
            }

            if (_rerollButton != null)
                _rerollButton.interactable = interactable;
        }

        private void ShowSelectionWindow()
        {
            if (_mainMenuRoot != null)
                _mainMenuRoot.SetActive(false);

            if (_selectionWindowRoot != null)
                _selectionWindowRoot.SetActive(true);
        }

        private void SetError(string message)
        {
            bool hasError = !string.IsNullOrWhiteSpace(message);

            if (_errorText == null)
            {
                if (hasError)
                    Debug.LogError($"[EngineerSelection] {message}", this);

                return;
            }

            _errorText.gameObject.SetActive(hasError);
            _errorText.text = hasError ? message : string.Empty;
        }
    }
}
