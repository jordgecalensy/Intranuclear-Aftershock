using UnityEngine;

namespace Failsafe.Player.View
{
    /// <summary>
    /// Сдвигает объект (оружие) назад при приближении к стене.
    /// Рекомендуется вешать на родительский контейнер оружия (Weapon Root).
    /// </summary>
    public class WeaponWallRetractor : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Слой стен/препятствий")]
        [SerializeField] private LayerMask _obstacleMask;

        [Tooltip("Точка начала луча (обычно Камера)")]
        [SerializeField] private Transform _originPoint; // Обычно камера

        [Tooltip("Длина оружия (дистанция проверки)")]
        [SerializeField] private float _weaponLength = 0.7f;

        [Tooltip("Максимальное смещение назад (в метрах)")]
        [SerializeField] private float _maxRetraction = 0.5f;

        [Tooltip("Скорость сглаживания движения")]
        [SerializeField] private float _smoothTime = 0.1f;

        private Vector3 _initialLocalPos;
        private Vector3 _currentVelocity; // Для SmoothDamp

        private void Start()
        {
            _initialLocalPos = transform.localPosition;

            if (_originPoint == null && Camera.main != null)
            {
                _originPoint = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            if (_originPoint == null) return;

            // 1. Расчет целевой позиции
            Vector3 targetPos = _initialLocalPos;
            
            // Луч стреляет из камеры (глаз) вперед
            if (Physics.Raycast(_originPoint.position, _originPoint.forward, out RaycastHit hit, _weaponLength, _obstacleMask))
            {
                // Считаем отступ. Чем ближе стена, тем больше retactAmount.
                // Формула: (Длина - Расстояние до стены)
                float penetration = _weaponLength - hit.distance;
                
                // Ограничиваем смещение, чтобы оружие не ушло за спину
                float retraction = Mathf.Clamp(penetration, 0f, _maxRetraction);

                // Смещаем назад по локальной оси Z (Vector3.back)
                targetPos = _initialLocalPos + (Vector3.right * retraction);
            }

            // 2. Применение сглаживания (SmoothDamp лучше Lerp для физических движений)
            transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetPos, ref _currentVelocity, _smoothTime);
        }

        private void OnDrawGizmosSelected()
        {
            if (_originPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(_originPoint.position, _originPoint.forward * _weaponLength);
            }
        }
    }
}