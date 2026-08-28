using System;
using System.Collections;
using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    public enum InventoryRobotPresentationState
    {
        Hidden,
        Opening,
        Open,
        Closing
    }

    [DisallowMultipleComponent]
    public sealed class InventoryRobotPresentationController : MonoBehaviour
    {
        private const string DefaultOpenTriggerName = "OpenInventory";
        private const string DefaultCloseTriggerName = "CloseInventory";

        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject _visualRoot;

        [Header("Animator Contract")]
        [SerializeField] private string _openTriggerName =
            DefaultOpenTriggerName;

        [SerializeField] private string _closeTriggerName =
            DefaultCloseTriggerName;

        [Header("Transition Safety")]
        [SerializeField] private bool _useFallbackTimeout = true;
        [SerializeField, Min(0.05f)] private float _openingTimeout = 3f;
        [SerializeField, Min(0.05f)] private float _closingTimeout = 3f;
        [SerializeField] private bool _startHidden = true;

        public InventoryRobotPresentationState State { get; private set; } =
            InventoryRobotPresentationState.Hidden;

        public bool IsTransitioning =>
            State == InventoryRobotPresentationState.Opening ||
            State == InventoryRobotPresentationState.Closing;

        public bool IsOpen =>
            State == InventoryRobotPresentationState.Open;

        public event Action<InventoryRobotPresentationState> StateChanged;
        public event Action OpenCompleted;
        public event Action CloseCompleted;

        private Coroutine _fallbackCoroutine;

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            if (_startHidden)
                ForceHidden();
        }

        public bool RequestOpen()
        {
            if (State == InventoryRobotPresentationState.Open)
                return true;

            if (State != InventoryRobotPresentationState.Hidden)
                return false;

            SetVisualActive(true);
            SetState(InventoryRobotPresentationState.Opening);

            if (!TrySetAnimatorTrigger(
                    _openTriggerName,
                    _closeTriggerName))
            {
                NotifyInventoryReady();
                return true;
            }

            StartFallbackTimeout(
                _openingTimeout,
                InventoryRobotPresentationState.Opening);

            return true;
        }

        public bool RequestClose()
        {
            if (State == InventoryRobotPresentationState.Hidden)
                return true;

            if (State != InventoryRobotPresentationState.Open)
                return false;

            SetState(InventoryRobotPresentationState.Closing);

            if (!TrySetAnimatorTrigger(
                    _closeTriggerName,
                    _openTriggerName))
            {
                NotifyRobotHidden();
                return true;
            }

            StartFallbackTimeout(
                _closingTimeout,
                InventoryRobotPresentationState.Closing);

            return true;
        }

        // Animation Event: place at the frame where the inventory screen
        // has reached its final readable position.
        public void NotifyInventoryReady()
        {
            if (State != InventoryRobotPresentationState.Opening)
                return;

            StopFallbackTimeout();
            SetState(InventoryRobotPresentationState.Open);
            OpenCompleted?.Invoke();
        }

        // Animation Event: place at the final frame of the closing clip.
        public void NotifyRobotHidden()
        {
            if (State != InventoryRobotPresentationState.Closing)
                return;

            StopFallbackTimeout();
            SetVisualActive(false);
            SetState(InventoryRobotPresentationState.Hidden);
            CloseCompleted?.Invoke();
        }

        public void ForceHidden()
        {
            StopFallbackTimeout();

            if (_animator != null)
            {
                ResetTriggerIfAssigned(_openTriggerName);
                ResetTriggerIfAssigned(_closeTriggerName);
            }

            SetVisualActive(false);
            SetState(InventoryRobotPresentationState.Hidden);
        }

        private bool TrySetAnimatorTrigger(
            string triggerName,
            string triggerToReset)
        {
            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                return false;
            }

            if (!HasTrigger(triggerName))
            {
                Debug.LogWarning(
                    $"Inventory robot Animator has no Trigger parameter " +
                    $"named '{triggerName}'. The transition will complete " +
                    "immediately.",
                    this);

                return false;
            }

            ResetTriggerIfAssigned(triggerToReset);
            _animator.SetTrigger(triggerName);
            return true;
        }

        private bool HasTrigger(string parameterName)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            foreach (AnimatorControllerParameter parameter in
                     _animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger &&
                    string.Equals(
                        parameter.name,
                        parameterName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetTriggerIfAssigned(string triggerName)
        {
            if (HasTrigger(triggerName))
                _animator.ResetTrigger(triggerName);
        }

        private void StartFallbackTimeout(
            float timeout,
            InventoryRobotPresentationState expectedState)
        {
            StopFallbackTimeout();

            if (!_useFallbackTimeout)
                return;

            _fallbackCoroutine = StartCoroutine(
                CompleteAfterTimeout(timeout, expectedState));
        }

        private IEnumerator CompleteAfterTimeout(
            float timeout,
            InventoryRobotPresentationState expectedState)
        {
            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.05f, timeout));

            _fallbackCoroutine = null;

            if (State != expectedState)
                yield break;

            Debug.LogWarning(
                $"Inventory robot did not receive its completion " +
                $"Animation Event while {expectedState}. " +
                "The transition was completed by the safety timeout.",
                this);

            if (expectedState == InventoryRobotPresentationState.Opening)
                NotifyInventoryReady();
            else
                NotifyRobotHidden();
        }

        private void StopFallbackTimeout()
        {
            if (_fallbackCoroutine == null)
                return;

            StopCoroutine(_fallbackCoroutine);
            _fallbackCoroutine = null;
        }

        private void SetVisualActive(bool active)
        {
            if (_visualRoot != null && _visualRoot != gameObject)
                _visualRoot.SetActive(active);
        }

        private void SetState(InventoryRobotPresentationState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(state);
        }

        private void OnDisable()
        {
            StopFallbackTimeout();
        }
    }
}
