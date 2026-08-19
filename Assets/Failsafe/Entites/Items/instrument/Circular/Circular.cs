using Failsafe.Items;
using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class Circular : IUsable
{
    private Item _item;
    private CircularData _data;

    [Inject] Camera _playerCam;
    public Circular(CircularData data)
    {
        _data = data;
    }
    public ItemUseResult Use()
    {
        //Wrrr(Raycast());
        Debug.Log("Wrrr");
        return new ItemUseResult() { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.HoldToUse };
    }
    private void Wrrr(RaycastHit hit)
    {
        //DamageableComponent damageableComponent = hit.collider.GetComponentInParent<DamageableComponent>();
        if (hit.collider.GetComponentInParent<DamageableComponent>() == null) return;
        hit.collider.GetComponentInParent<DamageableComponent>().TakeDamage(new FlatDamage(_data.Damage));
        Debug.Log($"{hit.collider.name} Take {_data.Damage} Damage");
    }
    private RaycastHit Raycast()
    {
        Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);
        //маска чтобы рейкаст точно игнорировал игрока
        LayerMask mask = ~(1 << 5);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, _data.MaxDistance, mask))
        {
            Debug.Log("Object ahead: " + hit.collider.name);
        }
        else
        {
            Debug.Log("No Object!");
        }
        return hit;
    }
    public void AltMode()
    {

    }
    public void ParseItem(Item item_object)
    {
        _item = item_object;
    }
    public void GetItemUseDelays(out float startDelay, out float useDelay)
    {
        startDelay = _data.StartUseDelay;
        useDelay = _data.UseDelay;
    }
}
