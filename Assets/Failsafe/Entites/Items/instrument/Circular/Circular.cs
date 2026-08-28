using Failsafe.Items;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class Circular : IUsable, ITickable
{
    private Item _item;
    private CircularData _data;
    private float _fireRateTimer = 0;
    private IEffectApplicationService _effect;

    [Inject] Camera _playerCam;
    public Circular(CircularData data, IEffectApplicationService effects)
    {
        _data = data;
        _effect = effects;
    }
    public void Tick()
    {
        if (_fireRateTimer < 0)
            _fireRateTimer = 0;
        if (_fireRateTimer > _data.CircularStages[_data.CircularStages.Count - 1].Duration)
            _fireRateTimer = _data.CircularStages[_data.CircularStages.Count - 1].Duration;

        if (Input.GetMouseButton(0) && _fireRateTimer < _data.CircularStages[_data.CircularStages.Count - 1].Duration)
        {
            _fireRateTimer += Time.deltaTime;
        }
        else if (!Input.GetMouseButton(0) && _fireRateTimer > 0)
        {
            _fireRateTimer -= Time.deltaTime;
        }
        Debug.Log("[Circular]  _fireRateTimer " + (int)_fireRateTimer);
    }
    public ItemUseResult Use()
    {
        Wrrr(Raycast());
        return new ItemUseResult() { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.HoldToUse };
    }
    private void Wrrr(RaycastHit hit)
    {
        if (hit.collider == null) return;

        EffectBundle bundle = new EffectBundle();
        foreach (var CircularStage in _data.CircularStages)
        {
            if(_fireRateTimer <= CircularStage.Duration)
            {
                if (CircularStage.EffectBundle == null)
                {
                    Debug.LogWarning("[Circular] EffectBundle is not assigned.", _item);
                    break;
                }
                bundle = CircularStage.EffectBundle;
                break;
            }
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
        _effect.Apply(bundle, context);
    }
    private RaycastHit Raycast()
    {
        Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);

        LayerMask mask = _item.ItemData.UseMask;
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, _data.MaxDistance, mask))
        {
            Debug.Log("Object ahead: " + hit.collider.name);
            return hit;
        }
        Debug.Log("No Object!");
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
        if (_item == null || _item.ItemData == null)
        {
            startDelay = 0f;
            useDelay = 0f;
            return;
        }

        startDelay = Mathf.Max(0f, _item.ItemData.StartUseDelay);
        useDelay = Mathf.Max(0f, _item.ItemData.UseDelay);
    }
}
