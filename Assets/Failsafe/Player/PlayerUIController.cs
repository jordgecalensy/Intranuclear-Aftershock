using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Failsafe.Player.UI
{
    public class PlayerUIController : MonoBehaviour
    {
        [Header("Здоровье и Стамина")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private Slider[] _staminaSegments;
        [SerializeField] private float _healthCurvePower = 1.5f;

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
            float ratio = Mathf.Clamp01(current / max);
            _healthSlider.value = 1f - Mathf.Pow(1f - ratio, _healthCurvePower);
        }

        public void UpdateStaminaUI(float current, float max)
        {
            float total = Mathf.Clamp01(current / max);
            for (int i = 0; i < _staminaSegments.Length; i++)
                _staminaSegments[i].value = Mathf.Clamp01((total - i * 0.25f) / 0.25f);
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
    }
}