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
        private bool _isExiting;
        private bool _initialized;
        private Color _defaultDurationColor = Color.white;

        public RectTransform RectTransform => _rectTransform;
        public EffectPresentation Presentation => _presentation;
        public bool IsExiting => _isExiting;

        public void Initialize()
        {
            if (_initialized)
                return;

            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;

            if (_durationFillImage != null)
                _defaultDurationColor = _durationFillImage.color;

            _initialized = true;
        }

        public void Bind(
            EffectPresentation presentation,
            Vector2 slotPosition)
        {
            Initialize();

            _presentation = presentation;
            _targetPosition = slotPosition;
            _isExiting = false;

            _rectTransform.anchoredPosition = slotPosition;
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
            if (!_isExiting)
                _targetPosition = targetPosition;
        }

        public void BeginExit(float targetX)
        {
            if (_presentation == null || _isExiting)
                return;

            _isExiting = true;
            _targetPosition = new Vector2(
                targetX,
                _rectTransform.anchoredPosition.y);
        }

        public bool Tick(
            float deltaTime,
            float rearrangeSpeed,
            float exitSpeed)
        {
            if (_presentation == null)
                return false;

            UpdatePresentation();

            float speed = _isExiting
                ? exitSpeed
                : rearrangeSpeed;

            _rectTransform.anchoredPosition = Vector2.MoveTowards(
                _rectTransform.anchoredPosition,
                _targetPosition,
                Mathf.Max(0f, speed) * deltaTime);

            return _isExiting &&
                   Vector2.SqrMagnitude(
                       _rectTransform.anchoredPosition - _targetPosition) <= 0.01f;
        }

        public void Release(Vector2 restingPosition)
        {
            _presentation = null;
            _targetPosition = restingPosition;
            _isExiting = false;

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

            _durationFillImage.color = _defaultDurationColor;

            if (SpriteAccentColorUtility.TryGetBottomEdgeColor(
                    icon,
                    out Color accentColor))
            {
                _durationFillImage.color = accentColor;
            }
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
