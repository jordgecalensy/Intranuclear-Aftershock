using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Failsafe.Scripts.Modifiebles;
using Failsafe.PlayerMovements;

namespace Failsafe.Scripts.EffectSystem
{
    public class AdrenalineEffect : Effect, IReapplicableEffect
    {
        private Material _adrenalineMaterial;
        private CustomPassVolume _customPassVolume;
        private PlayerMovementParameters _playerMovementParameters;
        private MultiplierFloat _speedModificator;
        private float _speedMultiplier;


        public AdrenalineEffect(float duration, PlayerMovementParameters playerMovementParameters, float SpeedMultiplier) //время действия
        {
            _playerMovementParameters = playerMovementParameters;
            _speedModificator = new MultiplierFloat(SpeedMultiplier, priority: 100);
            _adrenalineMaterial = Resources.Load<Material>("BoostMaterial");
            if (_adrenalineMaterial == null)
                Debug.LogWarning("PlayerController: не найден материал BoostMaterial в Resources/");

            _duration = duration;
            IsUniqueEffect = true;
            _speedMultiplier = SpeedMultiplier;
        }

        public override void ApplyEffect()
        {
            
            _playerMovementParameters.WalkSpeed.AddModificator(_speedModificator);
            _playerMovementParameters.RunSpeed.AddModificator(_speedModificator);
            _playerMovementParameters.CrouchSpeed.AddModificator(_speedModificator);

            // Создаём CustomPassVolume динамически
            _customPassVolume = new GameObject("AdrenalineMaterialEffect")
                .AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess; 

            // Добавляем кастомный пасс
            var pass = new CustomPassDrawer(_adrenalineMaterial);
            _customPassVolume.customPasses.Add(pass);

            Debug.Log("Low Health HDRP effect applied");
        }

        public override void ClearEffect()
        {
            _playerMovementParameters.WalkSpeed.RemoveModificator(_speedModificator);
            _playerMovementParameters.RunSpeed.RemoveModificator(_speedModificator);
            _playerMovementParameters.CrouchSpeed.RemoveModificator(_speedModificator);

            if (_customPassVolume != null)
            {
                Object.Destroy(_customPassVolume.gameObject);
                Debug.Log("Low Health HDRP effect cleared");
            }
        }
        public void OnReapply(Effect newEffect)
        {
            AdrenalineEffect reapplied = newEffect as AdrenalineEffect;
            Debug.Log("AdrenalineEffect reapplied — restarting effect");

            _duration = reapplied._duration + (Time.time - StarteAt);
            
        }
    }
}