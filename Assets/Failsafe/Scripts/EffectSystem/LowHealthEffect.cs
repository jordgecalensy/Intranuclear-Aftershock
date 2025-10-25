using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Failsafe.Scripts.EffectSystem
{
    public class LowHealthEffect : Effect
    {
        private Material _lowHpMaterial;
        private CustomPassVolume _customPassVolume;


        public LowHealthEffect()
        {
            _lowHpMaterial = Resources.Load<Material>("LowHealthEffect");
            if (_lowHpMaterial == null)
                Debug.LogWarning("PlayerController: не найден материал LowHealthEffect в Resources/");

            _duration = Mathf.Infinity;
            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            if (_lowHpMaterial == null)
            {
                Debug.LogError("LowHealthEffect: материал не задан!");
                return;
            }

            // Создаём CustomPassVolume динамически
            _customPassVolume = new GameObject("LowHealthEffectVolume")
                .AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

            // Добавляем кастомный пасс
            var pass = new CustomPassDrawer { EffectMaterial = _lowHpMaterial };
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