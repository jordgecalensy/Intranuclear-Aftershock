using System;
using UnityEngine;

public class NewGameButton : MonoBehaviour
{
   

    public void OnOpenSelectedWindow(GameObject window) 
    {
        window.SetActive(true);
    }
}
