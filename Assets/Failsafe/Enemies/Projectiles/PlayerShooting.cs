using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    public WeaponController weaponController;
    public Camera fpsCamera;

    void Update()
    {
        // R - перезарядка
        if (Input.GetKeyDown(KeyCode.R)) weaponController.StartReload();

        // ЛКМ - Огонь
        if (Input.GetMouseButton(0))
        {
            Ray ray = fpsCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Vector3 target;

            // Если попали в стену/врага - стреляем туда, иначе в бесконечность
            if (Physics.Raycast(ray, out RaycastHit hit, 999f))
                target = hit.point;
            else
                target = ray.GetPoint(100f);

            weaponController.TryShoot(target);
        }

        // Отпустили кнопку - стоп (важно для лазера)
        if (Input.GetMouseButtonUp(0))
        {
            weaponController.StopShooting();
        }
    }
}