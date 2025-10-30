using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Failsafe.Scripts.EffectSystem
{
    public class StimpackEffect : Effect
    {
        private Material _stimpackMaterial;
        private CustomPassVolume _customPassVolume;


        public StimpackEffect(float duration) //время действия
        {
            _stimpackMaterial = Resources.Load<Material>("StimpckEffect");
            if (_stimpackMaterial == null)
                Debug.LogWarning("PlayerController: не найден материал LowHealthEffect в Resources/");

            _duration = duration; // ← теперь ограничено временем
            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            if (_stimpackMaterial == null)
            {
                Debug.LogError("LowHealthEffect: материал не задан!");
                return;
            }

            // Создаём CustomPassVolume динамически
            _customPassVolume = new GameObject("StimpckMaterialEffect")
                .AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess; 

            // Добавляем кастомный пасс
            var pass = new CustomPassDrawer(_stimpackMaterial);
            _customPassVolume.customPasses.Add(pass); 

            Debug.Log("Low Health HDRP effect applied");
        }

        public override void ClearEffect()
        {
            if (_customPassVolume != null)
            {
                Object.Destroy(_customPassVolume.gameObject);
                Debug.Log("Low Health HDRP effect cleared");
            }
        }
    }
} 