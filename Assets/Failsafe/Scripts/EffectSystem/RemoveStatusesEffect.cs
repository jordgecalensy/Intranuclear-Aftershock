using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class RemoveStatusesEffect : Effect
    {
        private readonly StatusEffectState _state;
        private readonly StatusEffectType[] _statusesToRemove;

        public RemoveStatusesEffect(
            StatusEffectState state,
            StatusEffectType[] statusesToRemove)
        {
            _state = state;
            _statusesToRemove = statusesToRemove;

            _duration = 0f;
            IsUniqueEffect = false;
        }

        public override void ApplyEffect()
        {
            if (_state == null)
                return;

            if (_statusesToRemove == null)
                return;

            foreach (StatusEffectType status in _statusesToRemove)
            {
                if (status == StatusEffectType.None)
                    continue;

                if (!_state.HasStatus(status))
                    continue;

                Debug.Log($"[RemoveStatusesEffect] {_state.name}: remove {status}", _state);
                _state.RemoveStatus(status);
            }
        }

        public override void ClearEffect()
        {
        }
    }
}