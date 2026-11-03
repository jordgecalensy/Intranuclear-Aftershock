using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.Animations;

namespace Failsafe.Inventory.Presentation.Tests
{
    public sealed class InventoryRobotOpeningTests
    {
        private GameObject _root;
        private InventoryRobotPresentationController _controller;
        private int _opened;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Robot opening test");
            _controller = _root.AddComponent<InventoryRobotPresentationController>();
            _opened = 0;
            _controller.OpenCompleted += () => _opened++;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void FlightCompletionWaitsForBodyAndOpensOnlyOnce()
        {
            BeginOpening();
            _controller.NotifyInventoryReady();
            Assert.That(_controller.IsOpen, Is.False);
            Assert.That(_opened, Is.Zero);
            Set("_bodyReady", true);
            Complete();
            _controller.NotifyInventoryReady();
            Complete();
            Assert.That(_controller.IsOpen, Is.True);
            Assert.That(_opened, Is.EqualTo(1));
        }

        [Test]
        public void BodyCompletionWaitsForFlight()
        {
            BeginOpening();
            Set("_bodyReady", true);
            Complete();
            Assert.That(_opened, Is.Zero);
            _controller.NotifyInventoryReady();
            Assert.That(_opened, Is.Zero, "Animation Event must not start another animation inline.");
            Complete();
            Assert.That(_opened, Is.EqualTo(1));
        }

