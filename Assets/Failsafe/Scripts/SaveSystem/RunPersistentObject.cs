using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Failsafe.Scripts.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class RunPersistentObject : MonoBehaviour
    {
        private const string PlacedIdPrefix = "placed-object:";
        private const string SpawnedIdPrefix = "spawned-object:";
        private const string PrefabTemplateId = "prefab-template";

        [SerializeField]
        [Tooltip("Stable identity of this object inside a run. Do not edit manually.")]
        private string _persistentId;

        [SerializeField]
        [Tooltip("Capture and restore the world position and rotation.")]
        private bool _captureTransform = true;

        [SerializeField]
        [Tooltip("Capture and safely restore Rigidbody settings. Requires Rigidbody on this GameObject.")]
        private bool _captureRigidbody = true;

        [SerializeField]
        [Tooltip("Fail checkpoint restoration if this object is missing. Enable for progression-critical interactives.")]
        private bool _requiredOnRestore;

        private Rigidbody _rigidbody;
        private bool _hasPendingRigidbodyRestore;
        private bool _pendingIsKinematic;
        private bool _pendingUseGravity;
        private RigidbodyConstraints _pendingConstraints;

        public string PersistentId => _persistentId;
        public bool RequiredOnRestore => _requiredOnRestore;

        private void Awake()
        {
            CacheRigidbody();
        }

        private void Reset()
        {
            _captureRigidbody = GetComponent<Rigidbody>() != null;
            EnsureFallbackPlacedId();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            EnsureEditorIdentity();
#else
            EnsureFallbackPlacedId();
#endif
        }

        public void AssignRuntimeId(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
                throw new ArgumentException("Runtime persistent ID cannot be empty.", nameof(runtimeId));

            string normalizedId = runtimeId.Trim();
            _persistentId = normalizedId.StartsWith(SpawnedIdPrefix, StringComparison.Ordinal)
                ? normalizedId
                : $"{SpawnedIdPrefix}{normalizedId}";
        }

        internal PersistentObjectStateData CaptureState()
        {
            ValidatePersistentId();
            CacheRigidbody();

            PersistentObjectStateData state = new PersistentObjectStateData
            {
                persistentId = _persistentId.Trim(),
                requiredOnRestore = _requiredOnRestore,
                isActive = gameObject.activeSelf,
                hasTransform = _captureTransform
            };

            if (_captureTransform)
            {
                state.position = transform.position;
                state.rotation = transform.rotation;
            }

            if (_captureRigidbody)
            {
                if (_rigidbody == null)
                {
                    throw new InvalidOperationException(
                        $"Persistent object '{name}' is configured to capture a Rigidbody, " +
                        "but no Rigidbody exists on the same GameObject.");
                }

                state.hasRigidbody = true;
                state.isKinematic = _rigidbody.isKinematic;
                state.useGravity = _rigidbody.useGravity;
                state.rigidbodyConstraints = (int)_rigidbody.constraints;
            }

            IRunPersistentStateProvider stateProvider = ResolveStateProvider();
            if (stateProvider != null)
            {
                if (string.IsNullOrWhiteSpace(stateProvider.StateTypeId))
                {
                    throw new InvalidOperationException(
                        $"Persistent state provider on '{name}' has an empty state type ID.");
                }

                if (stateProvider.StateVersion <= 0)
                {
                    throw new InvalidOperationException(
                        $"Persistent state provider '{stateProvider.StateTypeId}' on '{name}' " +
                        "has an invalid state version.");
                }

                state.stateType = stateProvider.StateTypeId.Trim();
                state.stateVersion = stateProvider.StateVersion;
                state.state = stateProvider.CapturePersistentState();
            }

            return state;
        }

        internal void PrepareRestore(PersistentObjectStateData state)
        {
            ValidateStateIdentity(state);
            CacheRigidbody();

            if (state.isActive && !gameObject.activeSelf)
                gameObject.SetActive(true);

            if (state.hasRigidbody)
            {
                if (_rigidbody == null)
                {
                    throw new InvalidOperationException(
                        $"Saved persistent object '{state.persistentId}' requires a Rigidbody, " +
                        $"but runtime object '{name}' has none.");
                }

                _pendingIsKinematic = state.isKinematic;
                _pendingUseGravity = state.useGravity;
                _pendingConstraints = (RigidbodyConstraints)state.rigidbodyConstraints;
                _hasPendingRigidbodyRestore = true;

                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }

                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }

            if (!state.hasTransform)
                return;

            ValidateTransformState(state);
            Quaternion normalizedRotation = NormalizeRotation(state.rotation);

            if (state.hasRigidbody && _rigidbody != null)
            {
                _rigidbody.position = state.position;
                _rigidbody.rotation = normalizedRotation;
            }

            transform.SetPositionAndRotation(state.position, normalizedRotation);
        }

        internal void RestoreCustomState(PersistentObjectStateData state)
        {
            IRunPersistentStateProvider stateProvider = ResolveStateProvider();
            bool hasSavedCustomState = !string.IsNullOrWhiteSpace(state.stateType);

            if (!hasSavedCustomState)
                return;

            if (stateProvider == null)
            {
                throw new InvalidOperationException(
                    $"Persistent object '{state.persistentId}' contains state type " +
                    $"'{state.stateType}', but runtime object '{name}' has no state provider.");
            }

            if (!string.Equals(
                    stateProvider.StateTypeId?.Trim(),
                    state.stateType.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Persistent object '{state.persistentId}' expected state provider " +
                    $"'{state.stateType}', but runtime object '{name}' provides " +
                    $"'{stateProvider.StateTypeId}'.");
            }

            stateProvider.RestorePersistentState(state.state, state.stateVersion);
        }

        internal void CompleteRestore(PersistentObjectStateData state)
        {
            if (_hasPendingRigidbodyRestore && _rigidbody != null)
            {
                _rigidbody.constraints = _pendingConstraints;
                _rigidbody.useGravity = _pendingUseGravity;
                _rigidbody.isKinematic = _pendingIsKinematic;

                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
            }

            _hasPendingRigidbodyRestore = false;

            if (gameObject.activeSelf != state.isActive)
                gameObject.SetActive(state.isActive);
        }

        private void CacheRigidbody()
        {
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();
        }

        private IRunPersistentStateProvider ResolveStateProvider()
        {
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            IRunPersistentStateProvider stateProvider = null;

            for (int i = 0; i < components.Length; i++)
            {
                if (!(components[i] is IRunPersistentStateProvider candidate))
                    continue;

                if (stateProvider != null)
                {
                    throw new InvalidOperationException(
                        $"Persistent object '{name}' has more than one " +
                        $"{nameof(IRunPersistentStateProvider)}.");
                }

                stateProvider = candidate;
            }

            return stateProvider;
        }

        private void ValidatePersistentId()
        {
            string normalizedId = _persistentId?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedId) ||
                string.Equals(normalizedId, PrefabTemplateId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Persistent object '{name}' has no scene or runtime identity.");
            }

            if (!normalizedId.StartsWith(PlacedIdPrefix, StringComparison.Ordinal) &&
                !normalizedId.StartsWith(SpawnedIdPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Persistent object '{name}' has unsupported ID '{normalizedId}'.");
            }
        }

        private void EnsureFallbackPlacedId()
        {
            if (string.IsNullOrWhiteSpace(_persistentId))
                _persistentId = $"{PlacedIdPrefix}{Guid.NewGuid():N}";
        }

