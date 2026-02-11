using Failsafe.Items;
using UnityEngine;

public class NewTestGranade : IUsable
{
    private GranadeData _data;
    private Item _granadeItem;
    NewTestGranade(GranadeData data)
    {
        _data = data;
    }
    public void ParseItem(Item item_object)
    {
        _granadeItem = item_object;
    }
    public ItemUseResult Use()
    {
        _granadeItem.gameObject.GetComponent<GranadeObject>().ActivesionGranade(_data);
        Debug.Log("Use");
        return new ItemUseResult { ItemStateAfterUse = ItemState.Drop, UsageType = UsageType.HoldToUse };
    }
    public void AltMode()
    {

    }
    public void GetItemUseDelays(out float startUseDelay, out float useDelay)
    {
        startUseDelay = _data.StartUseDelay;
        useDelay = _data.UseDelay;
    }
}
