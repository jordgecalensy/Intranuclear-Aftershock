using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.Health;
using Failsafe.Scripts.Modifiebles;

namespace Failsafe.Scripts.EffectSystem
{
    public class StimpackEffect : Effect
    {
        private Material _stimpackMaterial;
        private CustomPassVolume _customPassVolume;
        private PlayerHealth _playerHealth;
        private AdderFloat _maxHealthModificator;
        private int _healAmount;


        public StimpackEffect(float duration, PlayerHealth playerHealth, AdderFloat maxHealthModificator, int HealAmount) 
        {            
            _stimpackMaterial = Resources.Load<Material>("StimpckEffect");
            if (_stimpackMaterial == null)
                Debug.LogWarning("PlayerController: не найден материал LowHealthEffect в Resources/");

            _duration = duration; 
            _playerHealth = playerHealth;
            _maxHealthModificator = maxHealthModificator;
            _healAmount = HealAmount;
            
            IsUniqueEffect = false;
        }

        public override void ApplyEffect()
        {
            if (_stimpackMaterial == null)
            {
                Debug.LogError("материал не задан!");
                return;
            }
    
            _playerHealth.ModifyMaxHealth(_maxHealthModificator);
            _playerHealth.AddHealth(_healAmount);

            // Создаём CustomPassVolume динамически
            _customPassVolume = new GameObject("StimpckMaterialEffect")
                .AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess; 

            // Добавляем кастомный пасс
            var pass = new CustomPassDrawer(_stimpackMaterial);
            _customPassVolume.customPasses.Add(pass); 

            Debug.Log("StimpckEffect applied");
        }

        public override void ClearEffect()
        {
            if (_customPassVolume != null)
            {
                Object.Destroy(_customPassVolume.gameObject);
                Debug.Log("StimpckEffect effect cleared");
            }
        }
    }
} 