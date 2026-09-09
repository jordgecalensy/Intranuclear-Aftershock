using Failsafe.Items;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;

public class Wrench : IUsable
{
    [Inject] Camera _playerCam;

    private Item _item;
    private WrenchData _data;
    private IEffectApplicationService _effects;

    public Wrench(WrenchData data, IEffectApplicationService effects)
    {
        _data = data;
        _effects = effects;
    }
    public ItemUseResult Use()
    {
        TryDamageDeal(Raycast(), true);
        return new ItemUseResult() { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.ClickToUse };
    }
    public ItemUseResult AltMode()
    {
        TryDamageDeal(Raycast(), false);
        return new ItemUseResult() { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.ClickToUse };
    }
    private void TryDamageDeal(RaycastHit hit, bool ItsDefaultUse)
    {
        if (hit.collider == null) return;
        if (_effects == null)
        {
            Debug.LogError("[Wrench] IEffectApplicationService is null.");
            return;
        }

        EffectBundle bundle;
        if (ItsDefaultUse)
            bundle = _item.ItemData.DefaultModeEffects;
        else
            bundle = _item.ItemData.AlternativeModeEffects;

        if (bundle == null)
        {
            Debug.LogWarning("[Wrench] EffectBundle is not assigned.", _item);
            return;
        }

        Vector3 direction = hit.point - _playerCam.transform.position;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = _playerCam.transform.forward;

        var context = new EffectContext(
                _item.gameObject,
                hit.collider,
                hit.point,
                hit.normal,
                direction,
                1f);
        _effects.Apply(bundle, context);
    }
    private RaycastHit Raycast()
    {
        Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);

        LayerMask mask = _item.ItemData.UseMask;
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, _item.ItemData.UseRange, mask))
        {
            Debug.Log("Object ahead: " + hit.collider.name);
            return hit;
        }
        Debug.Log("No Object!");
        return hit;
    }
    public void ParseItem(Item item_object)
    {
        _item = item_object;
    }
    public void GetItemUseDelays(out float startDelay, out float useDelay, out float startAltUseDelay, out float altUseDelay)
    {
        if (_item == null || _item.ItemData == null)
        {
            startDelay = 0f;
            useDelay = 0f;
            startAltUseDelay = 0f;
            altUseDelay = 0f;
            return;
        }

        startDelay = Mathf.Max(0f, _item.ItemData.StartUseDelay);
        useDelay = Mathf.Max(0f, _item.ItemData.UseDelay);
        startAltUseDelay = Mathf.Max(0f, _item.ItemData.StartAltUseDelay);
        altUseDelay = Mathf.Max(0f, _item.ItemData.UseAltDelay);
    }
}
