using System;
using Cysharp.Threading.Tasks;
using Failsafe.Player.Model;
using Failsafe.Player.View;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.Health;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using VContainer.Unity;
using SavedPlayerStateData = Failsafe.Scripts.SaveSystem.PlayerStateData;

namespace Failsafe.Player.Scripts
{
    public sealed class PlayerRunSaveParticipant : IRunSaveParticipant, IInitializable, IDisposable
    {
        public const string Id = RunSaveParticipantIds.Player;

        private const int PlayerRestoreOrder = 500;

        private readonly IRestorableHealth _health;
        private readonly IRestorableStamina _stamina;
        private readonly PlayerView _playerView;
        private readonly PlayerMovementController _movementController;
        private readonly RunSaveParticipantRegistry _participantRegistry;

        private IDisposable _registration;

        public string ParticipantId => Id;
        public int RestoreOrder => PlayerRestoreOrder;

        public PlayerRunSaveParticipant(
            IRestorableHealth health,
            IRestorableStamina stamina,
            PlayerView playerView,
            PlayerMovementController movementController,
            RunSaveParticipantRegistry participantRegistry)
        {
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _stamina = stamina ?? throw new ArgumentNullException(nameof(stamina));
            _playerView = playerView ?? throw new ArgumentNullException(nameof(playerView));
            _movementController = movementController ?? throw new ArgumentNullException(nameof(movementController));
            _participantRegistry = participantRegistry ?? throw new ArgumentNullException(nameof(participantRegistry));
        }

        public void Initialize()
        {
            _registration = _participantRegistry.Register(this);
        }

        public void Dispose()
        {
            _registration?.Dispose();
            _registration = null;
        }

        public void Capture(RunCheckpointData checkpoint)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            if (_health.IsDead)
                throw new InvalidOperationException("A checkpoint cannot be created while the player is dead.");

            Transform playerTransform = ResolvePlayerTransform();

            checkpoint.player = new SavedPlayerStateData
            {
                hasState = true,
                health = _health.CurrentHealth,
                stamina = _stamina.CurrentStamina,
                position = playerTransform.position,
                rotation = playerTransform.rotation
            };
        }

        public UniTask RestoreAsync(RunCheckpointData checkpoint, RunLoadContext context)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            SavedPlayerStateData state = checkpoint.player;
            if (state == null || !state.hasState)
                return UniTask.CompletedTask;

            ValidateState(state);
            RestoreTransform(state.position, NormalizeRotation(state.rotation));

            _movementController.ResetTransientState();
            _health.RestoreState(state.health);
            _stamina.RestoreState(state.stamina);

            return UniTask.CompletedTask;
        }

        private Transform ResolvePlayerTransform()
        {
            return _playerView.PlayerTransform != null
                ? _playerView.PlayerTransform
                : _playerView.transform;
        }

        private void RestoreTransform(Vector3 position, Quaternion rotation)
        {
            CharacterController characterController = _playerView.CharacterController;
            bool wasControllerEnabled = characterController != null && characterController.enabled;

            try
            {
                if (wasControllerEnabled)
                    characterController.enabled = false;

                ResolvePlayerTransform().SetPositionAndRotation(position, rotation);
            }
            finally
            {
                if (wasControllerEnabled)
                    characterController.enabled = true;
            }
        }

        private static void ValidateState(SavedPlayerStateData state)
        {
            if (!IsFinite(state.health) || !IsFinite(state.stamina))
                throw new InvalidOperationException("The saved player health or stamina is not finite.");

            if (!IsFinite(state.position))
                throw new InvalidOperationException("The saved player position is not finite.");

            if (!IsFinite(state.rotation) || RotationMagnitudeSquared(state.rotation) <= Mathf.Epsilon)
                throw new InvalidOperationException("The saved player rotation is invalid.");
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
