using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Failsafe.Player.UI
{
    public class PlayerUIController : MonoBehaviour
    {
        [Header("Health & Stamina")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private Slider[] _staminaSegments;
        [SerializeField] private float _healthCurvePower = 1.5f;

        [Header("Crosshair Sprites")]
        [SerializeField] private Sprite _whiteCircle;
        [SerializeField] private Sprite _whiteTriangle;
        [SerializeField] private Sprite _whiteCrosshair;

        [Header("Crosshair Settings")]
        [SerializeField] private Image _mainCursorImage; // Объект Crosshair из префаба [cite: 71]
        [SerializeField] private Color _orangeColor = new Color(1f, 0.45f, 0f);

        [Header("Awareness Cubes")]
        [SerializeField] private Image[] _awarenessCubes;
        private float _lastAwareness;
        private Coroutine _blinkCoroutine;

        public void UpdateCursorVisual(bool hasItem, bool isInteractable, bool isConsole, bool isEnemy)
        {
            // Сбрасываем в белый по умолчанию
            _mainCursorImage.color = Color.white;

            if (isConsole)
            {
                _mainCursorImage.sprite = _whiteTriangle;
                return;
            }

            if (hasItem)
            {
                _mainCursorImage.sprite = _whiteCrosshair;
                // Красим в оранжевый, если это враг или предмет
                if (isEnemy || isInteractable) _mainCursorImage.color = _orangeColor;
            }
            else
            {
                _mainCursorImage.sprite = _whiteCircle;
                // Красим в оранжевый, если это предмет или враг
                if (isInteractable || isEnemy) _mainCursorImage.color = _orangeColor;
            }
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