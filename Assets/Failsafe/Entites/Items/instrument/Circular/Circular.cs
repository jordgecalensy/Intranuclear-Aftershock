using Failsafe.Items;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class Circular : IUsable, ITickable
{
    [Inject] Camera _playerCam;

    private Item _item;
    private CircularData _data;
    private IEffectApplicationService _effects;
    private InputHandler _inputHandler;

    private float _durationWork = 0;

    public Circular(
        CircularData data, 
        IEffectApplicationService effects, 
        InputHandler inputHandler)
    {
        _data = data;
        _effects = effects;
        _inputHandler = inputHandler;
    }
    public void Tick()
    {
        if (_durationWork < 0)
            _durationWork = 0;
        if (_durationWork > _data.CircularStages[_data.CircularStages.Count - 1].StageDuration)
            _durationWork = _data.CircularStages[_data.CircularStages.Count - 1].StageDuration;

        if (_inputHandler.AttackTrigger.IsTriggered && _durationWork < _data.CircularStages[_data.CircularStages.Count - 1].StageDuration)
        {
            _durationWork += Time.deltaTime * _data.TimeChargeModifier;
        }
        else if (!_inputHandler.AttackTrigger.IsTriggered && _durationWork > 0)
        {
            _durationWork -= Time.deltaTime / _data.TimeDischargeModifier;
        }
        //Debug.Log("[Circular]  _fireRateTimer " + (int)_durationWork);
    }
    public ItemUseResult Use()
    {
        TryDamageDealing(Raycast());
        return new ItemUseResult() { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.HoldToUse };
    }
    private void TryDamageDealing(RaycastHit hit)
    {
        if (hit.collider == null) return;
        if (_effects == null)
        {
            Debug.LogError("[Circular] IEffectApplicationService is null.");
            return;
        }

        EffectBundle bundle = new EffectBundle();
        foreach (var CircularStage in _data.CircularStages)
        {
            if(_durationWork <= CircularStage.StageDuration)
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
