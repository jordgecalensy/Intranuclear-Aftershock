using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Failsafe.Enemies.Sensors;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Failsafe.Enemies.Tests
{
    public sealed class EnemyPatrolNavigationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<NavMeshBuildSource> _sources = new List<NavMeshBuildSource>();
        private readonly Vector3 _origin = new Vector3(1000f, 1000f, 1000f);
        private NavMeshData _data;
        private NavMeshDataInstance _navMesh;
        private NavMeshAgent _agent;
        private EnemyMovement _movement;
        private EnemyLinkTraverser _traverser;
        private Enemy_ScriptableObject _config;
        private float _previousCaptureDeltaTime;

        [UnitySetUp]
        public IEnumerator EnterSimulation()
        {
            yield return new EnterPlayMode();
            _previousCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
        }

        [UnityTearDown]
        public IEnumerator ExitSimulation()
        {
            Time.captureDeltaTime = _previousCaptureDeltaTime;
            foreach (GameObject obj in _objects)
                Object.DestroyImmediate(obj);
            _objects.Clear();
            _sources.Clear();
            if (_navMesh.valid)
                _navMesh.Remove();
            if (_data != null)
                Object.DestroyImmediate(_data);
            if (_config != null)
                Object.DestroyImmediate(_config);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PatrolVisitsRaisedMarkersInOrderOnOneFlatFloor()
        {
            FlatFloor();
            Bake(Vector3.zero, true);
            Transform[] points = Markers(new Vector3(4f, 0f, 0f),
                new Vector3(4f, 0f, 4f), new Vector3(0f, 0f, 4f));
            PatrolState patrol = CreatePatrol(points, 2f);
            patrol.Enter();
            int visited = 0;
            bool arrivedAtCurrent = false;
            int previousIndex = NextManualIndex(patrol);

            for (int frame = 0; frame < 1200 && visited < points.Length; frame++)
            {
                _traverser.CheckAndTraverseLink();
                patrol.Update();
                _movement.HandleRotationAndMovement();
                int nextIndex = NextManualIndex(patrol);
                if (nextIndex != previousIndex)
                {
                    Assert.That(arrivedAtCurrent, Is.True,
                        "Patrol must physically visit a marker before selecting the next one");
                    arrivedAtCurrent = false;
                    previousIndex = nextIndex;
                }

                int currentIndex = (nextIndex + points.Length - 1) % points.Length;
                if (!arrivedAtCurrent && !_movement.IsRotatingInPlace && _agent.isStopped &&
                    HorizontalDistance(_agent.transform.position, points[currentIndex].position) < 0.75f)
                {
                    Assert.That(currentIndex, Is.EqualTo(visited));
                    arrivedAtCurrent = true;
                    visited++;
                }
                yield return null;
            }

            Assert.That(visited, Is.EqualTo(3),
                "Raised markers over a continuous floor must all be reached without stalled or skipped legs");
            Assert.That(_traverser.IsTraversing, Is.False);
        }

        [UnityTest]
        public IEnumerator PatrolDoesNotInheritLongStoppingDistanceFromChase()
        {
            FlatFloor();
            Bake(Vector3.zero, true);
            _agent.stoppingDistance = 10f;
            Transform[] points = Markers(Vector3.forward * 8f, Vector3.right * 8f);
            PatrolState patrol = CreatePatrol(points, 1f);
            patrol.Enter();
            Vector3 start = _agent.transform.position;

            for (int frame = 0; frame < 90; frame++)
            {
                _traverser.CheckAndTraverseLink();
                patrol.Update();
                _movement.HandleRotationAndMovement();
                Assert.That(NextManualIndex(patrol), Is.EqualTo(1),
                    "A distant patrol marker must not be counted as reached using the chase attack range");
                yield return null;
            }

            Assert.That(_agent.stoppingDistance, Is.LessThan(1f));
            Assert.That(HorizontalDistance(start, _agent.transform.position), Is.GreaterThan(0.75f));
            Assert.That(_agent.isStopped, Is.False);
        }

        [UnityTest]
        public IEnumerator EndOfPartialPathIsNotArrivalAtUnreachableDestination()
        {
            Floor(Vector3.zero, new Vector3(4f, 0.4f, 4f));
            Floor(Vector3.right * 8f, new Vector3(4f, 0.4f, 4f));
            Bake(Vector3.right * 1.5f);
            Vector3 destination = _origin + Vector3.right * 8f;
            _movement.MoveTo(destination, 2f);
            for (int frame = 0; frame < 120; frame++)
            {
                _movement.HandleRotationAndMovement();
                yield return null;
            }

            Assert.That(_agent.pathPending, Is.False);
            Assert.That(_agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathPartial),
                "Control: the disconnected destination must produce a partial path");
            Assert.That(_agent.remainingDistance, Is.LessThan(0.5f),
                "Control: the agent must reach the accessible end of the partial path");
            Assert.That(HorizontalDistance(_agent.transform.position, destination), Is.GreaterThan(4f));
            Assert.That(_movement.IsPointReached(0.5f), Is.False);
        }

        [UnityTest]
        public IEnumerator ClearedPathFarFromCommandedDestinationIsNotArrival()
        {
            FlatFloor();
            Bake(Vector3.zero);
            _movement.MoveTo(_origin + Vector3.forward * 8f, 1f);
            for (int frame = 0; frame < 5; frame++)
                yield return null;
            Assert.That(_agent.hasPath, Is.True, "Control: the destination must initially have a valid path");

            _agent.ResetPath();

            Assert.That(_agent.hasPath, Is.False);
            Assert.That(_movement.IsPointReached(0.5f), Is.False,
                "A cleared path with remainingDistance zero must not count as a reached destination");
        }

        [UnityTest]
        public IEnumerator StopSurvivesMotorUpdatesUntilNextMoveCommand()
        {
            FlatFloor();
            Bake(Vector3.zero);
            Vector3 destination = _origin + Vector3.forward * 8f;
            _movement.MoveTo(destination, 2f);
            for (int frame = 0; frame < 15; frame++)
            {
                _movement.HandleRotationAndMovement();
                yield return null;
            }

            _movement.Stop();
            Vector3 stoppedPosition = _agent.transform.position;
            for (int frame = 0; frame < 30; frame++)
            {
                _movement.HandleRotationAndMovement();
                Assert.That(_agent.isStopped, Is.True,
                    "The regular motor update must preserve an explicit Stop command");
                yield return null;
            }
            Assert.That(HorizontalDistance(stoppedPosition, _agent.transform.position), Is.LessThan(0.05f));

            _movement.MoveTo(destination, 2f);
            for (int frame = 0; frame < 60; frame++)
            {
                _movement.HandleRotationAndMovement();
                yield return null;
            }
            Assert.That(_agent.isStopped, Is.False);
            Assert.That(HorizontalDistance(stoppedPosition, _agent.transform.position), Is.GreaterThan(0.75f));
        }

        [UnityTest]
        public IEnumerator SlowContinuousMotionDoesNotSkipPatrolPoint()
        {
            FlatFloor();
            Bake(Vector3.zero);
            PatrolState patrol = CreatePatrol(Markers(Vector3.forward * 8f,
                Vector3.right * 8f, Vector3.back * 8f), 0.3f);
            patrol.Enter();
            Vector3 start = _agent.transform.position;

            for (int frame = 0; frame < 240; frame++)
            {
                patrol.Update();
                _movement.HandleRotationAndMovement();
                Assert.That(NextManualIndex(patrol), Is.EqualTo(1),
                    "Five millimeters of progress per frame at 60 FPS is slow motion, not a blocked patrol");
                yield return null;
            }

            Assert.That(HorizontalDistance(start, _agent.transform.position), Is.GreaterThan(0.9f));
            Assert.That(_movement.IsPointReached(0.5f), Is.False);
        }

        [UnityTest]
        public IEnumerator ArrivalDoesNotAlsoAdvanceStuckPointInSameUpdate()
        {
            FlatFloor();
            Bake(Vector3.zero);
            Transform[] points = Markers(Vector3.zero, Vector3.forward * 6f, Vector3.right * 6f);
            points[0].position = _agent.nextPosition;
            PatrolState patrol = CreatePatrol(points, 2f);
            patrol.Enter();
            for (int frame = 0; frame < 5; frame++)
                yield return null;
            Assert.That(_movement.IsPointReached(0.5f), Is.True,
                "Control: the first marker must already be reached before updating patrol");
            typeof(PatrolState).GetField("_stuckTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(patrol, 2.1f);
            typeof(PatrolState).GetField("_lastPosition", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(patrol, _agent.transform.position);

            patrol.Update();
            _movement.HandleRotationAndMovement();

            Assert.That(NextManualIndex(patrol), Is.EqualTo(1),
                "Arrival must enter the waiting period without selecting another point in the same update");
            Assert.That(_agent.isStopped, Is.True);
        }

        [UnityTest]
        public IEnumerator AcceptedAuthoredLinkContinuesWalkingBeyondLandingToRequestedGoal()
        {
            Floor(Vector3.zero, new Vector3(4f, 0.4f, 4f));
            Floor(Vector3.right * 8f, new Vector3(8f, 0.4f, 4f));
            Bake(Vector3.zero, true);
            typeof(EnemyLinkTraverser).GetField("_enableShortcutPlanning",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_traverser, false);
            Assert.That(NavMesh.SamplePosition(_origin + Vector3.right * 1.5f,
                out NavMeshHit takeoff, 0.25f, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(_origin + Vector3.right * 4.5f,
                out NavMeshHit landing, 0.25f, NavMesh.AllAreas), Is.True);
            NavMeshLinkInstance link = NavMesh.AddLink(new NavMeshLinkData
            {
                startPosition = takeoff.position, endPosition = landing.position,
                agentTypeID = _agent.agentTypeID, area = NavMesh.GetAreaFromName("Jump"),
                bidirectional = true, costModifier = 1f, width = 0.5f
            });
            Assert.That(link.valid, Is.True);
            Vector3 destination = _origin + Vector3.right * 10f;
            _movement.MoveTo(destination, 3f);
            bool jumped = false;
            bool landedAwayFromGoal = false;
            try
            {
                for (int frame = 0; frame < 900; frame++)
                {
                    _traverser.CheckAndTraverseLink();
                    _movement.HandleRotationAndMovement();
                    jumped |= _traverser.IsTraversing;
                    if (jumped && !_traverser.IsTraversing &&
                        HorizontalDistance(_agent.transform.position, destination) > 4f)
                        landedAwayFromGoal = true;
                    if (jumped && !_traverser.IsTraversing && _movement.IsPointReached(0.5f))
                        break;
                    yield return null;
                }

                Assert.That(jumped, Is.True, "Control: the authored link must be traversed as a jump");
                Assert.That(landedAwayFromGoal, Is.True,
                    "Control: the destination must require walking after the authored link landing");
                Assert.That(HorizontalDistance(_agent.transform.position, destination), Is.LessThan(0.6f),
                    "Finishing the jump must restore the pending walking destination beyond its landing");
            }
            finally
            {
                link.Remove();
            }
        }

        [UnityTest]
        public IEnumerator SmallSuccessiveTargetMovesRefreshProjectedWalkingDestination()
        {
            FlatFloor();
            Bake(Vector3.zero, true);
            Vector3 destination = _origin + Vector3.up * 0.9f;
            for (int step = 0; step <= 60; step++)
            {
                destination = _origin + new Vector3(0f, 0.9f, 2f + step * 0.1f);
                _movement.MoveTo(destination, 3f);
                _traverser.CheckAndTraverseLink();
                _movement.HandleRotationAndMovement();
                yield return null;
            }

            for (int frame = 0; frame < 300; frame++)
            {
                _movement.MoveTo(destination, 3f);
                _traverser.CheckAndTraverseLink();
                _movement.HandleRotationAndMovement();
                yield return null;
            }

            Assert.That(HorizontalDistance(_agent.destination, destination), Is.LessThanOrEqualTo(0.21f),
                "Small target changes must accumulate relative to the last projected goal, not the last request");
            Assert.That(HorizontalDistance(_agent.transform.position, destination), Is.LessThan(0.55f),
                "The agent must follow the final target instead of stopping at an outdated projection");
            Assert.That(_movement.IsPointReached(0.5f), Is.True);
        }

        [UnityTest]
        public IEnumerator ArrivalOnRampUsesStoppingRadiusDespiteVerticalOffset()
        {
            Quaternion slope = Quaternion.Euler(-30f, 0f, 0f);
            Vector3 dimensions = new Vector3(4f, 0.4f, 16f);
            var ramp = new GameObject("Patrol test ramp");
            _objects.Add(ramp);
            ramp.transform.SetPositionAndRotation(_origin - slope * Vector3.up * dimensions.y * 0.5f, slope);
            ramp.AddComponent<BoxCollider>().size = dimensions;
            _sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box, size = dimensions,
                transform = ramp.transform.localToWorldMatrix, area = 0
            });
            Bake(slope * Vector3.back * 5f);
            _agent.stoppingDistance = 1f;
            Assert.That(NavMesh.SamplePosition(_origin + slope * Vector3.forward * 4f,
                out NavMeshHit goal, 0.25f, NavMesh.AllAreas), Is.True);
            _movement.MoveTo(goal.position, 2f);

            for (int frame = 0; frame < 600; frame++)
            {
                _movement.HandleRotationAndMovement();
                yield return null;
                if (frame > 120 && !_agent.pathPending && _agent.velocity.sqrMagnitude < 0.001f)
                    break;
            }

            Assert.That(_agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(_agent.remainingDistance, Is.LessThanOrEqualTo(1.05f));
            Assert.That(Vector3.Distance(_agent.nextPosition, goal.position), Is.LessThanOrEqualTo(1.05f));
            Assert.That(Mathf.Abs(_agent.nextPosition.y - goal.position.y), Is.GreaterThan(0.25f),
                "Control: stopping on the ramp must leave a real vertical offset above the former fixed limit");
            Assert.That(_movement.IsPointReached(_agent.stoppingDistance + 0.05f), Is.True,
                "A complete walking path within the stopping radius must count as arrival on a ramp");
        }

        [UnityTest]
        public IEnumerator BlockedPatrolRetriesCurrentPointBeforeAdvancing()
        {
            FlatFloor();
            Bake(Vector3.zero);
            Transform[] points = Markers(Vector3.forward * 8f, Vector3.right * 8f);
            PatrolState patrol = CreatePatrol(points, 2f);
            patrol.Enter();
            Vector3 blockedPosition = _agent.transform.position;
            _agent.updatePosition = false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo attemptsField = typeof(PatrolState).GetField("_recoveryAttempts", flags);
            FieldInfo stuckTimerField = typeof(PatrolState).GetField("_stuckTimer", flags);
            float longestStuckTime = 0f;
            int pendingFrames = 0;

            for (int frame = 0; frame < 160; frame++)
            {
                if (frame == 10)
                {
                    Assert.That(_agent.hasPath, Is.True, "Control: the initial patrol path must be ready");
                    _agent.ResetPath();
                    Assert.That(_agent.hasPath, Is.False, "Control: this scenario must lose its active path");
                }
                // A root-motion motor unable to make progress keeps the navigation
                // simulation at its actual position instead of letting it walk ahead.
                _agent.nextPosition = blockedPosition;
                _agent.velocity = Vector3.zero;
                if (_agent.pathPending)
                    pendingFrames++;
                patrol.Update();
                longestStuckTime = Mathf.Max(longestStuckTime, (float)stuckTimerField.GetValue(patrol));
                Assert.That(NextManualIndex(patrol), Is.EqualTo(1),
                    "The first recovery after two seconds must retry the current manual point");
                yield return null;
            }

            // Reissuing the same path can complete without an observable pathPending
            // transition. Inspect the recovery counter while retaining the physical
            // position and selected-destination assertions below.
            Assert.That((int)attemptsField.GetValue(patrol), Is.EqualTo(1),
                $"Expected one recovery; longest stuck time={longestStuckTime:F3}, pending frames={pendingFrames}, " +
                $"current timer={(float)stuckTimerField.GetValue(patrol):F3}, path pending={_agent.pathPending}, " +
                $"on NavMesh={_agent.isOnNavMesh}, " +
                $"remaining distance={_agent.remainingDistance:F3}, actual movement=" +
                $"{Vector3.Distance(_agent.transform.position, blockedPosition):F3}");
            Assert.That((float)stuckTimerField.GetValue(patrol), Is.LessThan(1f),
                "Retrying the current point must start a fresh progress interval");
            Assert.That(_agent.hasPath, Is.True, "Recovery must rebuild the path that was lost during patrol");
            Assert.That(_agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(HorizontalDistance(_agent.destination, points[0].position), Is.LessThan(0.2f));
            Assert.That(HorizontalDistance(_agent.transform.position, blockedPosition), Is.LessThan(0.02f));
        }

        private void FlatFloor()
        {
            Floor(Vector3.zero, new Vector3(24f, 0.4f, 24f));
        }

        private void Floor(Vector3 top, Vector3 dimensions)
        {
            var obj = new GameObject("Patrol test floor");
            _objects.Add(obj);
            obj.transform.position = _origin + top - Vector3.up * dimensions.y * 0.5f;
            obj.AddComponent<BoxCollider>().size = dimensions;
            _sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box, size = dimensions,
                transform = obj.transform.localToWorldMatrix, area = 0
            });
        }

        private void Bake(Vector3 start, bool withTraverser = false)
        {
            NavMeshBuildSettings build = NavMesh.GetSettingsByID(0);
            build.agentRadius = 0.2f;
            build.agentHeight = 0.8f;
            build.agentClimb = 0.2f;
            build.overrideVoxelSize = true;
            build.voxelSize = 0.05f;
            build.minRegionArea = 0f;
            _data = NavMeshBuilder.BuildNavMeshData(build, _sources,
                new Bounds(_origin, Vector3.one * 100f), Vector3.zero, Quaternion.identity);
            Assert.That(_data, Is.Not.Null);
            _navMesh = NavMesh.AddNavMeshData(_data);
            var obj = new GameObject("Patrol test agent");
            _objects.Add(obj);
            obj.transform.position = _origin + start;
            _agent = obj.AddComponent<NavMeshAgent>();
            _agent.radius = 0.2f;
            _agent.height = 0.8f;
            _agent.acceleration = 120f;
            _agent.stoppingDistance = 0.3f;
            Assert.That(_agent.Warp(_origin + start), Is.True);
            _traverser = null;
            if (withTraverser)
                _traverser = obj.AddComponent<EnemyLinkTraverser>();
            _movement = new EnemyMovement(obj.transform, _agent, null, null, false, _traverser);
            if (withTraverser)
                _traverser.Initialize(_agent, null, _movement);
            Physics.SyncTransforms();
        }

        private Transform[] Markers(params Vector3[] points)
        {
            var markers = new Transform[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                var obj = new GameObject("Raised patrol marker " + index);
                _objects.Add(obj);
                obj.transform.position = _origin + points[index] + Vector3.up * 0.9f;
                markers[index] = obj.transform;
            }
            return markers;
        }

        private PatrolState CreatePatrol(Transform[] markers, float speed)
        {
            _config = ScriptableObject.CreateInstance<Enemy_ScriptableObject>();
            _config.PatrolingSpeed = speed;
            return new PatrolState(new Sensor[0], _agent.transform, null, _movement,
                null, _agent, _config, markers, _traverser);
        }

        private static int NextManualIndex(PatrolState patrol)
        {
            return (int)typeof(PatrolState)
                .GetField("_currentManualIndex", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(patrol);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
