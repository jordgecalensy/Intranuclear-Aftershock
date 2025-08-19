using UnityEngine;

public class PlayAudioClipOnMe : MonoBehaviour
{
    [SerializeField] FMODUnity.EventReference _eventReference;

    public void PlayAtMyPosition() =>
        FMODUnity.RuntimeManager.PlayOneShot(_eventReference, transform.position);
}