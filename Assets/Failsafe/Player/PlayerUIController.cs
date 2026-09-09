using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace Failsafe.Player.UI
{
    public class PlayerUIController : MonoBehaviour
    {
        [Header("Legacy: здоровье и стамина")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private Slider[] _staminaSegments;
        [SerializeField] private float _healthCurvePower = 1.5f;

        [Header("Новый HUD: здоровье")]
        [SerializeField] private TMP_Text _healthValueText;
        [SerializeField, Min(1f)] private float _neutralHealthValue = 100f;
        [SerializeField] private Color _criticalHealthColor = new Color(1f, 0.05f, 0f, 1f);
        [SerializeField] private Color _normalHealthColor = new Color(1f, 0.35f, 0f, 1f);
        [SerializeField] private Color _bonusHealthColor = new Color(0f, 0.85f, 0.55f, 1f);

        [Header("Новый HUD: стамина")]
        [Tooltip("Сегменты основной стамины в порядке сверху вниз. Каждый сегмент должен содержать Image с именем Fill.")]
        [SerializeField] private Image[] _baseStaminaSegments;
        [SerializeField] private Color _availableStaminaColor = new Color(1f, 0.35f, 0f, 1f);
        [SerializeField] private Color _spentStaminaColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [Tooltip("Корень основной колонки. Его высота используется как масштаб для бонусной стамины.")]
        [SerializeField] private RectTransform _baseStaminaRoot;
        [SerializeField] private GameObject _bonusStaminaRoot;
        [Tooltip("Image должен иметь Type = Filled, Fill Method = Vertical, Origin = Bottom.")]
        [SerializeField] private Image _bonusStaminaFill;
        private readonly List<GameObject> _bonusStaminaSegments = new();
        private readonly List<Image> _bonusStaminaFills = new();
        private float _bonusStaminaFullSegmentHeight;

        [Header("Новый HUD: шум")]
        [Tooltip("Заливка одного квадрата для низкого уровня шума.")]
        [SerializeField] private Image[] _lowNoiseFills;
        [Tooltip("Заливки двух квадратов для среднего уровня шума.")]
        [SerializeField] private Image[] _mediumNoiseFills;
        [Tooltip("Заливки трёх квадратов для высокого уровня шума.")]
        [SerializeField] private Image[] _highNoiseFills;
        [Tooltip("Старый общий массив. Используется, только если новые группы не настроены.")]
        [SerializeField] private Image[] _noiseSegments;
        [SerializeField] private Color _activeNoiseColor = new Color(1f, 0.35f, 0f, 1f);
        [SerializeField] private Color _inactiveNoiseColor = new Color(1f, 0.35f, 0f, 0f);

        [Header("Базовые Спрайты")]
        [SerializeField] private Sprite _whiteCircle;
        [SerializeField] private Sprite _whiteTriangle;
        [SerializeField] private Sprite _whiteCrosshair;

        [Header("Настройки Прицела")]
        [SerializeField] private Image _mainCursorImage;
        [SerializeField] private float _scaleSpeed = 8f; //
        private float _targetScale = 1f;

        [Header("Awareness (Кубики)")]
        [SerializeField] private Image[] _awarenessCubes;
        private float _lastAwareness;
        private Coroutine _blinkCoroutine;

        private void Update()
        {
            // Плавное изменение масштаба RectTransform
            float current = _mainCursorImage.rectTransform.localScale.x;
            float next = Mathf.Lerp(current, _targetScale, Time.deltaTime * _scaleSpeed);
            _mainCursorImage.rectTransform.localScale = new Vector3(next, next, 1f);
        }

        public void SetTargetScale(float scale) => _targetScale = scale;

        public void UpdateCursorVisual(bool hasItem, bool isInteractable, bool isConsole, bool isEnemy)
        {
            // Оранжевый если враг или предмет, иначе белый
            _mainCursorImage.color = (isInteractable || isEnemy) ? new Color(1f, 0.45f, 0f) : Color.white;

            if (isConsole)
            {
                _mainCursorImage.sprite = _whiteTriangle;
                _mainCursorImage.color = Color.white; // Консоль всегда белая
                return;
            }

            // Выбор спрайта в зависимости от наличия предмета
            _mainCursorImage.sprite = hasItem ? _whiteCrosshair : _whiteCircle;
        }

        public void UpdateHealthUI(float current, float max)
        {
            float safeMax = Mathf.Max(1f, max);
            float safeCurrent = Mathf.Max(0f, current);
            float ratio = Mathf.Clamp01(safeCurrent / safeMax);

            if (_healthSlider != null)
                _healthSlider.value = 1f - Mathf.Pow(1f - ratio, _healthCurvePower);

            if (_healthValueText == null)
                return;

            _healthValueText.text = Mathf.CeilToInt(safeCurrent).ToString();
            _healthValueText.color = EvaluateHealthColor(safeCurrent, safeMax);
        }

        public void UpdateStaminaUI(float current, float max)
        {
            UpdateStaminaUI(current, max, max);
        }

        public void UpdateStaminaUI(float current, float max, float baseMax)
        {
            float safeMax = Mathf.Max(1f, max);
            float safeCurrent = Mathf.Max(0f, current);
            float total = Mathf.Clamp01(safeCurrent / safeMax);

            if (_staminaSegments != null && _staminaSegments.Length > 0)
            {
                float legacySegmentSize = 1f / _staminaSegments.Length;

                for (int i = 0; i < _staminaSegments.Length; i++)
                {
                    if (_staminaSegments[i] == null)
                        continue;

                    _staminaSegments[i].value = Mathf.Clamp01(
                        (total - i * legacySegmentSize) / legacySegmentSize);
                }
            }

            UpdateBaseStaminaSegments(safeCurrent, baseMax);
            UpdateBonusStamina(safeCurrent, safeMax, baseMax);
        }

        public void UpdateNoiseUI(
            float strength,
            float crouchStrength,
            float walkStrength,
            float sprintStrength)
        {
            int activeGroup = 0;

            if (strength >= crouchStrength - 0.01f)
                activeGroup = 1;
            if (strength >= walkStrength - 0.01f)
                activeGroup = 2;
            if (strength >= sprintStrength - 0.01f)
                activeGroup = 3;

            if (HasConfiguredNoiseGroups())
            {
                SetNoiseGroupActive(_lowNoiseFills, activeGroup == 1);
                SetNoiseGroupActive(_mediumNoiseFills, activeGroup == 2);
                SetNoiseGroupActive(_highNoiseFills, activeGroup == 3);
                return;
            }

            if (_noiseSegments == null)
                return;

            for (int i = 0; i < _noiseSegments.Length; i++)
            {
                if (_noiseSegments[i] != null)
                    _noiseSegments[i].color = i < activeGroup
                        ? _activeNoiseColor
                        : _inactiveNoiseColor;
            }
        }

        private bool HasConfiguredNoiseGroups()
        {
            return HasImage(_lowNoiseFills)
                || HasImage(_mediumNoiseFills)
                || HasImage(_highNoiseFills);
        }

        private static bool HasImage(Image[] images)
        {
            if (images == null)
                return false;

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                    return true;
            }

            return false;
        }

        private void SetNoiseGroupActive(Image[] group, bool isActive)
        {
            if (group == null)
                return;

            Color color = isActive ? _activeNoiseColor : _inactiveNoiseColor;

            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] != null)
                    group[i].color = color;
            }
        }

        public void UpdateAwarenessUI(float value)
        {
            bool growing = value > _lastAwareness + 0.01f;
            _lastAwareness = value;
            
            _awarenessCubes[0].gameObject.SetActive(value > 0);
            _awarenessCubes[1].gameObject.SetActive(value >= 25);
            _awarenessCubes[2].gameObject.SetActive(value >= 100);

            if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);
            if (growing)
            {
                int idx = value >= 100 ? 2 : (value >= 25 ? 1 : 0);
                _blinkCoroutine = StartCoroutine(BlinkRoutine(_awarenessCubes[idx], value >= 100 ? 10f : 5f));
            }
            else ResetAlphas();
        }

        private IEnumerator BlinkRoutine(Image img, float s)
        {
            while (true)
            {
                float a = (Mathf.Sin(Time.time * s) + 1f) / 2f;
                img.color = new Color(img.color.r, img.color.g, img.color.b, a);
                yield return null;
            }
        }

        private void ResetAlphas() { foreach (var c in _awarenessCubes) c.color = Color.white; }

        private Color EvaluateHealthColor(float current, float max)
        {
            float neutral = Mathf.Max(1f, _neutralHealthValue);

            if (current <= neutral)
            {
                float normalProgress = Mathf.Clamp01(current / neutral);
                return Color.Lerp(_criticalHealthColor, _normalHealthColor, normalProgress);
            }

            float bonusRange = Mathf.Max(1f, max - neutral);
            float bonusProgress = Mathf.Clamp01((current - neutral) / bonusRange);
            return Color.Lerp(_normalHealthColor, _bonusHealthColor, bonusProgress);
        }

        private void UpdateBaseStaminaSegments(float current, float baseMax)
        {
            if (_baseStaminaSegments == null || _baseStaminaSegments.Length == 0)
                return;

            float safeBaseMax = Mathf.Max(1f, baseMax);
            float currentBaseStamina = Mathf.Clamp(current, 0f, safeBaseMax);
            float staminaPerSegment = safeBaseMax / _baseStaminaSegments.Length;

            for (int i = 0; i < _baseStaminaSegments.Length; i++)
            {
                Image fill = ResolveStaminaFill(_baseStaminaSegments[i]);
                if (fill == null)
                    continue;

                int segmentsBelow = _baseStaminaSegments.Length - i - 1;
                float staminaBelow = segmentsBelow * staminaPerSegment;
                fill.fillAmount = Mathf.Clamp01(
                    (currentBaseStamina - staminaBelow) / staminaPerSegment);
            }
        }

        private static Image ResolveStaminaFill(Image segment)
        {
            if (segment == null)
                return null;

            Transform fillTransform = segment.transform.Find("Fill");
            return fillTransform != null && fillTransform.TryGetComponent(out Image fill)
                ? fill
                : segment;
        }

        private void UpdateBonusStamina(float current, float max, float baseMax)
        {
            float safeBaseMax = Mathf.Max(1f, baseMax);
            float bonusCapacity = Mathf.Max(0f, max - safeBaseMax);
            bool hasBonus = bonusCapacity > 0.01f;

            if (_bonusStaminaRoot == null || _bonusStaminaFill == null)
                return;

            if (!hasBonus)
            {
                if (SetBonusStaminaSegmentCount(0))
                    MarkStaminaLayoutForRebuild();

                return;
            }

            int baseSegmentCount = _baseStaminaSegments != null
                ? _baseStaminaSegments.Length
                : 0;
            float staminaPerSegment = safeBaseMax / Mathf.Max(1, baseSegmentCount);
            int requiredSegments = Mathf.Max(
                1,
                Mathf.CeilToInt(bonusCapacity / staminaPerSegment - 0.0001f));

            bool layoutChanged = EnsureBonusStaminaSegments(requiredSegments);
            layoutChanged |= SetBonusStaminaSegmentCount(requiredSegments);

            float currentBonus = Mathf.Clamp(current - safeBaseMax, 0f, bonusCapacity);
            float topSegmentCapacity = Mathf.Clamp(
                bonusCapacity - (requiredSegments - 1) * staminaPerSegment,
                0.0001f,
                staminaPerSegment);

            for (int i = 0; i < requiredSegments; i++)
            {
                float segmentCapacity = i == 0
                    ? topSegmentCapacity
                    : staminaPerSegment;
                float capacityBelow = (requiredSegments - i - 1) * staminaPerSegment;

                if (i < _bonusStaminaFills.Count && _bonusStaminaFills[i] != null)
                {
                    _bonusStaminaFills[i].fillAmount = Mathf.Clamp01(
                        (currentBonus - capacityBelow) / segmentCapacity);
                }

                if (i < _bonusStaminaSegments.Count)
                {
                    float heightRatio = segmentCapacity / staminaPerSegment;
                    layoutChanged |= SetBonusStaminaSegmentHeight(
                        _bonusStaminaSegments[i],
                        heightRatio);
                }
            }

            if (layoutChanged)
                MarkStaminaLayoutForRebuild();
        }

        private bool EnsureBonusStaminaSegments(int requiredSegments)
        {
            bool changed = false;

            if (_bonusStaminaSegments.Count == 0)
            {
                _bonusStaminaSegments.Add(_bonusStaminaRoot);
                _bonusStaminaFills.Add(_bonusStaminaFill);
                _bonusStaminaFullSegmentHeight = GetBonusStaminaSegmentHeight(
                    _bonusStaminaRoot);
                changed = true;
            }

            Transform parent = _bonusStaminaRoot.transform.parent;
            int firstSiblingIndex = _bonusStaminaRoot.transform.GetSiblingIndex();

            while (_bonusStaminaSegments.Count < requiredSegments)
            {
                GameObject segment = Instantiate(_bonusStaminaRoot, parent);
                int segmentNumber = _bonusStaminaSegments.Count + 1;
                segment.name = $"BonusStaminaSegment_{segmentNumber:00}";
                segment.transform.SetSiblingIndex(
                    firstSiblingIndex + _bonusStaminaSegments.Count);

                Image fill = FindBonusStaminaFill(segment);
                _bonusStaminaSegments.Add(segment);
                _bonusStaminaFills.Add(fill);
                changed = true;
            }

            return changed;
        }

        private bool SetBonusStaminaSegmentCount(int activeSegments)
        {
            bool changed = false;

            if (_bonusStaminaSegments.Count == 0)
            {
                bool shouldBeActive = activeSegments > 0;
                if (_bonusStaminaRoot.activeSelf != shouldBeActive)
                {
                    _bonusStaminaRoot.SetActive(shouldBeActive);
                    changed = true;
                }

                return changed;
            }

            for (int i = 0; i < _bonusStaminaSegments.Count; i++)
            {
                if (_bonusStaminaSegments[i] == null)
                    continue;

                bool shouldBeActive = i < activeSegments;
                if (_bonusStaminaSegments[i].activeSelf == shouldBeActive)
                    continue;

                _bonusStaminaSegments[i].SetActive(shouldBeActive);
                changed = true;
            }

            return changed;
        }

        private Image FindBonusStaminaFill(GameObject segment)
        {
            Transform fillTransform = segment.transform.Find(
                _bonusStaminaFill.gameObject.name);

            return fillTransform != null
                ? fillTransform.GetComponent<Image>()
                : null;
        }

        private float GetBonusStaminaSegmentHeight(GameObject segment)
        {
            LayoutElement layoutElement = segment.GetComponent<LayoutElement>();
            if (layoutElement != null && layoutElement.preferredHeight > 0f)
                return layoutElement.preferredHeight;

            return segment.transform is RectTransform rectTransform
                ? Mathf.Max(1f, rectTransform.rect.height)
                : 1f;
        }

        private bool SetBonusStaminaSegmentHeight(
            GameObject segment,
            float heightRatio)
        {
            float targetHeight = Mathf.Max(
                1f,
                _bonusStaminaFullSegmentHeight * Mathf.Clamp01(heightRatio));
            LayoutElement layoutElement = segment.GetComponent<LayoutElement>();

            if (layoutElement != null)
            {
                if (!Mathf.Approximately(layoutElement.preferredHeight, targetHeight))
                {
                    layoutElement.preferredHeight = targetHeight;
                    return true;
                }

                return false;
            }

            if (segment.transform is RectTransform rectTransform
                && !Mathf.Approximately(rectTransform.rect.height, targetHeight))
            {
                rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    targetHeight);
                return true;
            }

            return false;
        }

        private void MarkStaminaLayoutForRebuild()
        {
            if (_bonusStaminaRoot != null
                && _bonusStaminaRoot.transform.parent is RectTransform parent)
            {
                LayoutRebuilder.MarkLayoutForRebuild(parent);
            }
        }

        // Оставлено для совместимости со старой односегментной разметкой.
        private void UpdateBonusStaminaHeight(float bonusCapacity, float baseMax)
        {
            if (_baseStaminaRoot == null || _bonusStaminaRoot == null)
                return;

            if (_bonusStaminaRoot.transform is not RectTransform bonusRectTransform)
                return;

            float baseHeight = _baseStaminaRoot.rect.height;
            if (baseHeight <= 0.01f)
                return;

            float targetHeight = baseHeight * bonusCapacity / baseMax;
            LayoutElement layoutElement = _bonusStaminaRoot.GetComponent<LayoutElement>();

            if (layoutElement != null)
            {
                if (!Mathf.Approximately(layoutElement.preferredHeight, targetHeight))
                    layoutElement.preferredHeight = targetHeight;
            }
            else if (!Mathf.Approximately(bonusRectTransform.rect.height, targetHeight))
            {
                bonusRectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    targetHeight);
            }

            if (bonusRectTransform.parent is RectTransform parent)
                LayoutRebuilder.MarkLayoutForRebuild(parent);
        }
    }
}
