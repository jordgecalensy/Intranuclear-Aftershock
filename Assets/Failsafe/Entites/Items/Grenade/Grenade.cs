using Failsafe.Items;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using FMODUnity;

public class Grenade : IUsable
{
    public ThrowGrenadeData Data;
    protected Item Item;
    protected bool ItsMineState = false;

    protected Grenade(ThrowGrenadeData data)
    {
        Data = data;
    }
    public void ParseItem(Item item_object)
    {
        Item = item_object;
    }
    public ItemUseResult Use()
    {
        Item.gameObject.GetComponent<BaseGrеnadeObject>().ActivesionGranade(Data, ItsMineState);
        Debug.Log("Use");
        SoundUtils3D.Play(Item.gameObject, Data.ThrowGrendeSfx);
        return new ItemUseResult { ItemStateAfterUse = ItemState.Throw, UsageType = UsageType.HoldToUse };
    }
    public ItemUseResult AltMode()
    {
        ItsMineState = !ItsMineState;
        if (ItsMineState)
            SoundUtils3D.Play(Item.gameObject, Data.MineStateOnSfx);
        else
            SoundUtils3D.Play(Item.gameObject, Data.MineStateOffSfx);
        Debug.Log("ItsMineState " + ItsMineState);
        return new ItemUseResult() { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.ClickToUse };
    }
    public void GetItemUseDelays(out float startDelay, out float useDelay, out float startAltUseDelay, out float altUseDelay)
    {
        if (Item == null || Item.ItemData == null)
        {
            startDelay = 0f;
            useDelay = 0f;
            startAltUseDelay = 0f;
            altUseDelay = 0f;
            return;
        }

        startDelay = Mathf.Max(0f, Item.ItemData.StartUseDelay);
        useDelay = Mathf.Max(0f, Item.ItemData.UseDelay);
        startAltUseDelay = Mathf.Max(0f, Item.ItemData.StartAltUseDelay);
        altUseDelay = Mathf.Max(0f, Item.ItemData.UseAltDelay);
    }
}
