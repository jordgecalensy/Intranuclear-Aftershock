using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private Image _offEnergy1;
    [SerializeField] private Image _onEnergy1;
    [SerializeField] private Image _offEnergy2;
    [SerializeField] private Image _onEnergy2;
    [SerializeField] private Image _noEnergy;
    [SerializeField] private Image _warning;
    [SerializeField] private Image _battaryIs;
    [SerializeField] private Image _pullTheLever;
    [SerializeField] private Image _powerSupplyIs;
    [SerializeField] private float _switchTime = 2f;

    private Coroutine _switchNoBattaryCoroutine;
    private Coroutine _switchOnBattaryCoroutine;
    private bool _state;

    IEnumerator SwitchNoBattaryCanvas()
    {
        while (true)
        {
            _state = !_state;

            _noEnergy.enabled = _state;
            _warning.enabled = !_state;

            yield return new WaitForSeconds(_switchTime);
        }
    }

    IEnumerator SwitchOnBattaryCanvas()
    {
        yield return new WaitForSeconds(_switchTime);
        _battaryIs.enabled = false;
        _pullTheLever.enabled = true;
    }

    public void BattaryOn()
    {
        if (_switchNoBattaryCoroutine != null)
        {
            StopCoroutine(_switchNoBattaryCoroutine);
            _switchNoBattaryCoroutine = null;
        }

        if (_switchOnBattaryCoroutine != null)
            StopCoroutine(_switchOnBattaryCoroutine);

        _battaryIs.enabled = true;
        _pullTheLever.enabled = false;

        _switchOnBattaryCoroutine = StartCoroutine(SwitchOnBattaryCanvas());

        _noEnergy.enabled = false;
        _warning.enabled = false;

        _offEnergy1.enabled = false;
        _offEnergy2.enabled = false;
        _onEnergy1.enabled = true;
        _onEnergy2.enabled = true;
    }

    public void BattaryOff()
    {
        if (_switchOnBattaryCoroutine != null)
        {
            StopCoroutine(_switchOnBattaryCoroutine);
            _switchOnBattaryCoroutine = null;
        }

        if (_switchNoBattaryCoroutine == null)
            _switchNoBattaryCoroutine = StartCoroutine(SwitchNoBattaryCanvas());

        _battaryIs.enabled = false;
        _pullTheLever.enabled = false;
        _powerSupplyIs.enabled = false;

        _offEnergy1.enabled = true;
        _offEnergy2.enabled = true;
        _onEnergy1.enabled = false;
        _onEnergy2.enabled = false;
    }

    public void PullLever()
    {
        if (_switchOnBattaryCoroutine != null)
            StopCoroutine(_switchOnBattaryCoroutine);
        _pullTheLever.enabled = false;
        _battaryIs.enabled = false;
        _powerSupplyIs.enabled = true;
    }
    public void OffLever()
    {
        _battaryIs.enabled = false;
        _switchOnBattaryCoroutine = null;
        _pullTheLever.enabled = true;
        _powerSupplyIs.enabled = false;
    }
}
