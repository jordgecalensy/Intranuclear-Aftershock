using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Failsafe.Enemies.Tests
{
    public sealed class SpiderMultiJumpTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<NavMeshBuildSource> _sources = new List<NavMeshBuildSource>();
        private NavMeshDataInstance _navMesh;
        private NavMeshData _data;
        private NavMeshAgent _agent;
        private readonly Vector3 _origin = new Vector3(1000f, 1000f, 1000f);

        [UnitySetUp]
        public IEnumerator EnterSimulation()
        {
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator ExitSimulation()
        {
            yield return new ExitPlayMode();
        }

        private SpiderJumpPlanner.Settings Settings => new SpiderJumpPlanner.Settings
        {
            MoveSpeed = 3f, MinHorizontalDistance = 0.8f, MaxHorizontalDistance = 4.5f,
            MaxRise = 2.5f, MaxDrop = 4f, JumpSpeed = 8f, BaseJumpDuration = 0.5f,
            JumpApexHeight = 1.5f, DropApexHeight = 0.75f,
            PreparationDuration = 0.12f, LandingRecoveryDuration = 0.18f,
            MinimumTimeSaving = 0.6f, MinimumRelativeSaving = 0.1f,
            EdgeSearchRadius = 7f, LandingInset = 0.4f, NavMeshSampleRadius = 0.5f,
            MinimumVerticalShortcut = 0.75f, MinimumWalkDetourRatio = 1.4f,
            EdgeRingSamples = 8, LandingSampleStep = 0.5f, MaximumCandidateChecks = 96,
            CollisionMask = Physics.DefaultRaycastLayers, TrajectorySegments = 16, CollisionSkin = 0.05f,
            MaximumJumps = 8, MaximumSearchNodes = 256, MaximumExpandedNodes = 64,
            TargetGroundProbeDistance = 3f
        };

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in _objects)
                Object.DestroyImmediate(obj);
            _objects.Clear();
            _sources.Clear();
            if (_navMesh.valid)
                _navMesh.Remove();
            if (_data != null)
                Object.DestroyImmediate(_data);
        }

        private void Platform(Vector3 top, Vector3? size = null)
        {
            Vector3 dimensions = size ?? new Vector3(3f, 0.4f, 3f);
            Box(top - Vector3.up * dimensions.y * 0.5f, dimensions, true);
        }

        private void Box(Vector3 center, Vector3 size, bool bake)
        {
            var obj = new GameObject("Spider route test geometry");
            _objects.Add(obj);
            obj.transform.position = _origin + center;
            obj.AddComponent<BoxCollider>().size = size;
            if (bake)
                _sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box, size = size,
                    transform = obj.transform.localToWorldMatrix, area = 0
                });
        }

        private void Bake(Vector3 start)
        {
            NavMeshBuildSettings build = NavMesh.GetSettingsByID(0);
            build.agentRadius = 0.2f;
            build.agentHeight = 0.8f;
            build.agentClimb = 0.2f;
            build.overrideVoxelSize = true;
            build.voxelSize = 0.05f;
            build.minRegionArea = 0f;
            _data = NavMeshBuilder.BuildNavMeshData(build, _sources,
                new Bounds(_origin + Vector3.up * 4f, Vector3.one * 100f), Vector3.zero, Quaternion.identity);
            Assert.That(_data, Is.Not.Null);
            _navMesh = NavMesh.AddNavMeshData(_data);
            Assert.That(NavMesh.SamplePosition(_origin + start, out _, 1f, NavMesh.AllAreas), Is.True,
                "Test platform must contain baked NavMesh");
            var obj = new GameObject("Spider route test agent");
            _objects.Add(obj);
            obj.transform.position = _origin + start;
            _agent = obj.AddComponent<NavMeshAgent>();
            _agent.radius = 0.2f;
            _agent.height = 0.8f;
            Assert.That(_agent.Warp(_origin + start), Is.True);
            Physics.SyncTransforms();
        }

        private SpiderJumpPlanner Plan(Vector3 target, SpiderJumpPlanner.Settings? settings = null)
        {
            var planner = new SpiderJumpPlanner();
            planner.TryPlan(_agent, _agent.transform, null, _origin + target, settings ?? Settings, out _);
            return planner;
        }

        private void AssertConnectedRoute(SpiderJumpPlanner planner, int jumps)
        {
            Assert.That(planner.PlannedJumps.Count, Is.EqualTo(jumps),
                $"{planner.LastFailureReason}; nodes={planner.LastExpandedNodes}; checks={planner.LastCandidateChecks}");
            var path = new NavMeshPath();
            Vector3 position = _agent.nextPosition;
            foreach (SpiderJumpPlanner.Route jump in planner.PlannedJumps)
            {
                Assert.That(NavMesh.CalculatePath(position, jump.TakeoffPosition, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(planner.IsJumpStillClear(jump, jump.TakeoffPosition, _agent, _agent.transform, null, Settings), Is.True);
                position = jump.LandingPosition;
            }
            Assert.That(NavMesh.CalculatePath(position, planner.ResolvedTargetPosition, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
        }

        [Test]
        public void PlansThreeJumpsUpSeparatePlatforms()
        {
            for (int index = 0; index < 4; index++)
                Platform(new Vector3(index * 5f, index * 2f, 0f));
            Bake(Vector3.zero);
            AssertConnectedRoute(Plan(new Vector3(15f, 6f, 0f)), 3);
        }

        [Test]
        public void PrefersIntermediatePlatformEvenWhenDirectEightMeterJumpIsPossible()
        {
            Platform(Vector3.zero);
            Platform(new Vector3(5f, 0f, 0f));
            Platform(new Vector3(10f, 0f, 0f));
            Bake(Vector3.zero);
            var settings = Settings;
            settings.MaxHorizontalDistance = 8f;
            settings.MaxRise = 8f;
            // Доступность прямого перехода проверяется независимо от порядка
            // обхода ограниченного поиска, который не обещает глобальный оптимум.
            Assert.That(NavMesh.SamplePosition(_origin + Vector3.right * 0.75f, out var start, 0.2f, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(_origin + Vector3.right * 8.75f, out var end, 0.2f, NavMesh.AllAreas), Is.True);
            Assert.That(new SpiderJumpPlanner().TryCreateJump(start.position, end.position,
                _agent, _agent.transform, null, settings, out _), Is.True,
                "Control: the long jump must really be available");
            settings.LongJumpPenalty = 6f;
            var viaPlatform = Plan(new Vector3(10f, 0f, 0f), settings);
            AssertConnectedRoute(viaPlatform, 2);
            Assert.That(viaPlatform.PlannedJumps[0].LandingPosition.x - _origin.x, Is.InRange(3.5f, 6.5f));
        }

        [Test]
        public void StillUsesLongJumpWhenThereIsNoIntermediatePlatform()
        {
            Platform(Vector3.zero);
            Platform(new Vector3(9f, 0f, 0f));
            Bake(Vector3.zero);
            var settings = Settings;
            settings.MaxHorizontalDistance = 8f;
            settings.LongJumpPenalty = 6f;
            var planner = Plan(new Vector3(9f, 0f, 0f), settings);
            Assert.That(planner.PlannedJumps.Count, Is.EqualTo(1), planner.LastFailureReason.ToString());
            float distance = Vector3.Distance(planner.PlannedJumps[0].TakeoffPosition, planner.PlannedJumps[0].LandingPosition);
            Assert.That(distance, Is.GreaterThan(5f).And.LessThanOrEqualTo(8f));
        }

        [Test]
        public void BlockedFirstCandidateDoesNotExhaustIntermediatePlatformSearch()
        {
            Platform(Vector3.zero);
            Platform(new Vector3(4f, 0f, 5f));
            Platform(new Vector3(8f, 0f, 0f));
            Box(new Vector3(4f, 3f, 0f), new Vector3(0.5f, 10f, 5f), false);
            Bake(Vector3.zero);
            var settings = Settings;
            settings.MaxHorizontalDistance = 8f;
            settings.LongJumpPenalty = 6f;
            settings.MaximumCandidateChecks = 1;
            var planner = Plan(new Vector3(8f, 0f, 0f), settings);
            Assert.That(planner.PlannedJumps.Count, Is.GreaterThanOrEqualTo(2), planner.LastFailureReason.ToString());
            Assert.That(planner.LastCandidateChecks, Is.GreaterThan(1));
            Assert.That(planner.PlannedJumps[0].LandingPosition.z - _origin.z, Is.GreaterThan(3f));
        }

        [Test]
        public void RejectsTakeoffDisplacedBeyondJumpLimit()
        {
            Platform(Vector3.zero);
            Platform(new Vector3(5f, 2f, 0f));
            Bake(Vector3.zero);
            var planner = Plan(new Vector3(5f, 2f, 0f));
            Assert.That(planner.PlannedJumps, Is.Not.Empty);
            var jump = planner.PlannedJumps[0];
            var settings = Settings;
            settings.MaxHorizontalDistance = 1f;
            Assert.That(planner.IsJumpStillClear(jump, jump.TakeoffPosition, _agent, _agent.transform, null, settings), Is.False);
        }

        [Test]
        public void PlansFlatGapChainAndDrops()
        {
            Platform(new Vector3(0f, 4f, 0f));
            Platform(new Vector3(5f, 4f, 0f));
            Platform(new Vector3(10f, 2f, 0f));
            Platform(new Vector3(15f, 0f, 0f));
            Bake(new Vector3(0f, 4f, 0f));
            AssertConnectedRoute(Plan(new Vector3(15f, 0f, 0f)), 3);
        }

        [Test]
        public void ConsidersIntermediateFloorBelowAnotherPlatform()
        {
            Platform(Vector3.zero);
            Platform(new Vector3(5f, 2f, 0f));
            Platform(new Vector3(5f, 5.5f, 0f));
            Platform(new Vector3(10f, 4f, 0f));
            Bake(Vector3.zero);
            SpiderJumpPlanner planner = Plan(new Vector3(10f, 4f, 0f));
            AssertConnectedRoute(planner, 2);
            Assert.That(planner.PlannedJumps[0].LandingPosition.y, Is.EqualTo(_origin.y + 2f).Within(0.15f));
        }

        [Test]
        public void UsesLowArcBetweenStackedPlatforms()
        {
            // Геометрия склада: два стеллажа, проход между ними и потолок
            // над точкой отрыва. Обычная дуга 2 м ударяется в верхнюю полку.
            for (int floor = 0; floor < 4; floor++)
            {
                Platform(new Vector3(0f, floor * 2.65f, 0f), new Vector3(4f, 0.1f, 12f));
                Platform(new Vector3(8.44f, floor * 2.65f, 0f), new Vector3(4f, 0.1f, 12f));
            }
            Bake(Vector3.zero);
            _agent.radius = 0.5f;
            _agent.height = 0.35f;
            var settings = Settings;
            settings.MaxHorizontalDistance = 8f;
            settings.MaxRise = 8f;
            settings.JumpApexHeight = 2f;
            settings.LandingInset = 0.65f;
            settings.LongJumpPenalty = 6f;
            var planner = new SpiderJumpPlanner();
            var search = planner.Plan(_agent, _agent.transform, null,
                _origin + new Vector3(8.44f, 7.95f, 0f), settings);
            int batches = 0;
            while (search.MoveNext())
                Assert.That(++batches, Is.LessThan(512), "Do not exhaust every lower-floor sample before continuing upward");
            Assert.That(planner.PlannedJumps.Count, Is.GreaterThanOrEqualTo(3), planner.LastFailureReason.ToString());
            Assert.That(planner.PlannedJumps[0].ApexHeight, Is.LessThan(2f));
            foreach (var jump in planner.PlannedJumps)
                Assert.That(planner.IsJumpStillClear(jump, jump.TakeoffPosition, _agent, _agent.transform, null, settings), Is.True);
        }

        [Test]
        public void CanInitiallyMoveAwayFromTargetToGoAroundGap()
        {
            Platform(Vector3.zero);
            Platform(new Vector3(0f, 0f, 5f));
            Platform(new Vector3(5f, 0f, 5f));
            Platform(new Vector3(10f, 0f, 5f));
            Platform(new Vector3(10f, 0f, 0f));
            Bake(Vector3.zero);
            var settings = Settings;
            settings.MaxHorizontalDistance = 3.5f;
            AssertConnectedRoute(Plan(new Vector3(10f, 0f, 0f), settings), 4);
        }

        [Test]
        public void JumpLimitNeverReturnsIncompleteChain()
        {
            for (int index = 0; index < 4; index++)
                Platform(new Vector3(index * 5f, index * 2f, 0f));
            Bake(Vector3.zero);
            var settings = Settings;
            settings.MaximumJumps = 2;
            var planner = Plan(new Vector3(15f, 6f, 0f), settings);
            Assert.That(planner.PlannedJumps, Is.Empty);
            Assert.That(planner.LastFailureReason, Is.EqualTo(SpiderJumpPlanner.PlanFailureReason.SearchBudgetExceeded));
        }

        [Test]
        public void RejectsWallAcrossOnlyJumpRoute()
        {
            Platform(Vector3.zero);
            Platform(new Vector3(5f, 2f, 0f));
            Box(new Vector3(2.5f, 5f, 0f), new Vector3(0.5f, 16f, 30f), false);
            Bake(Vector3.zero);
            Assert.That(Plan(new Vector3(5f, 2f, 0f)).PlannedJumps, Is.Empty);
        }

        [Test]
        public void ResolvesRaisedMarkerOntoFloorBelowWithoutSnappingUpstairs()
        {
            Platform(Vector3.zero, new Vector3(6f, 0.4f, 6f));
            Platform(new Vector3(0f, 3f, 0f), new Vector3(6f, 0.4f, 6f));
            Bake(Vector3.zero);
            var planner = Plan(new Vector3(0f, 1.8f, 0f));
            Assert.That(planner.HasResolvedTarget, Is.True);
            Assert.That(planner.ResolvedTargetPosition.y, Is.EqualTo(_origin.y).Within(0.15f));
            Assert.That(planner.PlannedJumps, Is.Empty);
        }

        [Test]
        public void SearchYieldsAndReportsUnreachableTargetWithinBudget()
        {
            Platform(Vector3.zero);
            Platform(new Vector3(5f, 0f, 0f));
            Platform(new Vector3(30f, 0f, 0f));
            Bake(Vector3.zero);
            var settings = Settings;
            settings.MaximumExpandedNodes = 2;
            var planner = new SpiderJumpPlanner();
            var search = planner.Plan(_agent, _agent.transform, null, _origin + new Vector3(30f, 0f, 0f), settings);
            int yields = 0;
            while (search.MoveNext())
                Assert.That(++yields, Is.LessThan(5000));
            Assert.That(yields, Is.GreaterThan(1));
            Assert.That(planner.PlannedJumps, Is.Empty);
            Assert.That(planner.LastExpandedNodes, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void TraverserRetainsNextJumpAndRestoresAgentAfterLastLanding()
        {
            Platform(Vector3.zero);
            Bake(Vector3.zero);
            EnemyLinkTraverser traverser = _agent.gameObject.AddComponent<EnemyLinkTraverser>();
            traverser.Initialize(_agent, null, null);
            Vector3 point = _agent.nextPosition;
            var jump = new SpiderJumpPlanner.Route(point, point, point, 10f, 5f, 0.5f, 1f);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(EnemyLinkTraverser);
            var jumps = (List<SpiderJumpPlanner.Route>)type.GetField("_routeJumps", flags).GetValue(traverser);
            jumps.Add(jump);
            jumps.Add(jump);
            type.GetField("_plannedRoute", flags).SetValue(traverser, jump);
            type.GetField("_destinationAreaMask", flags).SetValue(traverser, NavMesh.AllAreas);
            type.GetField("_hasStoredStoppingDistance", flags).SetValue(traverser, true);
            type.GetField("_destinationStoppingDistance", flags).SetValue(traverser, 2f);
            _agent.stoppingDistance = 0.05f;
            _agent.areaMask = SpiderJumpPlanner.GetWalkOnlyAreaMask(NavMesh.AllAreas);
            var finish = type.GetMethod("FinishTraversal", flags);
            finish.Invoke(traverser, new object[] { point, false });
            Assert.That(traverser.HasCommittedTraversal, Is.True);
            Assert.That(jumps.Count, Is.EqualTo(2));
            finish.Invoke(traverser, new object[] { point, false });
            Assert.That(traverser.HasCommittedTraversal, Is.False);
            Assert.That(_agent.areaMask, Is.EqualTo(NavMesh.AllAreas));
            Assert.That(_agent.stoppingDistance, Is.EqualTo(2f));
        }

        [UnityTest]
        public IEnumerator TraverserExecutesWholeChainWithoutNewDestinationRequests()
        {
            for (int index = 0; index < 4; index++)
                Platform(new Vector3(index * 5f, index * 2f, 0f));
            Bake(Vector3.zero);
            EnemyLinkTraverser traverser = _agent.gameObject.AddComponent<EnemyLinkTraverser>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(EnemyLinkTraverser).GetField("_maximumHorizontalDistance", flags).SetValue(traverser, 4.5f);
            typeof(EnemyLinkTraverser).GetField("_maximumRise", flags).SetValue(traverser, 2.5f);
            traverser.Initialize(_agent, null, null);
            Vector3 goal = _origin + new Vector3(15f, 6f, 0f);
            traverser.SetDestination(goal, 3f);
            float previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            int completedJumps = 0;
            bool wasTraversing = false;
            try
            {
                for (int frame = 0; frame < 1800; frame++)
                {
                    traverser.CheckAndTraverseLink();
                    yield return null;
                    if (wasTraversing && !traverser.IsTraversing)
                        completedJumps++;
                    wasTraversing = traverser.IsTraversing;
                    if (Vector3.Distance(_agent.transform.position, goal) < 0.4f && !traverser.HasCommittedTraversal)
                        break;
                }
                Assert.That(completedJumps, Is.EqualTo(3));
                Assert.That(Vector3.Distance(_agent.transform.position, goal), Is.LessThan(0.4f));
                Assert.That(traverser.HasCommittedTraversal, Is.False);
                Assert.That(_agent.updatePosition, Is.True);
                Assert.That(_agent.areaMask, Is.EqualTo(NavMesh.AllAreas));
            }
            finally
            {
                Time.captureDeltaTime = previousCapture;
            }
        }

        [Test]
        public void MovingTargetCancelsPendingSearchAndCommittedChain()
        {
            Platform(Vector3.zero);
            Bake(Vector3.zero);
            EnemyLinkTraverser traverser = _agent.gameObject.AddComponent<EnemyLinkTraverser>();
            traverser.Initialize(_agent, null, null);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(EnemyLinkTraverser);
            Vector3 oldTarget = _agent.nextPosition;
            var planner = new SpiderJumpPlanner();
            type.GetField("_planningTarget", flags).SetValue(traverser, oldTarget);
            type.GetField("_planningSearch", flags).SetValue(traverser,
                planner.Plan(_agent, _agent.transform, null, oldTarget, Settings));
            Assert.That(traverser.HasCommittedTraversal, Is.True);
            traverser.SetDestination(oldTarget + Vector3.right * 3f, 3f);
            Assert.That(traverser.HasCommittedTraversal, Is.False);

            var jump = new SpiderJumpPlanner.Route(oldTarget, oldTarget, oldTarget, 10f, 5f, 0.5f, 1f);
            type.GetField("_plannedRoute", flags).SetValue(traverser, jump);
            type.GetField("_destinationAreaMask", flags).SetValue(traverser, NavMesh.AllAreas);
            ((List<SpiderJumpPlanner.Route>)type.GetField("_routeJumps", flags).GetValue(traverser)).Add(jump);
            traverser.SetDestination(oldTarget + Vector3.left * 3f, 3f);
            Assert.That(traverser.HasCommittedTraversal, Is.False);
            Assert.That(_agent.areaMask, Is.EqualTo(NavMesh.AllAreas));
        }

        [Test]
        public void BlockedJumpNearTakeoffDoesNotKeepCommittedRouteForever()
        {
            Platform(Vector3.zero);
            Bake(Vector3.zero);
            var traverser = _agent.gameObject.AddComponent<EnemyLinkTraverser>();
            traverser.Initialize(_agent, null, null);
            Vector3 feet = _agent.nextPosition;
            // Внутри допуска отрыва, но дальше дистанции остановки.
            var jump = new SpiderJumpPlanner.Route(feet + Vector3.right * 0.2f,
                feet + Vector3.right * 20f, feet, 10f, 5f, 0.5f, 1f);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(EnemyLinkTraverser);
            type.GetField("_plannedRoute", flags).SetValue(traverser, jump);
            type.GetField("_destinationAreaMask", flags).SetValue(traverser, _agent.areaMask);
            type.GetField("_lastApproachDistance", flags).SetValue(traverser, 0.2f);
            type.GetField("_approachStuckTime", flags).SetValue(traverser, 3.1f);
            traverser.CheckAndTraverseLink();
            Assert.That(traverser.IsTraversing, Is.False);
            Assert.That(traverser.HasCommittedTraversal, Is.False,
                "Unusable jump inside the takeoff tolerance must release the route for replanning");
        }

        [UnityTest]
        public IEnumerator LongNavMeshLinkCannotBypassJumpLimits()
        {
            Platform(Vector3.zero);
            Platform(new Vector3(16f, 0f, 0f));
            Bake(Vector3.zero);
            var link = NavMesh.AddLink(new NavMeshLinkData
            {
                startPosition = _agent.nextPosition,
                endPosition = _origin + new Vector3(16f, 0.03f, 0f),
                agentTypeID = _agent.agentTypeID, area = NavMesh.GetAreaFromName("Jump"),
                bidirectional = true, costModifier = 0.1f, width = 0.5f
            });
            Assert.That(link.valid, Is.True);
            var traverser = _agent.gameObject.AddComponent<EnemyLinkTraverser>();
            traverser.Initialize(_agent, null, null);
            traverser.SetDestination(_origin + new Vector3(16f, 0f, 0f), 3f);
            float previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                bool encounteredLink = false;
                for (int frame = 0; frame < 240; frame++)
                {
                    encounteredLink |= _agent.isOnOffMeshLink;
                    traverser.CheckAndTraverseLink();
                    Assert.That(traverser.IsTraversing, Is.False);
                    yield return null;
                }
                Assert.That(encounteredLink, Is.True, "Control: the agent must encounter the authored link");
                Assert.That(_agent.transform.position.x - _origin.x, Is.LessThan(3f));
                traverser.ClearDestination();
                Assert.That(_agent.areaMask, Is.EqualTo(NavMesh.AllAreas));
            }
            finally
            {
                link.Remove();
                Time.captureDeltaTime = previousCapture;
            }
        }
    }
}
