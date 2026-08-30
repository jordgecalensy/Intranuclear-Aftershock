using System.Collections.Generic;
using Failsafe.PlayerMovements;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class StunEffect : Effect, IReapplicableEffect, IRegisteredStatusEffect
    {
        private const int PlayerStunLockId = 1007;

        private readonly StatusEffectState _state;
        private readonly Enemy _enemy;
        private readonly PlayerControlBlocker _playerControlBlocker;
        private readonly GameObject _source;

        private readonly bool _disableEnemyState;
        private readonly bool _blockPlayerControls;
        private readonly PlayerControlBlock _playerBlocks;

        private readonly IReadOnlyList<StatusEffectType> _removeStatusesOnApply;
        private readonly IReadOnlyList<StatusEffectType> _immunityStatusesOnEnd;
        private readonly float _immunityDurationOnEnd;

        private bool _cleared;

        public StatusEffectType StatusType => StatusEffectType.Stun;

        public StunEffect(
            StatusEffectState state,
            Enemy enemy,
            PlayerControlBlocker playerControlBlocker,
            float duration,
            GameObject source,
            bool disableEnemyState,
            bool blockPlayerControls,
            PlayerControlBlock playerBlocks,
            IReadOnlyList<StatusEffectType> removeStatusesOnApply,
            IReadOnlyList<StatusEffectType> immunityStatusesOnEnd,
            float immunityDurationOnEnd)
        {
            _state = state;
            _enemy = enemy;
            _playerControlBlocker = playerControlBlocker;
            _source = source;

            _duration = Mathf.Max(0f, duration);

            _disableEnemyState = disableEnemyState;
            _blockPlayerControls = blockPlayerControls;
            _playerBlocks = playerBlocks;

            _removeStatusesOnApply = removeStatusesOnApply;
            _immunityStatusesOnEnd = immunityStatusesOnEnd;
            _immunityDurationOnEnd = Mathf.Max(0f, immunityDurationOnEnd);

            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            if (_state == null)
                return;

            if (!_state.CanReceive(StatusEffectType.Stun))
            {
                EffectLog.Info(EffectLog.Status, $"[StunEffect] {_state.name}: blocked by immunity Stun", _state);
                _duration = 0f;
                return;
            }

            _state.RemoveStatuses(_removeStatusesOnApply);
            _state.RegisterStatus(StatusEffectType.Stun, this);

            ApplyEnemyStun();
            ApplyPlayerStun();

            EffectLog.Info(EffectLog.Status, $"[StunEffect] {_state.name}: apply Stun for {_duration:0.00}s", _state);
        }

        public override void ClearEffect()
        {
            if (_cleared)
                return;

            _cleared = true;

            ClearPlayerStun();

            if (_state != null)
            {
                _state.UnregisterStatus(StatusEffectType.Stun, this);
                _state.AddTemporaryImmunity(_immunityStatusesOnEnd, _immunityDurationOnEnd);

                EffectLog.Info(EffectLog.Status, $"[StunEffect] {_state.name}: clear Stun", _state);
            }
        }

        public void ForceClearFromStatusState()
        {
            ClearEffect();
            _duration = 0f;
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not StunEffect reapplied)
                return;

            _duration = (Time.time - StarteAt) + reapplied._duration;

            if (_disableEnemyState && _enemy != null)
                _enemy.DisableState(reapplied._duration);

            if (_blockPlayerControls && _playerControlBlocker != null)
            {
                _playerControlBlocker.AddLock(
                    PlayerStunLockId,
                    _playerBlocks);
            }

            EffectLog.Info(EffectLog.Status, $"[StunEffect] {_state.name}: refresh Stun for {reapplied._duration:0.00}s", _state);
        }

        private void ApplyEnemyStun()
        {
            if (!_disableEnemyState)
                return;

            if (_enemy == null)
                return;

            _enemy.DisableState(_duration);
        }

        private void ApplyPlayerStun()
        {
            if (!_blockPlayerControls)
                return;

            if (_playerControlBlocker == null)
                return;

            _playerControlBlocker.AddLock(
                PlayerStunLockId,
                _playerBlocks);
        }

        private void ClearPlayerStun()
        {
            if (_playerControlBlocker == null)
                return;

            _playerControlBlocker.RemoveLock(PlayerStunLockId);
        }
    }
}