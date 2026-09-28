using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class SoundMenuSettings : MonoBehaviour
{
    public Slider masterVolume;
    public Slider musicVolume;
    public Slider sfxVolume;
    public GameObject saveButton;
    public GameObject defaultButton;


    public void OnMasterVolumeChange()
    {

        global::Failsafe.Debugging.GameplayLog.Trace(masterVolume.value);



    }

    public void OnMusicVolumeChange()
    {

        global::Failsafe.Debugging.GameplayLog.Trace(musicVolume.value);



    }
    public void OnSFXVolumeChange()
    {

        global::Failsafe.Debugging.GameplayLog.Trace(sfxVolume.value);



    }
    public void  OnSaveButtonClick()
    {   
        global::Failsafe.Debugging.GameplayLog.Trace("Settings button pressed");
        

    }

    public void  OnDefaultButtonClick()
    {   
        global::Failsafe.Debugging.GameplayLog.Trace("Settings button pressed");


    }




}