#if UNITY_EDITOR
        private void EnsureEditorIdentity()
        {
            if (Application.isPlaying)
                return;

            bool isPrefabAsset =
                PrefabUtility.IsPartOfPrefabAsset(gameObject) ||
                PrefabStageUtility.GetPrefabStage(gameObject) != null;

            if (isPrefabAsset)
            {
                if (!string.Equals(_persistentId, PrefabTemplateId, StringComparison.Ordinal))
                {
                    _persistentId = PrefabTemplateId;
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            if (!gameObject.scene.IsValid())
            {
                EnsureFallbackPlacedId();
                return;
            }

            GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(this);
            string sceneGuid = globalObjectId.assetGUID.ToString();

            if (globalObjectId.targetObjectId == 0 ||
                string.IsNullOrWhiteSpace(sceneGuid) ||
                sceneGuid.Trim('0').Length == 0)
            {
                EnsureFallbackPlacedId();
                return;
            }

            string expectedId =
                $"{PlacedIdPrefix}{sceneGuid}:" +
                $"{globalObjectId.targetObjectId}:" +
                $"{globalObjectId.targetPrefabId}";

            if (string.Equals(_persistentId, expectedId, StringComparison.Ordinal))
                return;

            _persistentId = expectedId;
            EditorUtility.SetDirty(this);
        }
#endif

        private static void ValidateStateIdentity(PersistentObjectStateData state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.persistentId))
                throw new InvalidOperationException("Checkpoint contains an unidentified persistent object.");
        }

        private static void ValidateTransformState(PersistentObjectStateData state)
        {
            if (!IsFinite(state.position))
            {
                throw new InvalidOperationException(
                    $"Persistent object '{state.persistentId}' has a non-finite position.");
            }

            if (!IsFinite(state.rotation) ||
                RotationMagnitudeSquared(state.rotation) <= Mathf.Epsilon)
            {
                throw new InvalidOperationException(
                    $"Persistent object '{state.persistentId}' has an invalid rotation.");
            }
        }

        private static Quaternion NormalizeRotation(Quaternion rotation)
        {
            float magnitude = Mathf.Sqrt(RotationMagnitudeSquared(rotation));
            return new Quaternion(
                rotation.x / magnitude,
                rotation.y / magnitude,
                rotation.z / magnitude,
                rotation.w / magnitude);
        }

        private static float RotationMagnitudeSquared(Quaternion rotation)
        {
            return rotation.x * rotation.x +
                   rotation.y * rotation.y +
                   rotation.z * rotation.z +
                   rotation.w * rotation.w;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z) &&
                   IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
