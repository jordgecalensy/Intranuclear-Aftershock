using System.Collections.Generic;
using Failsafe.Player;
using Failsafe.Player.View;
using Failsafe.PlayerMovements;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Chests.Tests
{
    [TestFixture]
    public sealed class ChestInteractionSessionTests
    {
        private const float Tolerance = 0.0001f;

        private readonly List<Object> _objects = new List<Object>();
        private readonly List<ChestInteractionSession> _sessions =
            new List<ChestInteractionSession>();
        private CursorLockMode _savedCursorMode;
        private bool _savedCursorVisible;

        [SetUp]
        public void SetUp()
        {
            _savedCursorMode = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _sessions.Count - 1; index >= 0; index--)
                _sessions[index]?.Dispose();

            _sessions.Clear();

            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                    Object.DestroyImmediate(_objects[index]);
            }

            _objects.Clear();
            Cursor.lockState = _savedCursorMode;
            Cursor.visible = _savedCursorVisible;
        }

        [Test]
        public void Session_ExclusivelyOwnsCameraAndControlLockUntilClosed()
        {
            GameObject player = Track(new GameObject("Chest Session Player"));
            PlayerControlBlocker blocker =
                player.AddComponent<PlayerControlBlocker>();
            CursorLock cursorLock = player.AddComponent<CursorLock>();
            var cameraOverride = new PlayerCameraPoseOverride();
            ChestInteractionSession session = Track(
                new ChestInteractionSession(
                    blocker,
                    cameraOverride,
                    cursorLock));

            GameObject owner = Track(new GameObject("Chest Owner"));
            GameObject otherOwner = Track(new GameObject("Other Chest"));
            Transform anchor = Track(new GameObject("Camera Anchor")).transform;
            anchor.SetPositionAndRotation(
                new Vector3(3f, 4f, 5f),
                Quaternion.Euler(10f, 20f, 30f));

            Assert.That(
                session.TryOpen(owner, anchor, out string openError),
                Is.True,
                openError);
            Assert.That(session.IsOwnedBy(owner), Is.True);
            Assert.That(
                blocker.IsLockedBy(PlayerControlLockIds.ChestOpened),
                Is.True);
            Assert.That(
                blocker.IsBlocked(PlayerControlBlock.Inventory),
                Is.True);
            Assert.That(cameraOverride.TryGetPose(out Pose pose), Is.True);
            AssertVector(pose.position, anchor.position);
            Assert.That(
                Quaternion.Angle(pose.rotation, anchor.rotation),
                Is.LessThan(Tolerance));

            Assert.That(
                session.TryOpen(otherOwner, anchor, out string conflictError),
                Is.False);
            Assert.That(conflictError, Is.Not.Empty);
            Assert.That(
                session.TryClose(otherOwner, out string wrongOwnerError),
                Is.False);
            Assert.That(wrongOwnerError, Is.Not.Empty);
            Assert.That(session.IsOwnedBy(owner), Is.True);

            Assert.That(
                session.TryClose(owner, out string closeError),
                Is.True,
                closeError);
            Assert.That(session.IsOpen, Is.False);
            Assert.That(
                blocker.IsLockedBy(PlayerControlLockIds.ChestOpened),
                Is.False);
            Assert.That(cameraOverride.HasOverride, Is.False);
        }

        [Test]
        public void Session_DestroyedOwnerOrAnchor_ReleasesLockAndCameraOverride()
        {
            GameObject player = Track(new GameObject("Destroyed Owner Player"));
            PlayerControlBlocker blocker =
                player.AddComponent<PlayerControlBlocker>();
            CursorLock cursorLock = player.AddComponent<CursorLock>();
            var cameraOverride = new PlayerCameraPoseOverride();
            ChestInteractionSession session = Track(
                new ChestInteractionSession(
                    blocker,
                    cameraOverride,
                    cursorLock));

            GameObject owner = Track(new GameObject("Temporary Chest Owner"));
            Transform anchor = Track(new GameObject("Temporary Anchor")).transform;

            Assert.That(
                session.TryOpen(owner, anchor, out string error),
                Is.True,
                error);

            Object.DestroyImmediate(owner);

            Assert.That(session.IsOpen, Is.False);
            Assert.That(
                blocker.IsLockedBy(PlayerControlLockIds.ChestOpened),
                Is.False);
            Assert.That(cameraOverride.HasOverride, Is.False);

            GameObject secondOwner = Track(
                new GameObject("Second Temporary Chest Owner"));
            GameObject secondAnchorObject = Track(
                new GameObject("Second Temporary Anchor"));

            Assert.That(
                session.TryOpen(
                    secondOwner,
                    secondAnchorObject.transform,
                    out string secondError),
                Is.True,
                secondError);

            Object.DestroyImmediate(secondAnchorObject);

            Assert.That(session.IsOpen, Is.False);
            Assert.That(
                blocker.IsLockedBy(PlayerControlLockIds.ChestOpened),
                Is.False);
            Assert.That(cameraOverride.HasOverride, Is.False);
        }

        [Test]
        public void PlayerCameraController_UsesOverrideThenReturnsToHeadFollow()
        {
            GameObject playerObject = Track(new GameObject("Camera Test Player"));
            PlayerView playerView = playerObject.AddComponent<PlayerView>();
            Transform modelHead = Track(new GameObject("Model Head")).transform;
            Transform rigHead = Track(new GameObject("Rig Head")).transform;
            Transform camera = Track(new GameObject("Player Camera")).transform;

            modelHead.position = new Vector3(1f, 2f, 3f);
            rigHead.rotation = Quaternion.Euler(0f, 35f, 0f);
            camera.position = new Vector3(1f, 3f, 3f);
            playerView.PlayerModelHead = modelHead;
            playerView.PlayerRigHead = rigHead;
            playerView.PlayerCamera = camera;

            var cameraOverride = new PlayerCameraPoseOverride();
            var controller = new PlayerCameraController(
                playerView,
                cameraOverride);

            controller.LateTick();
            Vector3 expectedFollowPosition =
                modelHead.position + rigHead.rotation * Vector3.up;
            AssertVector(camera.position, expectedFollowPosition);

            GameObject owner = Track(new GameObject("Camera Override Owner"));
            Transform anchor = Track(new GameObject("Override Anchor")).transform;
            anchor.SetPositionAndRotation(
                new Vector3(8f, 7f, 6f),
                Quaternion.Euler(15f, 25f, 5f));

            Assert.That(
                cameraOverride.TryAcquire(owner, anchor, out string error),
                Is.True,
                error);

            controller.LateTick();
            AssertVector(camera.position, anchor.position);
            Assert.That(
                Quaternion.Angle(camera.rotation, anchor.rotation),
                Is.LessThan(Tolerance));

            Assert.That(cameraOverride.Release(owner), Is.True);
            modelHead.position = new Vector3(2f, 4f, 6f);
            controller.LateTick();
            AssertVector(
                camera.position,
                modelHead.position + rigHead.rotation * Vector3.up);
        }

        private T Track<T>(T target) where T : Object
        {
            _objects.Add(target);
            return target;
        }

        private ChestInteractionSession Track(
            ChestInteractionSession session)
        {
            _sessions.Add(session);
            return session;
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
