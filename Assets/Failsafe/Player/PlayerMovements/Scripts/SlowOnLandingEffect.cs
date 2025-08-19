using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.Modifiebles;
using UnityEngine;

namespace Failsafe.PlayerMovements
{
    /// <summary>
    /// Замедление перемещения при приземлении с большой высоты
    /// </summary>
    public class SlowOnLandingEffect : Effect
    {
        private PlayerMovementParameters _playerMovementParameters;
        private IModificator<float> _slowModificator;
        public SlowOnLandingEffect(PlayerMovementParameters playerMovementParameters)
        {
            _playerMovementParameters = playerMovementParameters;
            _duration = _playerMovementParameters.SlowDurationOnLanding;
            _slowModificator = new MultiplierFloat(_playerMovementParameters.SlowMultiplierOnLanding, priority: 100);
            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            _playerMovementParameters.WalkSpeed.AddModificator(_slowModificator);
            _playerMovementParameters.RunSpeed.AddModificator(_slowModificator);
            _playerMovementParameters.CrouchSpeed.AddModificator(_slowModificator);
            Debug.Log("ApplyEffect " + nameof(SlowOnLandingEffect));
        }

        public override void ClearEffect()
        {
            _playerMovementParameters.WalkSpeed.RemoveModificator(_slowModificator);
            _playerMovementParameters.RunSpeed.RemoveModificator(_slowModificator);
            _playerMovementParameters.CrouchSpeed.RemoveModificator(_slowModificator);
            Debug.Log("ClearEffect " + nameof(SlowOnLandingEffect));
        }
    }
}