        [Test]
        public void ForcedHideRejectsLateCompletion()
        {
            BeginOpening();
            _controller.NotifyInventoryReady();
            _controller.ForceHidden();
            Set("_bodyReady", true);
            Complete();
            _controller.NotifyInventoryReady();
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Hidden));
            Assert.That(_opened, Is.Zero);
        }

        [Test]
        public void RefusalWaitsForBothMotionsAndNeverOpensInventory()
        {
            BeginOpening();
            Set("_refusalSelected", true);
            int closed = 0;
            int rejected = 0;
            _controller.CloseCompleted += () => closed++;
            _controller.OpenRejected += () => rejected++;
            _controller.NotifyInventoryReady();
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Opening));
            Assert.That(_controller.RequestOpen(), Is.False);
            Assert.That(_controller.RequestClose(), Is.True);
            Set("_bodyReady", true);
            Complete();
            Assert.That(_opened, Is.Zero);
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(rejected, Is.EqualTo(1));
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Hidden));
            _controller.NotifyInventoryReady();
            Assert.That(_opened, Is.Zero);
            Assert.That(_controller.RequestOpen(), Is.True);
            Assert.That(_opened, Is.EqualTo(1));
        }

        [Test]
        public void DisablingPresenterDuringOpeningHidesVisualAndCompletesCleanup()
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(_root.transform);
            Set("_visualRoot", visual);
            BeginOpening();
            int closed = 0;
            _controller.CloseCompleted += () => closed++;
            _controller.enabled = false;
            // Edit Mode does not run the lifecycle of a normal MonoBehaviour.
            // Exercise the cleanup callback explicitly in this unit test.
            InvokeCallback("OnDisable");
            Assert.That(visual.activeSelf, Is.False);
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Hidden));
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(_opened, Is.Zero);
            _controller.enabled = true;
            Assert.That(_controller.RequestOpen(), Is.True);
            Assert.That(visual.activeSelf, Is.True);
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(20)]
        public void RepeatedCloseDuringOpeningReturnsWithoutWaitingForEitherAnimation(int pressCount)
        {
            BeginOpening();
            for (int i = 0; i < pressCount; i++)
                Assert.That(_controller.RequestClose(), Is.True);
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Opening));
            int closed = 0;
            _controller.CloseCompleted += () => closed++;
            InvokeCallback("LateUpdate");
            Assert.That(_opened, Is.Zero);
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Hidden));
            _controller.NotifyInventoryReady();
            _controller.NotifyRobotHidden();
            InvokeCallback("LateUpdate");
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(_opened, Is.Zero);
            Assert.That(_controller.RequestOpen(), Is.True);
            Assert.That(_opened, Is.EqualTo(1));
        }

        [Test]
        public void ClosingCoroutineDefersAnimatorWorkAndReturnsIfBodyIsUnavailable()
        {
            typeof(InventoryRobotPresentationController)
                .GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_controller, new object[] { InventoryRobotPresentationState.Closing });
            int closed = 0;
            _controller.CloseCompleted += () => closed++;
            var closing = (System.Collections.IEnumerator)typeof(InventoryRobotPresentationController)
                .GetMethod("PlayBodyClosing", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_controller, null);

            Assert.That(closing.MoveNext(), Is.True);
            Assert.That(closing.Current, Is.Null);
            Assert.That(closed, Is.Zero);
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Closing));
            Assert.That(closing.MoveNext(), Is.False);
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Hidden));
        }

        [Test]
        public void ReactionWeightsCoverAllRollsExactly()
        {
            var counts = new int[5];
            for (int roll = 0; roll < InventoryRobotReactionSelector.RollCount; roll++)
                counts[(int)InventoryRobotReactionSelector.Select(roll)]++;
            Assert.That(counts, Is.EqualTo(new[] { 0, 5000, 2250, 2250, 500 }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => InventoryRobotReactionSelector.Select(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => InventoryRobotReactionSelector.Select(10000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PausedBodyCanSampleIntermediateClosingPoses(bool cancelOpening)
        {
            var body = new GameObject("Body");
            body.transform.SetParent(_root.transform);
            var lid = new GameObject("Lid");
            lid.transform.SetParent(body.transform);
            var clip = new AnimationClip();
            clip.SetCurve("Lid", typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 2f, 1f));
            var graph = new AnimatorController();
            graph.AddLayer("Base Layer");
            AnimatorState state = graph.layers[0].stateMachine.AddState("RUVI_Open");
            state.motion = clip;
            graph.layers[0].stateMachine.defaultState = state;
            var animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = graph;
            Set("_bodyAnimator", animator);
            MethodInfo sample = typeof(InventoryRobotPresentationController)
                .GetMethod("SampleBodyPose", BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                animator.speed = 0f;
                sample.Invoke(_controller, new object[] { 1f, null });
                Assert.That(lid.transform.localPosition.x, Is.EqualTo(1f).Within(0.01f));
                sample.Invoke(_controller, new object[] { 0.5f, null });
                Assert.That(lid.transform.localPosition.x, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(animator.speed, Is.Zero);
                Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip.length,
                    Is.EqualTo(2f).Within(0.01f));
                if (cancelOpening)
                {
                    Set("_activeBodyOpenState", "Base Layer.RUVI_Open");
                    BeginOpening();
                    Assert.That(_controller.RequestClose(), Is.True);
                    InvokeCallback("LateUpdate");
                    Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Closing),
                        "The half-open lid must close before returning and hiding.");
                    Assert.That(typeof(InventoryRobotPresentationController)
                        .GetField("_returnStarted", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(_controller), Is.False);
                    _controller.NotifyRobotHidden();
                    Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Closing));
                    Assert.That(_controller.RequestClose(), Is.False);
                    Assert.That(_opened, Is.Zero);
                    _controller.ForceHidden();
                }
                sample.Invoke(_controller, new object[] { 0f, null });
                Assert.That(lid.transform.localPosition.x, Is.EqualTo(0f).Within(0.01f));
                if (!cancelOpening)
                {
                    Set("_activeBodyOpenState", "Base Layer.RUVI_Open");
                    MethodInfo advance = typeof(InventoryRobotPresentationController)
                        .GetMethod("AdvanceBodyOpening", BindingFlags.NonPublic | BindingFlags.Instance);
                    object[] step = { 1f, 0f, 0f };
                    Assert.That(advance.Invoke(_controller, step), Is.False);
                    Assert.That(lid.transform.localPosition.x, Is.EqualTo(0.5f).Within(0.01f));
                    // Simulate the pose being reset between frames; completion
                    // must follow owned clip time, not the Animator's clock.
                    sample.Invoke(_controller, new object[] { 0f, null });
                    Assert.That(advance.Invoke(_controller, step), Is.True);
                    Assert.That(lid.transform.localPosition.x, Is.EqualTo(1f).Within(0.01f));
                    Assert.That(animator.speed, Is.Zero);
                }
            }
            finally
            {
                animator.runtimeAnimatorController = null;
                Object.DestroyImmediate(state);
                Object.DestroyImmediate(graph.layers[0].stateMachine);
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void HiddenEventCannotSkipBodyClosing()
        {
            typeof(InventoryRobotPresentationController)
                .GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_controller, new object[] { InventoryRobotPresentationState.Closing });
            int closed = 0;
            _controller.CloseCompleted += () => closed++;
            _controller.NotifyRobotHidden();
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Closing));
            Assert.That(closed, Is.Zero);
            Set("_returnStarted", true);
            _controller.NotifyRobotHidden();
            _controller.NotifyRobotHidden();
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Hidden));
            Assert.That(closed, Is.EqualTo(1));
        }

        [Test]
        public void LegacyRobotCanOpenCloseAndReopenWithoutBodyAnimator()
        {
            Assert.That(_controller.RequestOpen(), Is.True);
            Assert.That(_controller.IsOpen, Is.True);
            Assert.That(_controller.RequestClose(), Is.True);
            Assert.That(_controller.State, Is.EqualTo(InventoryRobotPresentationState.Hidden));
            Assert.That(_controller.RequestOpen(), Is.True);
            Assert.That(_opened, Is.EqualTo(2));
        }

        private void BeginOpening() => typeof(InventoryRobotPresentationController)
            .GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(_controller, new object[] { InventoryRobotPresentationState.Opening });

        private void Complete() => typeof(InventoryRobotPresentationController)
            .GetMethod("TryCompleteOpening", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(_controller, null);

        private void InvokeCallback(string methodName) => typeof(InventoryRobotPresentationController)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(_controller, null);

        private void Set(string name, object value) => typeof(InventoryRobotPresentationController)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_controller, value);
    }
}
