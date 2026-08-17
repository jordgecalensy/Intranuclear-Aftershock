using System;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Failsafe.Player.UI
{
    [DisallowMultipleComponent]
    public sealed class DeathScreenView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _screenRoot;

        [SerializeField]
        private Button _newRunButton;

        [SerializeField]
        private Button _mainMenuButton;

        public event Action NewRunRequested;
        public event Action MainMenuRequested;

        private bool IsConfigured =>
            _screenRoot != null &&
            _newRunButton != null &&
            _mainMenuButton != null;

        private void Awake()
        {
            if (!IsConfigured)
            {
                RunSaveLog.Error(
                    RunSaveLog.DeathScreen,
                    $"{nameof(DeathScreenView)} is not configured. " +
                    "Assign the screen root and both buttons.",
                    this);
                enabled = false;
                return;
            }

            _newRunButton.onClick.AddListener(HandleNewRunClicked);
            _mainMenuButton.onClick.AddListener(HandleMainMenuClicked);
            Hide();
        }

        private void OnDestroy()
        {
            if (_newRunButton != null)
                _newRunButton.onClick.RemoveListener(HandleNewRunClicked);

            if (_mainMenuButton != null)
                _mainMenuButton.onClick.RemoveListener(HandleMainMenuClicked);
        }

        public void Show()
        {
            if (!IsConfigured)
                return;

            _screenRoot.SetActive(true);
            SetInteractable(true);
        }

        public void Hide()
        {
            if (_screenRoot != null)
                _screenRoot.SetActive(false);
        }

        public void SetInteractable(bool interactable)
        {
            if (_newRunButton != null)
                _newRunButton.interactable = interactable;

            if (_mainMenuButton != null)
                _mainMenuButton.interactable = interactable;
        }

        private void HandleNewRunClicked()
        {
            NewRunRequested?.Invoke();
        }

        private void HandleMainMenuClicked()
        {
            MainMenuRequested?.Invoke();
        }
    }
}
