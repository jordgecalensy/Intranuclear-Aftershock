using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ChargeStation : MonoBehaviour, IEnterable
{
    private bool _chargingOngoing;
    private bool _isEmpty = true;
    [SerializeField] private Animation _lidAnimation;
    [SerializeField] private Image _noItem;
    [SerializeField] private Image _itemPlaced;
    [SerializeField] private Image _chargeBar;
    [SerializeField] private Image[] _bars;
    public event Action<bool> UpdateIsEmpty;

    public void OnButtonPress()
    {
        if(!_chargingOngoing)
            StartCoroutine(Charging());
    }

    private IEnumerator Charging()
    {
        _chargingOngoing = true;
        _lidAnimation.Play("LidClose");
        _noItem.enabled = false;
        _itemPlaced.enabled = false;
        _chargeBar.enabled = true;
        foreach (Image bar in _bars)
        {
            bar.enabled = true;
        }

        yield return new WaitForSeconds(2);

        _lidAnimation.Play("LidOpen");
        _chargingOngoing = false;
        _noItem.enabled = false;
        _itemPlaced.enabled = true;
        _chargeBar.enabled = false;
        foreach (Image bar in _bars)
        {
            bar.enabled = false;
        }
        Debug.Log("FooEnd");
    }
    public void OnEntered()
    {
        _isEmpty = false;

        _noItem.enabled = false;
        _itemPlaced.enabled = true;
        _chargeBar.enabled = false;
        foreach (Image bar in _bars)
        {
            bar.enabled = false;
        }   
    }

    public void OnExited()
    {
        _noItem.enabled = true;
        _itemPlaced.enabled = false;
        _chargeBar.enabled = false;
        foreach (Image bar in _bars)
        {
            bar.enabled = false;
        }
        _isEmpty = true;
    }

    public bool IsRightType(Component candidate)
    {
        return candidate.GetComponent<Item>().ItemData.Type == ItemType.Gun;
    }

    public bool IsEmpty()
    {
        return _isEmpty;
    }
}
