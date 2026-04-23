using Failsafe.Items;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using FMODUnity;

public class Grenade : IUsable
{
    public ThrowGrenadeData Data;
    protected Item GranadeItem;
    protected bool ItsMineState = false;

    protected Grenade(ThrowGrenadeData data)
    {
        Data = data;
    }
    public void ParseItem(Item item_object)
    {
        GranadeItem = item_object;
    }
    public ItemUseResult Use()
    {
        GranadeItem.gameObject.GetComponent<BaseGrеnadeObject>().ActivesionGranade(Data, ItsMineState);
        Debug.Log("Use");
        SoundUtils3D.Play(GranadeItem.gameObject, Data.ThrowGrendeSfx);
        return new ItemUseResult { ItemStateAfterUse = ItemState.Drop, UsageType = UsageType.HoldToUse };
    }
    public void AltMode()
    {
        ItsMineState = !ItsMineState;
        SoundUtils3D.Play(GranadeItem.gameObject, Data.SwitchGrendeSfx);
        Debug.Log("ItsMineState " + ItsMineState);
    }
    public void GetItemUseDelays(out float startUseDelay, out float useDelay)
    {
        startUseDelay = Data.StartUseDelay;
        useDelay = Data.UseDelay;
    }
}