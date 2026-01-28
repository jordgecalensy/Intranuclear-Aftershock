using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private Image _ui1;
    [SerializeField] private Image _ui2;
    [SerializeField] private float _switchTime = 2f;

    private Coroutine _switchCoroutine;
    private bool _state;

    IEnumerator SwitchCanvas()
    {
        while (true)
        {
            _state = !_state;

            _ui1.enabled = _state;
            _ui2.enabled = !_state;

            yield return new WaitForSeconds(_switchTime);
        }
    }

    public void HideAll()
    {
        if (_switchCoroutine != null)
            StopCoroutine(_switchCoroutine);
        _switchCoroutine = null;
        _ui1.enabled = false;
        _ui2.enabled = false;
    }

    public void Show()
    {
        if (_switchCoroutine == null)
            _switchCoroutine = StartCoroutine(SwitchCanvas());
    }
}
