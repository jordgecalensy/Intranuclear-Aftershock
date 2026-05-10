using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public class MineTrigger : MonoBehaviour
{
    [SerializeField] private BaseGrеnadeObject _granade;
    private EventInstance _eventInstance;

    private bool _itsTriggerActivated = false;
    private void OnTriggerStay(Collider other)
    {
        if (_itsTriggerActivated) return;
        if (other.gameObject.tag == "Player" || other.gameObject.tag == "Enemy")
        {
            Vector3 directionToEnemy = (other.transform.position - transform.position).normalized;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToEnemy, out hit))
            {
                _itsTriggerActivated = true;
                Debug.Log("trig " + other.name);
                _eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _granade.Explosion();
            }
        }
    }
    public void ActivateMineIndicationSfx(EventReference sfx)
    {
        _eventInstance = RuntimeManager.CreateInstance(sfx);
        _eventInstance.start();
    }
    private void OnDestroy()
    {
        _eventInstance.release();
    }
}
