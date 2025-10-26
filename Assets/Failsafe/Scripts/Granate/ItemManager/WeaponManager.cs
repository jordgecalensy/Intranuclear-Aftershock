using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    private IUsableGranade _currentGranade;

    public void SetWeapon(IUsableGranade granade)
    {
        _currentGranade = granade;
    }
    private void UseCurrentGranade(bool ItsAltUse)
    {
        if (_currentGranade != null)
        {
            if (!ItsAltUse)
                _currentGranade.Use();
            else
                _currentGranade.AltUse();
        }
        else
            Debug.Log("Оружие не выбрано");
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            UseCurrentGranade(false);

        else if (Input.GetMouseButtonDown(1))
            UseCurrentGranade(true);
    }
}
