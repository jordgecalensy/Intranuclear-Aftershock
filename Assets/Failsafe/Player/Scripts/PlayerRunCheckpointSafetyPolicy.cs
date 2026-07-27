using System;
using Failsafe.Player.View;
using Failsafe.Player.Scripts.Interaction;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.Configs;
using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.Health;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Player.Scripts
{
    public sealed class PlayerRunCheckpointSafetyPolicy :
        IRunCheckpointSafetyPolicy,
        IInitializable,
        ITickable,
        IDisposable
    {
        private readonly IRestorableHealth _health;
        private readonly PlayerMovementController _movementController;
        private readonly InputHandler _inputHandler;
        private readonly DamageableComponent _damageable;
        private readonly PlayerView _playerView;
        private readonly EnemyRuntimeRegistry _enemyRegistry;
        private readonly GameConfig _gameConfig;
        private readonly PhysicsInteraction _physicsInteraction;
        private readonly PlayerHandsContainer _playerHandsContainer;

        private float _previousHealth;
        private float _lastCombatActivityAt = float.NegativeInfinity;
        private float _lastDotDamageAt = float.NegativeInfinity;
        private StatusEffectState _statusEffectState;

        public PlayerRunCheckpointSafetyPolicy(
            IRestorableHealth health,
            PlayerMovementController movementController,
            InputHandler inputHandler,
            DamageableComponent damageable,
            PlayerView playerView,
            EnemyRuntimeRegistry enemyRegistry,
            GameConfig gameConfig,
            PhysicsInteraction physicsInteraction,
            PlayerHandsContainer playerHandsContainer)
        {
            _health =
                health ?? throw new ArgumentNullException(nameof(health));
            _movementController =
                movementController ??
                throw new ArgumentNullException(nameof(movementController));
            _inputHandler =
                inputHandler ??
                throw new ArgumentNullException(nameof(inputHandler));
            _damageable =
                damageable ??
                throw new ArgumentNullException(nameof(damageable));
            _playerView =
                playerView ??
                throw new ArgumentNullException(nameof(playerView));
            _enemyRegistry =
                enemyRegistry ??
                throw new ArgumentNullException(nameof(enemyRegistry));
            _gameConfig =
                gameConfig ??
                throw new ArgumentNullException(nameof(gameConfig));
            _physicsInteraction =
                physicsInteraction ??
                throw new ArgumentNullException(nameof(physicsInteraction));
            _playerHandsContainer =
                playerHandsContainer ??
                throw new ArgumentNullException(nameof(playerHandsContainer));
        }

        public void Initialize()
        {
            _previousHealth = _health.CurrentHealth;

            _health.OnHealthChanged += HandleHealthChanged;
            _health.OnStateRestored += HandleHealthStateRestored;
            _damageable.OnTakeDamage += HandleDamageTaken;
        }

        public void Tick()
        {
            if (_inputHandler.AttackTrigger.IsTriggered ||
                _inputHandler.AttackTrigger.IsPressed)
            {
                _lastCombatActivityAt = Time.realtimeSinceStartup;
            }
        }

        public void Dispose()
        {
            _health.OnHealthChanged -= HandleHealthChanged;
            _health.OnStateRestored -= HandleHealthStateRestored;
            _damageable.OnTakeDamage -= HandleDamageTaken;
        }

        public RunCheckpointSafetyDecision Evaluate()
        {
            if (_health.IsDead)
            {
                return RunCheckpointSafetyDecision.Blocked(
                    RunCheckpointBlockReason.PlayerDead,
                    "the player is dead");
            }

            float groundedSeconds =
                _gameConfig.CheckpointGroundedSeconds;

            if (!_movementController.IsGrounded ||
                !_movementController.IsGroundedFor(groundedSeconds))
            {
                return RunCheckpointSafetyDecision.Blocked(
                    RunCheckpointBlockReason.Airborne,
                    $"the player has not been grounded for " +
                    $"{groundedSeconds:0.##} seconds");
            }

            if (_physicsInteraction.IsDragging)
            {
                return RunCheckpointSafetyDecision.Blocked(
                    RunCheckpointBlockReason.CarryingObject,
                    "the player is carrying a physics object");
            }

            if (_playerHandsContainer.State ==
                PlayerHandsContainer.HandState.ItemInHand)
            {
                return RunCheckpointSafetyDecision.Blocked(
                    RunCheckpointBlockReason.CarryingObject,
                    "the player is holding an inventory item");
            }

            if (HasActiveDamageOverTime())
            {
                return RunCheckpointSafetyDecision.Blocked(
                    RunCheckpointBlockReason.DamageOverTime,
                    "a damage-over-time effect is active");
            }

            float dotCooldownRemaining =
                RemainingCooldown(
                    _lastDotDamageAt,
                    _gameConfig.CheckpointDotCooldownSeconds);

            if (dotCooldownRemaining > 0f)
            {
                return RunCheckpointSafetyDecision.Blocked(
                    RunCheckpointBlockReason.DamageOverTime,
                    $"damage over time was received recently " +
                    $"({dotCooldownRemaining:0.##} seconds remaining)");
            }

            foreach (EnemyRuntimeEntry entry in _enemyRegistry.Entries)
            {
                Enemy enemy = entry.Enemy;
                if (enemy == null || !enemy.IsEngagedWithPlayer)
                    continue;

                return RunCheckpointSafetyDecision.Blocked(
                    RunCheckpointBlockReason.Combat,
                    $"enemy '{enemy.name}' is engaged with the player");
            }

            float combatCooldownRemaining =
                RemainingCooldown(
                    _lastCombatActivityAt,
                    _gameConfig.CheckpointCombatCooldownSeconds);

            if (combatCooldownRemaining > 0f)
            {
                return RunCheckpointSafetyDecision.Blocked(
                    RunCheckpointBlockReason.Combat,
                    $"the player attacked or received damage recently " +
                    $"({combatCooldownRemaining:0.##} seconds remaining)");
            }

            return RunCheckpointSafetyDecision.Allowed();
        }

        private void HandleHealthChanged(float health)
        {
            if (health < _previousHealth)
                _lastCombatActivityAt = Time.realtimeSinceStartup;

            _previousHealth = health;
        }

        private void HandleHealthStateRestored(float health)
        {
            _previousHealth = health;
        }

        private void HandleDamageTaken(IDamage damage)
        {
            if (!IsDamageOverTime(damage))
                return;

            float now = Time.realtimeSinceStartup;
            _lastDotDamageAt = now;
            _lastCombatActivityAt = now;
        }

        private bool HasActiveDamageOverTime()
        {
            StatusEffectState statusState = ResolveStatusEffectState();
            return statusState != null &&
                   (statusState.HasStatus(StatusEffectType.Burning) ||
                    statusState.HasStatus(StatusEffectType.Poison));
        }

        private StatusEffectState ResolveStatusEffectState()
        {
            if (_statusEffectState != null)
                return _statusEffectState;

            Transform playerTransform =
                _playerView.PlayerTransform != null
                    ? _playerView.PlayerTransform
                    : _playerView.transform;

            _statusEffectState =
                playerTransform.GetComponent<StatusEffectState>() ??
                playerTransform.GetComponentInParent<StatusEffectState>() ??
                playerTransform.GetComponentInChildren<StatusEffectState>(true);

            return _statusEffectState;
        }

        private static bool IsDamageOverTime(IDamage damage)
        {
            if (damage is FireDotTickDamage || damage is FireDamage)
                return true;

            return damage is DamageInfo damageInfo &&
                   damageInfo.ApplicationKind ==
                   DamageApplicationKind.DotTick;
        }

        private static float RemainingCooldown(
            float activityAt,
            float cooldown)
        {
            if (cooldown <= 0f ||
                float.IsNegativeInfinity(activityAt))
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                activityAt + cooldown - Time.realtimeSinceStartup);
        }
    }
}
