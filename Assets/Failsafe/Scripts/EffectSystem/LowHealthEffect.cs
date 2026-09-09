using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using FMODUnity;

namespace Failsafe.Scripts.EffectSystem
{
    public class LowHealthEffect : Effect
    {
        private Material _lowHpMaterial;
        private CustomPassVolume _customPassVolume;
        private StudioEventEmitter _lowHealthEmitter;

        private EventReference _lowHealthEvent;



        public LowHealthEffect()
        {
            _lowHpMaterial = Resources.Load<Material>("LowHealthEffect");
            if (_lowHpMaterial == null)
                EffectLog.Warning(EffectLog.Feedback, "PlayerController: не найден материал LowHealthEffect в Resources/");

            _duration = Mathf.Infinity;
            IsUniqueEffect = true;

            _lowHealthEvent = CreateEventReference("{1eb3bc7b-b80b-4f3f-bae0-97127f5d21f1}");
        }

        private FMODUnity.EventReference CreateEventReference(string guidString)
        {
            var guid = FMODUnity.RuntimeManager.PathToGUID(guidString);
            return new FMODUnity.EventReference { Guid = guid };
        }

        public override void ApplyEffect()
        {
            // Создаём CustomPassVolume динамически
            _customPassVolume = new GameObject("LowHealthEffect")
                .AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

            // Добавляем кастомный пасс
            var pass = new CustomPassDrawer(_lowHpMaterial);
            _customPassVolume.customPasses.Add(pass);

            // Создаём объект для фонового звука
            _lowHealthEmitter = _customPassVolume.gameObject.AddComponent<StudioEventEmitter>();
            _lowHealthEmitter.EventReference = _lowHealthEvent;
            _lowHealthEmitter.Play();

            EffectLog.Info(EffectLog.Feedback, "Low Health HDRP effect applied");
        }

        public override void ClearEffect()
        {
            if (_lowHealthEmitter != null)
            {
                _lowHealthEmitter.Stop();
            }
            if (_customPassVolume != null)
            {
                Object.Destroy(_customPassVolume.gameObject);
                EffectLog.Info(EffectLog.Feedback, "Low Health HDRP effect cleared");
            }
        }
    }
}