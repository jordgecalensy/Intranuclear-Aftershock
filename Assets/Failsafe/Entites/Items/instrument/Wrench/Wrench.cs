using Failsafe.Items;
using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;
using VContainer;

public class Wrench : IUsable
{
    private Item _item;
    private WrenchData _data;

    [Inject] Camera _playerCam;
    public Wrench(WrenchData data)
    {
        _data = data;
    }
    public ItemUseResult Use()
    {
        Banch(Raycast());
        return new ItemUseResult() { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.HoldToUse };
    }
    private void Banch(RaycastHit hit)
    {
        DamageableComponent damageableComponent = hit.collider.GetComponentInParent<DamageableComponent>();
        if (damageableComponent == null) return;
        damageableComponent.TakeDamage(new FlatDamage(_data.Damage));
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
