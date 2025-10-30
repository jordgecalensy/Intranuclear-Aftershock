using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using FMODUnity;

public class VisorController : MonoBehaviour
{
    [Header("Visor Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.H;
    [SerializeField] private CustomPassVolume customPassVolume;
    [SerializeField] private Transform player;
    [SerializeField] private float xrayRadius = 10f;

    [Header("FMOD Events")]
    [SerializeField] private EventReference visorOnEvent;   // звук включения
    [SerializeField] private EventReference visorOffEvent;  // звук выключения
    [SerializeField] private EventReference visorLoopEvent; // звук работы визора (зацикленный)

    private ObjectXRay[] xrayObjects;
    private bool visorActive = false;

    private GameObject loopEmitterGO;

    void Start()
    {
        xrayObjects = FindObjectsOfType<ObjectXRay>();

        if (customPassVolume != null)
            customPassVolume.enabled = false;

        // Создаем GO для фонового звука визора
        loopEmitterGO = new GameObject("VisorLoopEmitter");
        loopEmitterGO.transform.SetParent(transform);
        loopEmitterGO.transform.localPosition = Vector3.zero;

        var emitter = loopEmitterGO.AddComponent<FMODUnity.StudioEventEmitter>();
        emitter.EventReference = visorLoopEvent;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            visorActive = !visorActive;

            // переключаем кастомный пасс
            if (customPassVolume != null)
                customPassVolume.enabled = visorActive;

            // звук включения / выключения
            if (visorActive)
            {
                SoundUtils3D.Play(gameObject, visorOnEvent);   // одноразовый звук
                SoundUtils3D.Play(loopEmitterGO, visorLoopEvent); // запускаем фон
            }
            else
            {
                SoundUtils3D.Play(gameObject, visorOffEvent);  // звук отключения
                SoundUtils3D.Stop(loopEmitterGO);              // стоп фонового звука
            }

            // переключаем XRay у объектов
            foreach (var obj in xrayObjects)
            {
                if (visorActive)
                {
                    float distance = Vector3.Distance(player.position, obj.transform.position);
                    obj.SetXRay(distance <= xrayRadius);
                }
                else
                {
                    obj.SetXRay(false);
                }
            }
        }

        // обновляем радиус динамически
        if (visorActive)
        {
            foreach (var obj in xrayObjects)
            {
                float distance = Vector3.Distance(player.position, obj.transform.position);
                obj.SetXRay(distance <= xrayRadius);
            }
        }
    }
}
