using UnityEngine;

public class WeaponSelector : MonoBehaviour
{
    public GrandeManager GranadeManager;

    private IUsableGranade _grenadePrefab;

    public void ChooseGrenade()
    {
        GranadeManager.SetWeapon(_grenadePrefab);
    }
}

