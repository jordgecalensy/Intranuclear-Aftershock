using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Failsafe.Player.Scripts.Interaction
{
    /// <summary>Управление физикой объекта при захвате, перемещении и вставке</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsController : MonoBehaviour
    {
        private Rigidbody _rb;
        private Transform _grabPoint;
        private bool _fixRotation;
        private PhysicsInteraction _physicsInteraction;
        private int _carryingLayerIndex = 8; // слой для рейкаста
        private float _rotKp = 500f;   // коэффициент P для удержания поворота
        private float _rotKd = 50f;    // коэффициент D для демпфирования
        private float _toTargetSpeed = 4f;
        private int _cachedCarryingLayer;
        private Vector3 _grabHelperVector = new Vector3(0f, 0.1f, 0f); // смещение при захвате
        private bool _occupied = false;
        private bool _isInserted = false;
        public bool IsInserted => _isInserted;
        public bool IsGrabbed => _grabPoint != null;



        /// <summary>Получить или добавить контроллер на объект</summary>
        public static PhysicsController Create(GameObject parent)
        {
            var controller = parent.GetComponent<PhysicsController>();
            if (controller != null) return controller;
            controller = parent.AddComponent<PhysicsController>();
            return controller;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (_grabPoint != null)
            {
                DragObject();
                if (_fixRotation)
                {
                    RotationHold(_grabPoint.rotation, _rotKp, _rotKd);
                }
            }
        }

        private void EnablePhysics()
        {
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
            }
        }

        private void DisablePhysics()
        {
            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
            }
        }

        /// <summary>Тянет объект к точке захвата</summary>
        private void DragObject()
            {
                Vector3 toTarget = _grabPoint.position - transform.position;
                _rb.linearVelocity = toTarget * _toTargetSpeed;
            }

        /// <summary>Захватывает объект в точке grabPoint. Возвращает false, если объект занят</summary>
        public bool Grab(Transform grabPoint, PhysicsInteraction physicsInteraction, bool fixRotation = false)
        {
            if (_occupied) return false;
            _grabPoint = grabPoint;
            _physicsInteraction = physicsInteraction;
            _rb.useGravity = false;
            // хак для предотвращения залипания на поверхности
            _rb.transform.position += _grabHelperVector;
            _cachedCarryingLayer = gameObject.layer;
            gameObject.layer = _carryingLayerIndex;
            _rb.angularVelocity = Vector3.zero;
            _fixRotation = fixRotation;
            return true;
        }

        /// <summary>Отпускает объект, возвращает слой и гравитацию</summary>
        public void Release()
        {
            _grabPoint = null;
            _physicsInteraction?.Released();
            _physicsInteraction = null;
            _rb.useGravity = true;
            gameObject.layer = _cachedCarryingLayer;
        }

        /// <summary>Бросает объект с заданной силой</summary>
        public void Throw(float throwForce, float throwTorque, Transform direction)
        {
            _grabPoint = null;
            _physicsInteraction?.Released();
            _physicsInteraction = null;
            _rb.useGravity = true;
            gameObject.layer = _cachedCarryingLayer;
            // _rb.AddForce(_rb.transform.forward * throwForceMultiplier, ForceMode.VelocityChange);
            // CarryingBody.useGravity = true;

            _rb.AddForce(
                direction.forward * throwForce,
                ForceMode.Impulse
            );

            _rb.AddTorque(
                direction.forward * throwTorque,
                ForceMode.Impulse
            );
        }

        /// <summary>PD-регулятор для удержания заданной ориентации</summary>
        private void RotationHold(Quaternion targetRotation, float kp, float kd)
        {
            if (!_rb) return;

            // Ошибка ориентации: qErr = qTarget * inv(qCurrent)
            Quaternion qErr = targetRotation * Quaternion.Inverse(_rb.rotation);
            qErr.ToAngleAxis(out float angleDeg, out Vector3 axis);

            if (float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z))
                return;

            if (angleDeg > 180f) angleDeg -= 360f;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // P + D
            Vector3 torque = axis.normalized * (angleRad * kp) - _rb.angularVelocity * kd;
            _rb.AddTorque(torque, ForceMode.Acceleration);
        }

        /// <summary>плавное перемещение к позиции и повороту</summary>
        IEnumerator Move(Vector3 targetPos, Quaternion targetRot, float speed)
        {
            DisablePhysics();
            _occupied = true;

            while (Vector3.Distance(_rb.position, targetPos) > 0.001f ||
                Quaternion.Angle(_rb.rotation, targetRot) > 0.1f)
            {
                _rb.MovePosition(
                    Vector3.MoveTowards(_rb.position, targetPos, speed * Time.fixedDeltaTime)
                );
                _rb.MoveRotation(
                    Quaternion.RotateTowards(_rb.rotation, targetRot, speed * Time.fixedDeltaTime * 360)
                );
                yield return new WaitForFixedUpdate();
            }
            _rb.position = targetPos;
            _rb.rotation = targetRot;
            _occupied = false;
        }

        /// <summary>Запускает плавное перемещение к целевой позиции и повороту</summary>
        public void MoveToPosition(Vector3 targetPosition, Quaternion targetRotation, float speed)
        {
            StartCoroutine(Move(targetPosition, targetRotation, speed));
        }

        /// <summary>Вставляет объект в держатель (позиция + поворот holder'а)</summary>
        public void Insert(Transform holderTransform, float speed)
        {
            _isInserted = true;
            MoveToPosition(holderTransform.position, holderTransform.rotation, speed);
        }

        /// <summary>Выталкивает объект из слота, включает физику</summary>
        public void Eject()
        {
            _isInserted = false;
            EnablePhysics();
        }
    }
}