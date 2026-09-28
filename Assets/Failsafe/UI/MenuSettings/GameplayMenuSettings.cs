using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplayMenuSettings : MonoBehaviour
{
    public Slider sensetivityLevel;
    public Slider cameraShakeLevel;
    public GameObject saveButton;
    public GameObject defaultButton;

    public void OnSensetivityLevelChange()
    {

        global::Failsafe.Debugging.GameplayLog.Trace(sensetivityLevel.value);



    }

    public void OnCamerShakeChange()
    {

        global::Failsafe.Debugging.GameplayLog.Trace(cameraShakeLevel.value);



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
