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
        global::Failsafe.Debugging.GameplayLog.Trace("Use");
        SoundUtils3D.Play(GranadeItem.gameObject, Data.ThrowGrendeSfx);
        return new ItemUseResult { ItemStateAfterUse = ItemState.Throw, UsageType = UsageType.HoldToUse };
    }
    public void AltMode()
    {
        ItsMineState = !ItsMineState;
        if (ItsMineState)
            SoundUtils3D.Play(GranadeItem.gameObject, Data.MineStateOnSfx);
        else
            SoundUtils3D.Play(GranadeItem.gameObject, Data.MineStateOffSfx);
        global::Failsafe.Debugging.GameplayLog.Trace("ItsMineState " + ItsMineState);
    }
    public void GetItemUseDelays(out float startUseDelay, out float useDelay)
    {
        startUseDelay = Data.StartUseDelay;
        useDelay = Data.UseDelay;
    }
}
