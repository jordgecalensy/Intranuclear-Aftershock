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

        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject _visualRoot;

        [SerializeField] private Animator _bodyAnimator;
        [SerializeField] private string _bodyOpenState = "Base Layer.RUVI_Open";
        [SerializeField, Min(0.01f)] private float _bodyOpenSpeed = 1f;
        [SerializeField, Min(0.1f)] private float _bodyOpeningTimeout = 10f;

        [SerializeField] private string _bodyOpen2State = "Base Layer.RUVI_Open2";
        [SerializeField] private string _bodyOpen3State = "Base Layer.RUVI_Open3";
        [SerializeField] private string _bodyRefusalState = "Base Layer.RUVI_Special(FuckOff)";
        [Tooltip("Random for normal gameplay. Other values force a reaction for testing.")]
        [SerializeField] private InventoryRobotReaction _reactionOverride = InventoryRobotReaction.Random;

        public InventoryRobotReaction CurrentReaction { get; private set; } = InventoryRobotReaction.Open1;

        [SerializeField] private string _bodyCloseSourceState = "Base Layer.RUVI_Open";
        [SerializeField, Min(0.01f)] private float _bodyCloseSpeed = 1f;
        [SerializeField, Min(0.1f)] private float _bodyClosingTimeout = 10f;

        [SerializeField] private string _returnState = "Base Layer.Closing";
        [SerializeField] private string _openTriggerName =
            DefaultOpenTriggerName;

        [SerializeField] private string _closeTriggerName =
            DefaultCloseTriggerName;

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
        public event Action OpenRejected;

        private Coroutine _fallbackCoroutine;
        private Coroutine _bodyCoroutine;
        private bool _flightReady;
        private bool _bodyReady;
        private bool _returnStarted;
        private bool _refusalSelected;
        private string _activeBodyOpenState;
        private bool _closeRequested;

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            ResolveBodyAnimator();

            if (_startHidden)
                ForceHidden();
        }

        private void LateUpdate()
        {
            // Animation Events run inside Animator evaluation. Never start a new
            // sampled animation from that call stack (especially a queued close).
            if (State == InventoryRobotPresentationState.Opening && _closeRequested)
                CancelOpening();
            else
                TryCompleteOpening();
        }

        private void CancelOpening()
        {
            StopFallbackTimeout();
            StopBodyOpening();
            _closeRequested = false;
            float bodyTime = 0f;
            bool canReverse = !_refusalSelected && CanSampleBodyState(_activeBodyOpenState);
            if (canReverse)
            {
                AnimatorStateInfo bodyState = _bodyAnimator.GetCurrentAnimatorStateInfo(0);
                if (bodyState.fullPathHash == Animator.StringToHash(_activeBodyOpenState))
                    bodyTime = Mathf.Clamp01(bodyState.normalizedTime);
            }
            if (_bodyAnimator != null)
                _bodyAnimator.speed = 0f;
            SetState(InventoryRobotPresentationState.Closing);
            _returnStarted = false;
            // Finish reversing only the already-played part before flying away.
            // Starting return here would let its hide event cut off lid closing.
            // Normal closing still uses the complete, standard Open1 clip.
            if (canReverse && bodyTime > 0f)
                _bodyCoroutine = StartCoroutine(PlayBodyClosingFromPose(_activeBodyOpenState, bodyTime));
            else
                BeginReturn();
        }

        public bool RequestOpen()
        {
            if (!isActiveAndEnabled)
                return false;
            if (State == InventoryRobotPresentationState.Open)
                return true;

            if (State != InventoryRobotPresentationState.Hidden)
                return false;

            SetVisualActive(true);
            ResolveBodyAnimator();
            _flightReady = false;
            _returnStarted = false;
            _closeRequested = false;
            _bodyReady = _bodyAnimator == null;
            SelectOpeningReaction();
            if (_bodyAnimator != null && !StartBodyOpening())
            {
                ForceHidden();
                return false;
            }
            SetState(InventoryRobotPresentationState.Opening);
            if (!_bodyReady)
                _bodyCoroutine = StartCoroutine(WaitForBodyOpening());

            if (!TrySetAnimatorTrigger(
                    _openTriggerName,
                    _closeTriggerName))
            {
                NotifyInventoryReady();
                if (_bodyAnimator == null)
                    TryCompleteOpening();
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

            if (State == InventoryRobotPresentationState.Opening)
            {
                _closeRequested = true;
                return true;
            }

            if (State != InventoryRobotPresentationState.Open)
                return false;

            StartClosing();
            return true;
        }

        private void StartClosing()
        {
            SetState(InventoryRobotPresentationState.Closing);
            _returnStarted = false;
            StopFallbackTimeout();
            if (CanSampleBodyState(_bodyCloseSourceState))
                _bodyCoroutine = StartCoroutine(PlayBodyClosing());
            else
            {
                if (_bodyAnimator != null)
                    Debug.LogWarning($"Cannot play closing source '{_bodyCloseSourceState}'; " +
                        "continuing the return animation.", this);
                BeginReturn();
            }

        }

        private void BeginReturn()
        {
            if (State != InventoryRobotPresentationState.Closing || _returnStarted)
                return;
            _returnStarted = true;

            if (_animator != null && _animator.isActiveAndEnabled &&
                _animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(_returnState) &&
                _animator.HasState(0, Animator.StringToHash(_returnState)))
            {
                ResetTriggerIfAssigned(_openTriggerName);
                ResetTriggerIfAssigned(_closeTriggerName);
                if (_animator.speed <= 0f) _animator.speed = 1f;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _animator.CrossFadeInFixedTime(_returnState, 0.05f, 0, 0f);
                StopFallbackTimeout();
                _fallbackCoroutine = StartCoroutine(WaitForReturn());
                return;
            }

            if (!TrySetAnimatorTrigger(
                    _closeTriggerName,
                    _openTriggerName))
            {
                NotifyRobotHidden();
                return;
            }

            StartFallbackTimeout(
                _closingTimeout,
                InventoryRobotPresentationState.Closing);

        }

        private IEnumerator WaitForReturn()
        {
            int closingHash = Animator.StringToHash(_returnState);
            bool enteredReturn = false;
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.05f, _closingTimeout);
            yield return null;
            while (State == InventoryRobotPresentationState.Closing && _returnStarted)
            {
                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
                    if (state.fullPathHash == closingHash)
                        enteredReturn = true;
                    if (!_animator.IsInTransition(0) && enteredReturn &&
                        (state.fullPathHash != closingHash || state.normalizedTime >= 1f))
                        break;
                }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning("Robot return did not finish in time; hiding the robot.", this);
                    break;
                }
                yield return null;
            }
            _fallbackCoroutine = null;
            NotifyRobotHidden();
        }

        private IEnumerator PlayBodyClosing() => PlayBodyClosingFromPose(_bodyCloseSourceState, 1f);

        private IEnumerator PlayBodyClosingFromPose(string sourceState, float startTime)
        {
            // Register the coroutine before sampling, and leave any Animator
            // callback which requested this transition before calling Update(0).
            yield return null;
            if (State != InventoryRobotPresentationState.Closing)
                yield break;
            if (!CanSampleBodyState(sourceState))
            {
                _bodyCoroutine = null;
                BeginReturn();
                yield break;
            }
            // Sample backwards explicitly: no negative Animator.speed or replayed events.
            SampleBodyPose(startTime, sourceState);
            // State length can be invalid on a paused Animator. Use the source
            // clip's authored duration, independent of Animator/state speed.
            float length = 0f;
            foreach (AnimatorClipInfo clipInfo in _bodyAnimator.GetCurrentAnimatorClipInfo(0))
            {
                if (clipInfo.clip != null)
                    length = Mathf.Max(length, clipInfo.clip.length);
            }
            if (length <= 0f || float.IsNaN(length) || float.IsInfinity(length))
                Debug.LogWarning("Could not read the closing source clip duration. " +
                    "Check Body Close Source State and its Motion clip.", this);
            float elapsed = 0f;
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, _bodyClosingTimeout);
            yield return null;
            while (State == InventoryRobotPresentationState.Closing && length > 0f &&
                   !float.IsNaN(length) && !float.IsInfinity(length))
            {
                if (!CanSampleBodyState(sourceState))
                    break;
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning("Robot body closing timed out; continuing the return animation.", this);
                    break;
                }
                elapsed += _bodyAnimator.updateMode == AnimatorUpdateMode.UnscaledTime
                    ? Time.unscaledDeltaTime : Time.deltaTime;
                float time = Mathf.Clamp01(startTime - elapsed * Mathf.Max(0.01f, _bodyCloseSpeed) / length);
                SampleBodyPose(time, sourceState);
                if (time <= 0f) break;
                yield return null;
            }
            if (CanSampleBodyState(sourceState))
                SampleBodyPose(0f, sourceState);
            _bodyCoroutine = null;
            BeginReturn();
        }

        // Animation Event: place at the frame where the inventory screen
        // has reached its final readable position.
        public void NotifyInventoryReady()
        {
            if (State != InventoryRobotPresentationState.Opening)
                return;

            StopFallbackTimeout();
            _flightReady = true;
        }

        private void TryCompleteOpening()
        {
            if (State != InventoryRobotPresentationState.Opening || !_flightReady || !_bodyReady)
                return;
            if (_refusalSelected)
            {
                // A refusal never reaches Open and never runs the reversed lid opening.
                SetState(InventoryRobotPresentationState.Closing);
                BeginReturn();
                return;
            }
            if (_closeRequested)
            {
                StartClosing();
                return;
            }
            SetState(InventoryRobotPresentationState.Open);
            OpenCompleted?.Invoke();
        }

        // Animation Event: place at the final frame of the closing clip.
        public void NotifyRobotHidden()
        {
            if (State != InventoryRobotPresentationState.Closing || !_returnStarted)
                return;

            bool rejected = _refusalSelected;
            _refusalSelected = false;
            _closeRequested = false;
            StopFallbackTimeout();
            StopBodyOpening();
            // This can be called by a flight Animation Event. Do not evaluate
            // the nested body Animator from inside that event. Closing already
            // sampled frame zero; the next opening explicitly starts at zero.
            SetVisualActive(false);
            SetState(InventoryRobotPresentationState.Hidden);
            CloseCompleted?.Invoke();
            if (rejected)
                OpenRejected?.Invoke();
        }

        public void ForceHidden()
        {
            StopFallbackTimeout();
            StopBodyOpening();
            _flightReady = false;
            _bodyReady = false;
            _returnStarted = false;
            _refusalSelected = false;
            _closeRequested = false;

            if (_animator != null)
            {
                ResetTriggerIfAssigned(_openTriggerName);
                ResetTriggerIfAssigned(_closeTriggerName);
            }

            SetVisualActive(false);
            SetState(InventoryRobotPresentationState.Hidden);
        }

        private void ResolveBodyAnimator()
        {
            if (_bodyAnimator != null) return;
            foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
            {
                if (candidate != _animator && candidate.runtimeAnimatorController != null &&
                    candidate.runtimeAnimatorController.name == "RuviBody")
                {
                    _bodyAnimator = candidate;
                    break;
                }
            }
        }

        private bool StartBodyOpening()
        {
            if (_bodyAnimator == _animator || !_bodyAnimator.isActiveAndEnabled ||
                _bodyAnimator.runtimeAnimatorController == null ||
                string.IsNullOrWhiteSpace(_activeBodyOpenState) ||
                !_bodyAnimator.HasState(0, Animator.StringToHash(_activeBodyOpenState)))
            {
                Debug.LogError("Robot body Animator must be separate, active and contain " +
                    $"'{_activeBodyOpenState}' on layer 0. Inventory opening was cancelled.", this);
                return false;
            }
            _bodyAnimator.applyRootMotion = false;
            _bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Opening and closing both own clip time. Do not leave the Animator
            // running independently between explicit pose evaluations.
            _bodyAnimator.speed = 0f;
            _bodyAnimator.Play(_activeBodyOpenState, 0, 0f);
            return true;
        }

        private void SelectOpeningReaction()
        {
            // Legacy robots without a body Animator retain their old opening path.
            CurrentReaction = _bodyAnimator == null ? InventoryRobotReaction.Open1 :
                _reactionOverride == InventoryRobotReaction.Random
                    ? InventoryRobotReactionSelector.Select(UnityEngine.Random.Range(0, InventoryRobotReactionSelector.RollCount))
                    : _reactionOverride;
            _refusalSelected = CurrentReaction == InventoryRobotReaction.FuckOff;
            switch (CurrentReaction)
            {
                case InventoryRobotReaction.Open2: _activeBodyOpenState = _bodyOpen2State; break;
                case InventoryRobotReaction.Open3: _activeBodyOpenState = _bodyOpen3State; break;
                case InventoryRobotReaction.FuckOff: _activeBodyOpenState = _bodyRefusalState; break;
                default: _activeBodyOpenState = _bodyOpenState; break;
            }
        }

        private IEnumerator WaitForBodyOpening()
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, _bodyOpeningTimeout);
            float elapsed = 0f;
            float length = 0f;
            // Leave the activation/Play call stack before evaluating the pose.
            yield return null;
            while (State == InventoryRobotPresentationState.Opening)
            {
                float deltaTime = _bodyAnimator != null &&
                    _bodyAnimator.updateMode == AnimatorUpdateMode.UnscaledTime
                    ? Time.unscaledDeltaTime : Time.deltaTime;
                if (AdvanceBodyOpening(deltaTime, ref elapsed, ref length))
                {
                    _bodyCoroutine = null;
                    _bodyReady = true;
                    yield break;
                }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    _bodyCoroutine = null;
                    Debug.LogWarning($"Robot body opening '{_activeBodyOpenState}' did not complete " +
                        $"(clip length {length:F3}s, played {elapsed:F3}s, " +
                        $"body active: {_bodyAnimator != null && _bodyAnimator.isActiveAndEnabled}). " +
                        "Inventory opening was cancelled and player controls were released.", this);
                    ForceHidden();
                    // The input controller already uses this event to release its control lock.
                    CloseCompleted?.Invoke();
                    yield break;
                }
                yield return null;
            }
            _bodyCoroutine = null;
        }

        private bool AdvanceBodyOpening(float deltaTime, ref float elapsed, ref float length)
        {
            if (!CanSampleBodyState(_activeBodyOpenState))
                return false;
            if (length <= 0f)
            {
                SampleBodyPose(0f);
                foreach (AnimatorClipInfo clipInfo in _bodyAnimator.GetCurrentAnimatorClipInfo(0))
                    if (clipInfo.clip != null)
                        length = Mathf.Max(length, clipInfo.clip.length);
            }
            if (length <= 0f || float.IsNaN(length) || float.IsInfinity(length))
                return false;
            elapsed += Mathf.Max(0f, deltaTime) * Mathf.Max(0.01f, _bodyOpenSpeed);
            float progress = Mathf.Clamp01(elapsed / length);
            // Reapply state and time even if activation reset the nested Animator.
            SampleBodyPose(progress);
            return progress >= 1f;
        }

        private void SampleBodyPose(float normalizedTime, string stateName = null)
        {
            bool eventsEnabled = _bodyAnimator.fireEvents;
            _bodyAnimator.fireEvents = false;
            try
            {
                // Play queues a seek. Evaluate it while playback is enabled,
                // then freeze the resulting pose; seeking at speed 0 can leave
                // the previous pose/state cached by the Animator.
                _bodyAnimator.speed = 1f;
                _bodyAnimator.Play(stateName ?? _activeBodyOpenState ?? _bodyOpenState, 0, normalizedTime);
                _bodyAnimator.Update(0f);
                _bodyAnimator.speed = 0f;
            }
            finally { _bodyAnimator.fireEvents = eventsEnabled; }
        }

        private bool CanSampleBodyState(string stateName) =>
            _bodyAnimator != null && _bodyAnimator != _animator &&
            _bodyAnimator.isActiveAndEnabled && _bodyAnimator.runtimeAnimatorController != null &&
            !string.IsNullOrWhiteSpace(stateName) &&
            _bodyAnimator.HasState(0, Animator.StringToHash(stateName));

        private void StopBodyOpening()
        {
            if (_bodyCoroutine == null) return;
            StopCoroutine(_bodyCoroutine);
            _bodyCoroutine = null;
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
            bool wasVisible = State != InventoryRobotPresentationState.Hidden;
            StopFallbackTimeout();
            StopBodyOpening();
            _flightReady = false;
            _bodyReady = false;
            _returnStarted = false;
            _refusalSelected = false;
            _closeRequested = false;
            // Do not sample Animator poses while Unity is disabling a hierarchy.
            // The next opening explicitly starts its clip at zero.
            SetState(InventoryRobotPresentationState.Hidden);
            SetVisualActive(false);
            if (wasVisible)
                CloseCompleted?.Invoke();
        }
    }
}
