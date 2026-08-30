using System;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Parents supported passengers to a moving obstacle while they stand on it.
    /// </summary>
    public sealed class ObstaclePassengerAttachment
    {
        private readonly Transform _obstacleTransform;
        private readonly Collider _contactTrigger;
        private readonly Func<GameObject, bool> _isPlayer;
        private readonly Dictionary<Transform, int> _overlapCount = new();
        private readonly Dictionary<Transform, Transform> _oldParents = new();
        private readonly List<Collider> _colliderBuffer = new();

        public ObstaclePassengerAttachment(
            Transform obstacleTransform,
            Collider contactTrigger,
            Func<GameObject, bool> isPlayer)
        {
            _obstacleTransform = obstacleTransform != null
                ? obstacleTransform
                : throw new ArgumentNullException(nameof(obstacleTransform));
            _contactTrigger = contactTrigger;
            _isPlayer = isPlayer ??
                throw new ArgumentNullException(nameof(isPlayer));
        }

        public void Enter(
            Collider other,
            bool onlyPlayers,
            float topTolerance)
        {
            Transform root = GetRootTransform(other);

            if (root == null)
                return;

            if (onlyPlayers && !_isPlayer(root.gameObject))
                return;

            if (_overlapCount.TryGetValue(root, out int count))
                _overlapCount[root] = count + 1;
            else
                _overlapCount[root] = 1;

            if (_oldParents.ContainsKey(root))
                return;

            if (!IsFromTop(root, topTolerance))
                return;

            Rigidbody targetRigidbody = root.GetComponent<Rigidbody>();

            if (targetRigidbody != null && !targetRigidbody.isKinematic)
                return;

            _oldParents[root] = root.parent;
            root.SetParent(_obstacleTransform, true);
        }

        public void Exit(Collider other)
        {
            Transform root = GetRootTransform(other);

            if (root == null ||
                !_overlapCount.TryGetValue(root, out int count))
            {
                return;
            }

            count--;

            if (count > 0)
            {
                _overlapCount[root] = count;
                return;
            }

            _overlapCount.Remove(root);
            RestoreParent(root);
        }

        public void Clear()
        {
            foreach (KeyValuePair<Transform, Transform> pair in _oldParents)
            {
                Transform root = pair.Key;

                if (root != null)
                    root.SetParent(pair.Value, true);
            }

            _overlapCount.Clear();
            _oldParents.Clear();
            _colliderBuffer.Clear();
        }

        private void RestoreParent(Transform root)
        {
            if (!_oldParents.TryGetValue(root, out Transform parent))
                return;

            if (root != null)
                root.SetParent(parent, true);

            _oldParents.Remove(root);
        }

        private bool IsFromTop(Transform targetRoot, float topTolerance)
        {
            if (_contactTrigger == null)
                return false;

            Bounds platformBounds = _contactTrigger.bounds;
            Bounds targetBounds = GetBounds(targetRoot);

            return targetBounds.min.y >=
                   platformBounds.max.y - Mathf.Max(0f, topTolerance);
        }

        private Bounds GetBounds(Transform root)
        {
            _colliderBuffer.Clear();
            root.GetComponentsInChildren(false, _colliderBuffer);

            if (_colliderBuffer.Count == 0)
                return new Bounds(root.position, Vector3.zero);

            Bounds bounds = _colliderBuffer[0].bounds;

            for (int i = 1; i < _colliderBuffer.Count; i++)
                bounds.Encapsulate(_colliderBuffer[i].bounds);

            return bounds;
        }

        private static Transform GetRootTransform(Collider collider)
        {
            if (collider == null)
                return null;

            if (collider.attachedRigidbody != null)
                return collider.attachedRigidbody.transform;

            return collider.transform.root;
        }
    }
}
