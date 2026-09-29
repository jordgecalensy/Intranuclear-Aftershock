using UnityEngine;

public class MainMenuSettings : MonoBehaviour
{
    public GameObject graphicsMenu;
    public GameObject soundMenu;
    public GameObject gameplayMenu;

    


    public void  OnGraphicSettingsClick()
    {   
        global::Failsafe.Debugging.GameplayLog.Trace("Settings button pressed");
        graphicsMenu.SetActive(true);
        soundMenu.SetActive(false);
        gameplayMenu.SetActive(false);

    }

    public void  OnSoundSettingsClick()
    {   
        global::Failsafe.Debugging.GameplayLog.Trace("Settings button pressed");
        graphicsMenu.SetActive(false);
        soundMenu.SetActive(true);
        gameplayMenu.SetActive(false);

    }

    public void  OnGameSettingsClick()
    {   
        global::Failsafe.Debugging.GameplayLog.Trace("Settings button pressed");
        gameplayMenu.SetActive(true);
        graphicsMenu.SetActive(false);
        soundMenu.SetActive(false);
        

    }


}
