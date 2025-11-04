using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Failsafe.Scripts.Modifiebles;
using Failsafe.Player.Model;

namespace Failsafe.Scripts.EffectSystem
{
    public class GorillaEffect : Effect
    {
        private PlayerModelParameters _playerModelParameters;
        private IModificator<float> _throwPowerModificator;
        private Material _gorillaMaterial;
        private CustomPassVolume _customPassVolume;


        public GorillaEffect(float duration, PlayerModelParameters playerModelParameters, float ThrowPowerMultiplier) //время действия
        {
            _playerModelParameters = playerModelParameters;
            _throwPowerModificator = new MultiplierFloat(ThrowPowerMultiplier, priority: 100);
            _gorillaMaterial = Resources.Load<Material>("StimpckEffect");
            if (_gorillaMaterial == null)
                Debug.LogWarning("PlayerController: не найден материал в Resources/");

            _duration = duration; // ← теперь ограничено временем

            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            
            _playerModelParameters.ThrowPower.AddModificator(_throwPowerModificator);
            _playerModelParameters.ThrowTorquePower.AddModificator(_throwPowerModificator);

            // Создаём CustomPassVolume динамически
            _customPassVolume = new GameObject("GorillaMaterialEffect")
                .AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

            // Добавляем кастомный пасс
            var pass = new CustomPassDrawer(_gorillaMaterial);
            _customPassVolume.customPasses.Add(pass);

            Debug.Log("GorillaEffect effect applied");
        }

        public override void ClearEffect()
        {
            _playerModelParameters.ThrowPower.RemoveModificator(_throwPowerModificator);
            _playerModelParameters.ThrowTorquePower.RemoveModificator(_throwPowerModificator);

            if (_customPassVolume != null)
            {
                Object.Destroy(_customPassVolume.gameObject);
                Debug.Log("GorillaEffect cleared");
            }
        }
        
        public void OnReapply(Effect newEffect)
        {
            GorillaEffect reapplied = newEffect as GorillaEffect;
            Debug.Log("GorillaEffect reapplied — restarting effect");

            _duration = reapplied._duration + (Time.time - StarteAt);
            
        }
    }
}