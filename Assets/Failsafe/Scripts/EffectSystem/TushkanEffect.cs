using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Failsafe.Scripts.Modifiebles;
using Failsafe.PlayerMovements;

namespace Failsafe.Scripts.EffectSystem
{
    public class TushkanEffect : Effect
    {
        private PlayerMovementParameters _playerMovementParameters;
        private MultiplierFloat _jumpModificator;

        private Material _tushkanMaterial;
        private CustomPassVolume _customPassVolume;


        public TushkanEffect(float duration, PlayerMovementParameters playerMovementParameters, float JumpMultiplier) 
        {
            _playerMovementParameters = playerMovementParameters;
            _jumpModificator = new MultiplierFloat(JumpMultiplier, priority: 100);
            _tushkanMaterial = Resources.Load<Material>("StimpckEffect");
            if (_tushkanMaterial == null)
                Debug.LogWarning("PlayerController: не найден материал в Resources/");

            _duration = duration; 

            IsUniqueEffect = true; 
        }

        public override void ApplyEffect()
        {
            
            _playerMovementParameters.JumpMaxHeight.AddModificator(_jumpModificator);
            _playerMovementParameters.JumpMaxSpeed.AddModificator(_jumpModificator);

            // Создаём CustomPassVolume динамически
            _customPassVolume = new GameObject("TushkanMaterialEffect")
                .AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

            // Добавляем кастомный пасс
            var pass = new CustomPassDrawer(_tushkanMaterial);
            _customPassVolume.customPasses.Add(pass);

            Debug.Log("TushkanEffect effect applied");
        }

        public override void ClearEffect()
        {
            _playerMovementParameters.JumpMaxHeight.RemoveModificator(_jumpModificator);
            _playerMovementParameters.JumpMaxSpeed.RemoveModificator(_jumpModificator);

            if (_customPassVolume != null)
            {
                Object.Destroy(_customPassVolume.gameObject);
                Debug.Log("TushkanEffect effect cleared");
            }
        }

        public void OnReapply(Effect newEffect)
        {
            TushkanEffect reapplied = newEffect as TushkanEffect;
            Debug.Log("TushkanEffect reapplied — restarting effect");

            _duration = reapplied._duration + (Time.time - StarteAt);
            
        }
    }
}