using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(UIController))]
public class ElectricalPanelScript : Interactable, IEnterable
{
    [SerializeField]private PowerSource _powerSource;
    [SerializeField]private bool _isEnable = false;
    [SerializeField] private UIController _uiController;
    [SerializeField] private Animation _switchAnimation;
    private bool isBattaryInsert = false;
    [Header("InsertTrigger")]
    [SerializeField] private Collider _triggerCollider;
    [SerializeField] private Transform _holdPoint;

    private void Awake()
    {
        InsertTrigger.GetOrCreate(_triggerCollider.gameObject, this, _holdPoint);
    }

    private void Start()
    {
        _powerSource?.SetEnable(_isEnable);
    }
    private void OnEnablePowerSource()
    {
        _isEnable = true;
        _powerSource?.SetEnable(_isEnable);
        _switchAnimation?.Play("SwitchOn");
        _uiController.PullLever();
    }

    private void OnDisablePowerSource()
    {
        _isEnable = false;
        _powerSource?.SetEnable(_isEnable);
        _switchAnimation?.Play("SwitchOff");
        _uiController.OffLever();
    }

    protected override void Interact()
    {
        if (!isBattaryInsert) return;
        if (_isEnable)
            OnDisablePowerSource();
        else
            OnEnablePowerSource();
    }

    public void OnEntered()
    {
        isBattaryInsert = true;
        _uiController.BattaryOn();
    }

    public void OnExited()
    {
        isBattaryInsert = false;
        if (_isEnable) OnDisablePowerSource();
        _uiController.BattaryOff();
    }

    public bool IsRightType(Component candidate)
    {
        return candidate.GetComponent<ElectroBattary>() != null;
    }
}
