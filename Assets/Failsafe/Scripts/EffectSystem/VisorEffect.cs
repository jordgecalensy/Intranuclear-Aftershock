using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using FMODUnity;

namespace Failsafe.Scripts.EffectSystem
{
    public class VisorEffect : Effect
    {
        private Material _visorMaterial;
        private ObjectXRay[] _xrayObjects;
        private Transform _player;
        private float _xrayRadius = 10f;
        private GameObject _visorEffectObject;
        private StudioEventEmitter _visorLoopEmitter;
        private StudioEventEmitter _visorOnEmitter;
        private StudioEventEmitter _visorOffEmitter;
        private CustomPassVolume _customPassVolume;

        // FMOD события
        private EventReference _visorOnEvent;
        private EventReference _visorOffEvent;
        private EventReference _visorLoopEvent;

        public VisorEffect(Transform PlayerTransform)
        {
            _visorMaterial = Resources.Load<Material>("VisorShaderMaterial"); // можно заменить на свой материал визора
            if (_visorMaterial == null)
                Debug.LogWarning("VisorEffect: не найден материал LowHealthEffect в Resources/");

            _duration = Mathf.Infinity;
            IsUniqueEffect = true;
            _player = PlayerTransform;

            // FMOD события можно потом внедрить через DI или ScriptableObject
            _visorOnEvent = EventReference.Find("event:/UI/VISOR/visorON");
            _visorOffEvent = EventReference.Find("event:/UI/VISOR/visorOFF");
            _visorLoopEvent = EventReference.Find("event:/UI/VISOR/visorLoop");
        }

        public override void ApplyEffect()
        {

            _xrayObjects = Object.FindObjectsOfType<ObjectXRay>();
            Debug.Log($"VisorEffect: найдено {_xrayObjects.Length} XRay-объектов.");

            // Создаём Custom Pass Volume
            _visorEffectObject = new GameObject("VisorEffect");
            _customPassVolume = _visorEffectObject.AddComponent<CustomPassVolume>();
            _customPassVolume.isGlobal = true;
            _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
            // Добавляем кастомный пасс
            var pass = new CustomPassDrawer(_visorMaterial);
            _customPassVolume.customPasses.Add(pass);

            // Создаём объект для фонового звука
            _visorLoopEmitter = _visorEffectObject.AddComponent<StudioEventEmitter>();
            _visorLoopEmitter.EventReference = _visorLoopEvent;

            _visorOnEmitter = _visorEffectObject.AddComponent<StudioEventEmitter>();
            _visorOnEmitter.EventReference = _visorOnEvent;

            _visorOffEmitter = _visorEffectObject.AddComponent<StudioEventEmitter>();
            _visorOffEmitter.EventReference = _visorOffEvent;

            _visorOffEmitter.Stop();
            _visorOnEmitter.Play();
            _visorLoopEmitter.Play();

            Debug.Log("VisorEffect активирован");
        }

        public override void Update()
        {
            if (_player == null || _xrayObjects == null) return;

            foreach (var obj in _xrayObjects)
            {
                if (obj == null) continue;

                float distance = Vector3.Distance(_player.position, obj.transform.position);
                obj.SetXRay(distance <= _xrayRadius);
            }
        }

        public override void ClearEffect()
        {
            if (_visorLoopEmitter != null)
            {
                _visorLoopEmitter.Stop();
                _visorOnEmitter.Stop();
                _visorOffEmitter.Play();
            }

            // выключаем все подсветки
            
            if (_xrayObjects != null)
            {
                foreach (var obj in _xrayObjects)
                {
                    if (obj != null)
                        obj.SetXRay(false);
                }
            }

            // убираем CustomPass
            if (_customPassVolume != null)
                Object.Destroy(_customPassVolume.gameObject);

            Debug.Log("VisorEffect деактивирован");
        }
    }
}
