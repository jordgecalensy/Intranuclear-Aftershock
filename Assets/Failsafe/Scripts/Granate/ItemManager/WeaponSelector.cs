using UnityEngine;

public class WeaponSelector : MonoBehaviour
{
    public WeaponManager WeaponManager;
    public GameObject Amy;
    public GameObject Fire;
    public GameObject Frag;
    private IUsableGranade _granadePrefab;
    public void ChooseGrenade(GameObject grenadePrefab)
    {
        IUsableGranade grenade = Instantiate(grenadePrefab).GetComponent<IUsableGranade>();
        WeaponManager.SetWeapon(grenade);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            ChooseGrenade(Frag);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            ChooseGrenade(Fire);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            ChooseGrenade(Amy);
    }
}

