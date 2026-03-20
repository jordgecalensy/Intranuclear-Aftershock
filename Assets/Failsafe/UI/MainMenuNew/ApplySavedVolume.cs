using UnityEngine;
using FMOD.Studio;
using FMODUnity;

public class ApplySavedFMODMasterVolume : MonoBehaviour
{
    private const string VolumeKey = "FMOD_MasterVolume";
    private Bus masterBus;

    private void Awake()
    {
        masterBus = RuntimeManager.GetBus("bus:/");

        float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        masterBus.setVolume(savedVolume);
    }
}