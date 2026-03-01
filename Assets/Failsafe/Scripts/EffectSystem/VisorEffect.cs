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
        private float _xrayRadius = 30f;
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
            _visorMaterial = Resources.Load<Material>("VisorShaderMaterial");
            if (_visorMaterial == null)
                Debug.LogWarning("VisorEffect: не найден материал VisorShaderMaterial в Resources/");

            _duration = Mathf.Infinity;
            IsUniqueEffect = true;
            _player = PlayerTransform;

            _visorOnEvent   = CreateEventReference("{516d9d6b-bc84-416d-8333-ef18e1034b80}");
            _visorOffEvent  = CreateEventReference("{91e9e965-7a03-400a-b440-405b8c60aedb}");
            _visorLoopEvent = CreateEventReference("{018ffa1e-51d6-46e2-ae6f-fee9983f2ef2}");
        }

        private FMODUnity.EventReference CreateEventReference(string guidString)
        {
            var guid = FMODUnity.RuntimeManager.PathToGUID(guidString);
            return new FMODUnity.EventReference { Guid = guid };
        }
        public override void ApplyEffect()
        {

            _xrayObjects = Object.FindObjectsOfType<ObjectXRay>();
            Debug.Log($"VisorEffect: найдено {_xrayObjects.Length} XRay-объектов.");

            var prefab = Resources.Load<GameObject>("ScannerVisorVFX");

            // Создаём Custom Pass Volume
            _visorEffectObject = new GameObject("VisorEffect");
            _visorEffectObject.transform.SetParent(_player, false);

                // Вешаем VFX префаб
            if (prefab != null)
                Object.Instantiate(prefab, _visorEffectObject.transform);
            else
                Debug.LogError("VisorEffect: ScannerVFX prefab not found!");

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
