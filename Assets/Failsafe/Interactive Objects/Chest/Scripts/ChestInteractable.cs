using System;
using Failsafe.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Failsafe.Chests
{
    public enum ChestInteractionState
    {
        Closed,
        CheckingRequirement,
        Open
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(ChestInventoryController))]
    public sealed class ChestInteractable : Interactable
    {
        [Header("Chest")]
        [SerializeField] private ChestInventoryController _inventory;
        [SerializeField] private ChestInventoryPresenter3D _presentation;
        [SerializeField] private ChestItemContextMenuController3D _contextMenu;

        [Header("Camera")]
        [SerializeField] private Transform _cameraAnchor;

        [Header("Optional Opening Requirement")]
        [SerializeField] private MonoBehaviour _openRequirementBehaviour;

        private ChestInteractionSession _session;
        private IChestOpenRequirement _activeRequirement;
        private int _requestToken;
        private int _openedFrame = -1;

        public ChestInteractionState State { get; private set; } =
            ChestInteractionState.Closed;

        private void Awake()
        {
            ResolveReferences();
            _presentation?.SetVisible(false);
            _contextMenu?.SetInteractionActive(false);
        }

        private void Update()
        {
            if (State == ChestInteractionState.Closed)
                return;

            if (_session == null || !_session.IsOwnedBy(this))
            {
                ResetLocalState();
                return;
            }

            if (Time.frameCount <= _openedFrame)
                return;

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            bool interactionPressed = keyboard.eKey.wasPressedThisFrame;
            bool escapePressed = keyboard.escapeKey.wasPressedThisFrame;

            if (!interactionPressed && !escapePressed)
                return;

            if (escapePressed &&
                State == ChestInteractionState.Open &&
                _contextMenu != null &&
                _contextMenu.TryCloseTopmost())
            {
                return;
            }

            if (!TryClose(out string error) &&
                !string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning(
                    $"Chest '{name}' could not close: {error}",
                    this);
            }
        }

        protected override void Interact(PlayerInteractionContext context)
        {
            if (State != ChestInteractionState.Closed)
            {
                TryClose(out _);
                return;
            }

            if (!TryOpen(context, out string error))
            {
                Debug.LogWarning(
                    $"Chest '{name}' could not open: {error}",
                    this);
            }
        }

        public bool TryOpen(
            PlayerInteractionContext context,
            out string error)
        {
            ResolveReferences();

            if (State != ChestInteractionState.Closed)
            {
                error = "Chest is already opening or open.";
                return false;
            }

            if (context?.PlayerInteraction == null ||
                context.PlayerCamera == null)
            {
                error = "Player interaction context is incomplete.";
                return false;
            }

            if (_inventory == null ||
                _presentation == null ||
                _contextMenu == null ||
                _cameraAnchor == null)
            {
                error =
                    "Chest requires inventory, presentation, context menu and " +
                    "camera anchor references.";
                return false;
            }

            IChestOpenRequirement requirement =
                _openRequirementBehaviour as IChestOpenRequirement;

            if (_openRequirementBehaviour != null && requirement == null)
            {
                error =
                    $"Opening requirement component " +
                    $"'{_openRequirementBehaviour.GetType().Name}' does not " +
                    $"implement {nameof(IChestOpenRequirement)}.";
                return false;
            }

            if (!_inventory.TryEnsureGenerated(out error))
                return false;

            if (!_presentation.TryInitialize(out error))
                return false;

            if (!TryResolvePlayerServices(
                    context,
                    out ChestInteractionSession session,
                    out ChestTransferService transferService,
                    out error))
            {
                return false;
            }

            if (!_contextMenu.Initialize(
                    _inventory,
                    _presentation,
                    transferService,
                    context.PlayerCamera,
                    out error))
            {
                return false;
            }

            if (!session.TryOpen(this, _cameraAnchor, out error))
                return false;

            _session = session;
            _activeRequirement = requirement;
            _openedFrame = Time.frameCount;
            int requestToken = ++_requestToken;
            _presentation.SetVisible(false);
            _contextMenu.SetInteractionActive(false);

            if (_activeRequirement == null)
                return CompleteOpen(requestToken, out error);

            State = ChestInteractionState.CheckingRequirement;

            try
            {
                _activeRequirement.BeginCheck(
                    new ChestOpenRequirementContext(_inventory, context),
                    result => HandleRequirementCompleted(
                        requestToken,
                        result));
            }
            catch (Exception exception)
            {
                TryClose(out _);
                error =
                    $"Chest opening requirement failed: {exception.Message}";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryClose(out string error)
        {
            if (State == ChestInteractionState.Closed && _session == null)
            {
                error = null;
                return true;
            }

            _requestToken++;

            if (State == ChestInteractionState.CheckingRequirement &&
                _activeRequirement != null)
            {
                try
                {
                    _activeRequirement.CancelCheck();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            _contextMenu?.SetInteractionActive(false);
            _presentation?.SetVisible(false);

            error = null;
            bool closed = _session == null ||
                          _session.TryClose(this, out error);

            _session = null;
            _activeRequirement = null;
            State = ChestInteractionState.Closed;
            _openedFrame = -1;

            if (closed)
                error = null;

            return closed;
        }

        private bool CompleteOpen(int requestToken, out string error)
        {
            if (requestToken != _requestToken ||
                _session == null ||
                !_session.IsOwnedBy(this))
            {
                error = "Chest opening request is no longer active.";
                return false;
            }

            if (!_presentation.SetVisible(true))
            {
                TryClose(out _);
                error = "Chest inventory presentation could not be shown.";
                return false;
            }

            State = ChestInteractionState.Open;
            _contextMenu.SetInteractionActive(true);
            error = null;
            return true;
        }

        private void HandleRequirementCompleted(
            int requestToken,
            ChestOpenRequirementResult result)
        {
            if (requestToken != _requestToken ||
                State != ChestInteractionState.CheckingRequirement)
            {
                return;
            }

            if (!result.IsGranted)
            {
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    Debug.LogWarning(
                        $"Chest '{name}' opening was rejected: " +
                        $"{result.Error}",
                        this);
                }

                TryClose(out _);
                return;
            }

            if (!CompleteOpen(requestToken, out string error))
            {
                Debug.LogWarning(
                    $"Chest '{name}' could not finish opening: {error}",
                    this);
            }
        }

        private static bool TryResolvePlayerServices(
            PlayerInteractionContext context,
            out ChestInteractionSession session,
            out ChestTransferService transferService,
            out string error)
        {
            session = null;
            transferService = null;

            PlayerLifetimeScope playerScope =
                context.PlayerInteraction.GetComponentInParent<
                    PlayerLifetimeScope>();

            if (playerScope == null || playerScope.Container == null)
            {
                error = "Player lifetime scope is unavailable.";
                return false;
            }

            try
            {
                session = playerScope.Container.Resolve<
                    ChestInteractionSession>();
                transferService = playerScope.Container.Resolve<
                    ChestTransferService>();
            }
            catch (Exception exception)
            {
                error =
                    $"Player chest services are unavailable: " +
                    $"{exception.Message}";
                return false;
            }

            if (session == null || transferService == null)
            {
                error = "Player chest services are unavailable.";
                return false;
            }

            error = null;
            return true;
        }

        private void ResolveReferences()
        {
            if (_inventory == null)
                _inventory = GetComponent<ChestInventoryController>();

            if (_presentation == null)
            {
                _presentation = GetComponentInChildren<
                    ChestInventoryPresenter3D>(true);
            }

            if (_contextMenu == null)
            {
                _contextMenu = GetComponentInChildren<
                    ChestItemContextMenuController3D>(true);
            }
        }

        private void ResetLocalState()
        {
            _requestToken++;

            if (State == ChestInteractionState.CheckingRequirement &&
                _activeRequirement != null)
            {
                try
                {
                    _activeRequirement.CancelCheck();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            _contextMenu?.SetInteractionActive(false);
            _presentation?.SetVisible(false);
            _session = null;
            _activeRequirement = null;
            State = ChestInteractionState.Closed;
            _openedFrame = -1;
        }

        private void OnDisable()
        {
            if (!TryClose(out string error) &&
                !string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning(
                    $"Chest '{name}' session cleanup failed: {error}",
                    this);
            }
        }
    }
}
