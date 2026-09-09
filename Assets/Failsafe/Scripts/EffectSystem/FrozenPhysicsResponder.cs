using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Failsafe.Scripts.EffectSystem
{
    public class FrozenPhysicsResponder : MonoBehaviour
    {
        [Header("Rigidbody")]
        [SerializeField] private bool _addRigidbodyIfMissing = true;
        [SerializeField] private bool _restoreRigidbodyOnClear = true;
        [SerializeField] private bool _clearVelocityOnEnd = true;

        [Header("Player / CharacterController")]
        [Tooltip("Если цель использует CharacterController, на время заморозки будет создан обычный CapsuleCollider для Rigidbody.")]
        [SerializeField] private bool _createTemporaryCapsuleFromCharacterController = true;

        [Header("Upright Recovery")]
        [Tooltip("После разморозки плавно вернуть объект в вертикальное положение.")]
        [SerializeField] private bool _restoreUprightOnClear = true;

        [Tooltip("Сколько секунд занимает возврат в вертикаль.")]
        [SerializeField] private float _uprightRecoveryDuration = 0.35f;

        [Tooltip("Если true, отключенные скрипты вернутся только после выпрямления.")]
        [SerializeField] private bool _restoreBehavioursAfterUprightRecovery = true;

        [Header("Disable On Frozen")]
        [SerializeField] private bool _disableAnimator = true;
        [SerializeField] private bool _disableNavMeshAgent = true;
        [SerializeField] private bool _disableCharacterController = true;
        [SerializeField] private bool _disableEnemyState = true;

        [Tooltip("Сюда вручную добавь PlayerController / PlayerMovementController / AI / shooting scripts.")]
        [SerializeField] private Behaviour[] _behavioursToDisable;

        [Header("Impulse")]
        [SerializeField] private bool _addSmallImpulse = true;
        [SerializeField] private Vector3 _localImpulse = new Vector3(0f, 0.5f, -1.5f);
        [SerializeField] private float _impulseForce = 2f;

        [Header("Debug")]
        [SerializeField] private bool _log;

        private Rigidbody _rb;
        private Animator _animator;
        private NavMeshAgent _navMeshAgent;
        private CharacterController _characterController;
        private Enemy _enemy;

        private CapsuleCollider _temporaryCapsuleCollider;

        private bool _isFrozen;
        private bool _isRecovering;

        private Coroutine _clearRoutine;

        private bool _hadRigidbodyBefore;
        private bool _storedIsKinematic;
        private bool _storedUseGravity;
        private RigidbodyConstraints _storedConstraints;
        private CollisionDetectionMode _storedCollisionDetectionMode;
        private RigidbodyInterpolation _storedInterpolation;

        private bool _storedCharacterControllerEnabled;
        private bool _hasStoredCharacterControllerState;

        private readonly Dictionary<Behaviour, bool> _storedBehaviourStates = new();

        private void Awake()
        {
            ResolveComponents();
        }

        public void ApplyFrozen(float duration, GameObject source)
        {
            ResolveComponents();

            if (_isRecovering)
                CancelRecoveryAndRestoreBaseState();

            if (_isFrozen)
                return;

            _isFrozen = true;

            if (_disableEnemyState && _enemy != null)
                _enemy.DisableState(duration);

            CreateTemporaryCapsuleIfNeeded();
            StoreAndDisableBehaviours();
            ApplyRigidbodyFrozenState();

            if (_log)
                EffectLog.Info(EffectLog.Physics, $"[FrozenPhysicsResponder] {name}: frozen physics ON", this);
        }

        public void ClearFrozen(GameObject source)
        {
            if (!_isFrozen && !_isRecovering)
                return;

            if (_clearRoutine != null)
            {
                StopCoroutine(_clearRoutine);
                _clearRoutine = null;
            }

            if (_restoreUprightOnClear &&
                _uprightRecoveryDuration > 0f &&
                isActiveAndEnabled)
            {
                _clearRoutine = StartCoroutine(ClearFrozenRoutine());
                return;
            }

            ClearFrozenImmediate();
        }

        private IEnumerator ClearFrozenRoutine()
        {
            _isFrozen = false;
            _isRecovering = true;

            PrepareRigidbodyForManualRecovery();

            if (!_restoreBehavioursAfterUprightRecovery)
                RestoreBehaviours();

            yield return RestoreUprightSmooth();

            RestoreRigidbodyState();
            DestroyTemporaryCapsule();

            if (_restoreBehavioursAfterUprightRecovery)
                RestoreBehaviours();

            _isRecovering = false;
            _clearRoutine = null;

            if (_log)
                EffectLog.Info(EffectLog.Physics, $"[FrozenPhysicsResponder] {name}: frozen physics OFF after upright recovery", this);
        }

        private void ClearFrozenImmediate()
        {
            _isFrozen = false;
            _isRecovering = false;

            PrepareRigidbodyForManualRecovery();

            if (_restoreUprightOnClear)
                RestoreUprightInstant();

            RestoreRigidbodyState();
            DestroyTemporaryCapsule();
            RestoreBehaviours();

            _clearRoutine = null;

            if (_log)
                EffectLog.Info(EffectLog.Physics, $"[FrozenPhysicsResponder] {name}: frozen physics OFF", this);
        }

        private void CancelRecoveryAndRestoreBaseState()
        {
            if (_clearRoutine != null)
            {
                StopCoroutine(_clearRoutine);
                _clearRoutine = null;
            }

            ClearFrozenImmediate();
        }

        private void ResolveComponents()
        {
            if (_rb == null)
                _rb = GetComponent<Rigidbody>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            if (_navMeshAgent == null)
            {
                _navMeshAgent =
                    GetComponent<NavMeshAgent>() ??
                    GetComponentInChildren<NavMeshAgent>(true);
            }

            if (_characterController == null)
            {
                _characterController =
                    GetComponent<CharacterController>() ??
                    GetComponentInChildren<CharacterController>(true);
            }

            if (_enemy == null)
            {
                _enemy =
                    GetComponent<Enemy>() ??
                    GetComponentInParent<Enemy>() ??
                    GetComponentInChildren<Enemy>(true);
            }
        }

        private void CreateTemporaryCapsuleIfNeeded()
        {
            if (!_createTemporaryCapsuleFromCharacterController)
                return;

            if (_characterController == null)
                return;

            if (HasUsableNonTriggerCollider())
                return;

            _temporaryCapsuleCollider = gameObject.AddComponent<CapsuleCollider>();

            _temporaryCapsuleCollider.center = _characterController.center;
            _temporaryCapsuleCollider.radius = _characterController.radius;
            _temporaryCapsuleCollider.height = _characterController.height;
            _temporaryCapsuleCollider.direction = 1;
            _temporaryCapsuleCollider.isTrigger = false;

            if (_log)
            {
                EffectLog.Info(EffectLog.Physics,
                    $"[FrozenPhysicsResponder] {name}: temporary CapsuleCollider created from CharacterController",
                    this);
            }
        }

        private bool HasUsableNonTriggerCollider()
        {
            Collider[] colliders = GetComponents<Collider>();

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];

                if (collider == null)
                    continue;

                if (collider == _characterController)
                    continue;

                if (!collider.enabled)
                    continue;

                if (collider.isTrigger)
                    continue;

                return true;
            }

            return false;
        }

        private void DestroyTemporaryCapsule()
        {
            if (_temporaryCapsuleCollider == null)
                return;

            Destroy(_temporaryCapsuleCollider);
            _temporaryCapsuleCollider = null;
        }

        private void ApplyRigidbodyFrozenState()
        {
            _hadRigidbodyBefore = _rb != null;

            if (_rb == null && _addRigidbodyIfMissing)
                _rb = gameObject.AddComponent<Rigidbody>();

            if (_rb == null)
            {
                EffectLog.Warning(EffectLog.Physics, $"[FrozenPhysicsResponder] {name}: Rigidbody not found and was not created.", this);
                return;
            }

            _storedIsKinematic = _rb.isKinematic;
            _storedUseGravity = _rb.useGravity;
            _storedConstraints = _rb.constraints;
            _storedCollisionDetectionMode = _rb.collisionDetectionMode;
            _storedInterpolation = _rb.interpolation;

#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector3.zero;
#else
            _rb.velocity = Vector3.zero;
#endif
            _rb.angularVelocity = Vector3.zero;

            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (_addSmallImpulse)
            {
                Vector3 impulse =
                    transform.TransformDirection(_localImpulse.normalized) *
                    Mathf.Max(0f, _impulseForce);

                _rb.AddForce(impulse, ForceMode.Impulse);
            }
        }

        private void PrepareRigidbodyForManualRecovery()
        {
            if (_rb == null)
                return;

            if (_clearVelocityOnEnd)
            {
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = Vector3.zero;
#else
                _rb.velocity = Vector3.zero;
#endif
                _rb.angularVelocity = Vector3.zero;
            }

            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        private void RestoreRigidbodyState()
        {
            if (_rb == null)
                return;

            if (_clearVelocityOnEnd)
            {
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = Vector3.zero;
#else
                _rb.velocity = Vector3.zero;
#endif
                _rb.angularVelocity = Vector3.zero;
            }

            if (_restoreRigidbodyOnClear)
            {
                _rb.isKinematic = _storedIsKinematic;
                _rb.useGravity = _storedUseGravity;
                _rb.constraints = _storedConstraints;
                _rb.collisionDetectionMode = _storedCollisionDetectionMode;
                _rb.interpolation = _storedInterpolation;
            }

            if (!_hadRigidbodyBefore && _addRigidbodyIfMissing && _rb != null)
            {
                Destroy(_rb);
                _rb = null;
            }
        }

        private IEnumerator RestoreUprightSmooth()
        {
            Quaternion startRotation = transform.rotation;
            Quaternion targetRotation = GetUprightRotation();

            float duration = Mathf.Max(0.01f, _uprightRecoveryDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);

                transform.rotation = Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t);

                yield return null;
            }

            transform.rotation = targetRotation;
        }

        private void RestoreUprightInstant()
        {
            transform.rotation = GetUprightRotation();
        }

        private Quaternion GetUprightRotation()
        {
            Vector3 euler = transform.eulerAngles;

            return Quaternion.Euler(
                0f,
                euler.y,
                0f);
        }

        private void StoreAndDisableBehaviours()
        {
            _storedBehaviourStates.Clear();
            _hasStoredCharacterControllerState = false;

            TryStoreAndDisable(_animator, _disableAnimator);
            TryStoreAndDisable(_navMeshAgent, _disableNavMeshAgent);

            StoreAndDisableCharacterController();

            if (_behavioursToDisable == null)
                return;

            foreach (Behaviour behaviour in _behavioursToDisable)
                TryStoreAndDisable(behaviour, true);
        }

        private void TryStoreAndDisable(Behaviour behaviour, bool shouldDisable)
        {
            if (!shouldDisable)
                return;

            if (behaviour == null)
                return;

            if (behaviour == this)
                return;

            if (_storedBehaviourStates.ContainsKey(behaviour))
                return;

            _storedBehaviourStates.Add(behaviour, behaviour.enabled);
            behaviour.enabled = false;
        }

        private void StoreAndDisableCharacterController()
        {
            if (!_disableCharacterController)
                return;

            if (_characterController == null)
                return;

            _storedCharacterControllerEnabled = _characterController.enabled;
            _hasStoredCharacterControllerState = true;

            _characterController.enabled = false;
        }

        private void RestoreBehaviours()
        {
            foreach (KeyValuePair<Behaviour, bool> pair in _storedBehaviourStates)
            {
                if (pair.Key == null)
                    continue;

                pair.Key.enabled = pair.Value;
            }

            _storedBehaviourStates.Clear();

            if (_hasStoredCharacterControllerState && _characterController != null)
                _characterController.enabled = _storedCharacterControllerEnabled;

            _hasStoredCharacterControllerState = false;
        }

        private void OnDisable()
        {
            if (_clearRoutine != null)
            {
                StopCoroutine(_clearRoutine);
                _clearRoutine = null;
            }

            if (_isFrozen || _isRecovering)
                ClearFrozenImmediate();
        }
    }
}