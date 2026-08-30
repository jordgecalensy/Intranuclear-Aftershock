using Failsafe.Scripts.EffectSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Failsafe.Player.UI
{
    public sealed class PlayerEffectHudEntryView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _durationFillImage;
        [SerializeField] private TMP_Text _stageText;

        private EffectPresentation _presentation;
        private Vector2 _targetPosition;
        private Vector2 _slotPosition;
        private AnimationState _animationState;
        private bool _initialized;

        public RectTransform RectTransform => _rectTransform;
        public EffectPresentation Presentation => _presentation;
        public bool IsExiting => _animationState == AnimationState.Exiting;

        public void Initialize()
        {
            if (_initialized)
                return;

            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;

            _initialized = true;
        }

        public void Bind(
            EffectPresentation presentation,
            Vector2 startPosition,
            Vector2 entryPosition,
            Vector2 slotPosition)
        {
            Initialize();

            _presentation = presentation;
            _targetPosition = entryPosition;
            _slotPosition = slotPosition;
            _animationState = AnimationState.Entering;

            _rectTransform.anchoredPosition = startPosition;
            gameObject.SetActive(true);

            UpdateIconAndDurationColor();
            UpdatePresentation();
        }

        public void Refresh(EffectPresentation presentation)
        {
            if (_presentation != presentation)
                return;

            UpdatePresentation();
        }

        public void SetTargetPosition(Vector2 targetPosition)
        {
            if (IsExiting)
                return;

            _slotPosition = targetPosition;

            if (_animationState == AnimationState.Dropping ||
                _animationState == AnimationState.Active)
            {
                _targetPosition = targetPosition;
            }
        }

        public void BeginExit(float targetX)
        {
            if (_presentation == null || IsExiting)
                return;

            _animationState = AnimationState.Exiting;
            _targetPosition = new Vector2(
                targetX,
                _rectTransform.anchoredPosition.y);
        }

        public bool Tick(
            float deltaTime,
            float enterSpeed,
            float dropSpeed,
            float rearrangeSpeed,
            float exitSpeed)
        {
            if (_presentation == null)
                return false;

            UpdatePresentation();

            switch (_animationState)
            {
                case AnimationState.Entering:
                    if (MoveTowardsTarget(deltaTime, enterSpeed))
                    {
                        _animationState = AnimationState.Dropping;
                        _targetPosition = _slotPosition;

                        if (HasReachedTarget())
                            _animationState = AnimationState.Active;
                    }

                    return false;

                case AnimationState.Dropping:
                    if (MoveTowardsTarget(deltaTime, dropSpeed))
                        _animationState = AnimationState.Active;

                    return false;

                case AnimationState.Active:
                    MoveTowardsTarget(deltaTime, rearrangeSpeed);
                    return false;

                case AnimationState.Exiting:
                    return MoveTowardsTarget(deltaTime, exitSpeed);

                default:
                    return false;
            }
        }

        private bool MoveTowardsTarget(float deltaTime, float speed)
        {
            _rectTransform.anchoredPosition = Vector2.MoveTowards(
                _rectTransform.anchoredPosition,
                _targetPosition,
                Mathf.Max(0f, speed) * deltaTime);

            return HasReachedTarget();
        }

        private bool HasReachedTarget()
        {
            return Vector2.SqrMagnitude(
                _rectTransform.anchoredPosition - _targetPosition) <= 0.01f;
        }

        public void Release(Vector2 restingPosition)
        {
            _presentation = null;
            _targetPosition = restingPosition;
            _slotPosition = restingPosition;
            _animationState = AnimationState.Inactive;

            if (_rectTransform != null)
                _rectTransform.anchoredPosition = restingPosition;

            gameObject.SetActive(false);
        }

        private void UpdatePresentation()
        {
            if (_presentation == null)
                return;

            if (_durationFillImage != null)
            {
                bool hasFiniteDuration =
                    !float.IsPositiveInfinity(_presentation.AppliedDuration);

                _durationFillImage.enabled = hasFiniteDuration;

                if (hasFiniteDuration)
                    _durationFillImage.fillAmount = _presentation.NormalizedRemaining;
            }

            if (_stageText != null)
            {
                int stage = _presentation.Stage;
                _stageText.enabled = stage > 0;

                if (stage > 0)
                    _stageText.text = stage.ToString();
            }
        }

        private void UpdateIconAndDurationColor()
        {
            Sprite icon = _presentation.Definition.HudIcon;

            if (_iconImage != null)
                _iconImage.sprite = icon;

            if (_durationFillImage == null)
                return;

            _durationFillImage.color =
                _presentation.Definition.HudDurationColor;
        }

        private enum AnimationState
        {
            Inactive,
            Entering,
            Dropping,
            Active,
            Exiting
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;
        }
#endif
    }
}
