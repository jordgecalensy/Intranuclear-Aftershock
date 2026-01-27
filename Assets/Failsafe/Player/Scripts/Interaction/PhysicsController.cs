using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Failsafe.Player.Scripts.Interaction
{
    public sealed class PhysicsController : MonoBehaviour
    {
        private Rigidbody _rb;
        private Transform _grabPoint;
        private bool _fixRotation;
        private PhysicsInteraction _physicsInteraction;
        private int _carryingLayerIndex = 8; // слой для рейкаста
        private float _rotKp = 500f;
        private float _rotKd = 50f;
        private float _toTargetSpeed = 4f;
        private int _cachedCarryingLayer;
        private Vector3 _grabHelperVector = new Vector3(0f, 0.1f, 0f);
        private bool _occupied = false;
        private bool _isInserted = false;
        public bool IsInserted => _isInserted;
        public bool IsGrabbed => _grabPoint != null;



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

        private void DragObject()
            {
                Vector3 toTarget = _grabPoint.position - transform.position;
                _rb.linearVelocity = toTarget * _toTargetSpeed;
            }

        public bool Grab(Transform grabPoint, PhysicsInteraction physicsInteraction, bool fixRotation = false)
        {
            if (_occupied) return false;
            if (_isInserted) Eject();
            _grabPoint = grabPoint;
            _physicsInteraction = physicsInteraction;
            _rb.useGravity = false;
            _rb.transform.position += _grabHelperVector;
            _cachedCarryingLayer = gameObject.layer;
            gameObject.layer = _carryingLayerIndex;
            _rb.angularVelocity = Vector3.zero;
            _fixRotation = fixRotation;
            return true;
        }

        public void Release()
        {
            _grabPoint = null;
            _physicsInteraction?.Released();
            _physicsInteraction = null;
            _rb.useGravity = true;
            gameObject.layer = _cachedCarryingLayer;
        }

        public void ForceRelease()
        {
            _grabPoint = null;
            _physicsInteraction?.Released();
            _physicsInteraction = null;
            _rb.useGravity = true;
            gameObject.layer = _cachedCarryingLayer;
        }

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

        public void MoveToPosition(Vector3 targetPosition, Quaternion targetRotation, float speed)
        {
            StartCoroutine(Move(targetPosition, targetRotation, speed));
        }

        public void Insert(Transform holderTransform, float speed)
        {
            _isInserted = true;
            MoveToPosition(holderTransform.position, holderTransform.rotation, speed);
            Debug.Log("Object inserted: " + holderTransform.position);
        }

        public void Eject()
        {
            _isInserted = false;
            EnablePhysics();
        }
    }
}