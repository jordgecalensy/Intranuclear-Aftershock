using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Owns the movement state of one obstacle. It does not create an Update loop.
    /// </summary>
    public sealed class ObstacleMotion
    {
        private const float WaypointArrivalDistance = 0.02f;

        private readonly Rigidbody _rigidbody;
        private readonly Transform _transform;

        private int _currentWaypointIndex;
        private float _waitTimer;
        private bool _isWaiting;

        public int CurrentWaypointIndex => _currentWaypointIndex;

        public ObstacleMotion(Rigidbody rigidbody, Transform transform)
        {
            _rigidbody = rigidbody;
            _transform = transform;
        }

        public void Tick(
            float deltaTime,
            bool rotate,
            Vector3 rotationAxis,
            float rotationSpeed,
            bool movable,
            float moveSpeed,
            float waitAtWaypoint,
            IReadOnlyList<Transform> waypoints)
        {
            if (_rigidbody == null || _transform == null)
                return;

            TickRotation(deltaTime, rotate, rotationAxis, rotationSpeed);
            TickMovement(
                deltaTime,
                movable,
                moveSpeed,
                waitAtWaypoint,
                waypoints);
        }

        private void TickRotation(
            float deltaTime,
            bool rotate,
            Vector3 rotationAxis,
            float rotationSpeed)
        {
            if (!rotate)
                return;

            Quaternion deltaRotation = Quaternion.Euler(
                rotationAxis * rotationSpeed * deltaTime);
            _rigidbody.MoveRotation(
                _rigidbody.rotation * deltaRotation);
        }

        private void TickMovement(
            float deltaTime,
            bool movable,
            float moveSpeed,
            float waitAtWaypoint,
            IReadOnlyList<Transform> waypoints)
        {
            if (!movable || waypoints == null || waypoints.Count == 0)
            {
                ResetWaypointStateIfEmpty(waypoints);
                return;
            }

            NormalizeWaypointIndex(waypoints.Count);
            Transform target = waypoints[_currentWaypointIndex];

            if (target == null)
            {
                AdvanceWaypoint(waypoints.Count);
                return;
            }

            Vector3 offset = target.position - _transform.position;
            float arrivalDistanceSquared =
                WaypointArrivalDistance * WaypointArrivalDistance;

            if (offset.sqrMagnitude <= arrivalDistanceSquared)
            {
                TickWaypointWait(
                    deltaTime,
                    waitAtWaypoint,
                    waypoints.Count);
                return;
            }

            _isWaiting = false;
            Vector3 nextPosition = Vector3.MoveTowards(
                _transform.position,
                target.position,
                Mathf.Max(0f, moveSpeed) * deltaTime);
            _rigidbody.MovePosition(nextPosition);
        }

        private void TickWaypointWait(
            float deltaTime,
            float waitAtWaypoint,
            int waypointCount)
        {
            if (!_isWaiting)
            {
                _isWaiting = true;
                _waitTimer = Mathf.Max(0f, waitAtWaypoint);
                return;
            }

            _waitTimer -= deltaTime;

            if (_waitTimer > 0f)
                return;

            _isWaiting = false;
            AdvanceWaypoint(waypointCount);
        }

        private void AdvanceWaypoint(int waypointCount)
        {
            _isWaiting = false;

            if (waypointCount <= 0)
            {
                _currentWaypointIndex = 0;
                return;
            }

            _currentWaypointIndex =
                (_currentWaypointIndex + 1) % waypointCount;
        }

        private void NormalizeWaypointIndex(int waypointCount)
        {
            if (_currentWaypointIndex < 0 ||
                _currentWaypointIndex >= waypointCount)
            {
                _currentWaypointIndex = 0;
                _isWaiting = false;
            }
        }

        private void ResetWaypointStateIfEmpty(
            IReadOnlyList<Transform> waypoints)
        {
            if (waypoints != null && waypoints.Count > 0)
                return;

            _currentWaypointIndex = 0;
            _waitTimer = 0f;
            _isWaiting = false;
        }
    }
}
