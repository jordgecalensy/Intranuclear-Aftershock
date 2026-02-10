using UnityEngine;

public class ElectricalPanelScript : Interactable, IEnterable
{
    [SerializeField]private PowerSource _powerSource;
    [SerializeField]private bool _isEnable;
    [SerializeField] private UIController _uiController;
    private bool isBattaryInsert = false;
    [Header("InsertTrigger")]
    [SerializeField] private Collider _triggerCollider;
    [SerializeField] private Transform _holdPoint;
    private InsertTrigger _insertTrigger;

    private void Awake()
    {
        _insertTrigger = InsertTrigger.GetOrCreate(_triggerCollider.gameObject, this, _holdPoint);
    }

    private void Start()
    {
        _powerSource.SetEnable(_isEnable);
    }
    private void OnEnablePowerSource()
    {
        _isEnable = !_isEnable;
        _powerSource.SetEnable(_isEnable);
    }
    protected override void Interact()
    {
        OnEnablePowerSource();
    }

    public void OnEntered()
    {
        isBattaryInsert = true;
        _uiController.HideAll();
    }

    public void OnExited()
    {
        isBattaryInsert = false;
        _uiController.Show();
        Debug.Log("Exited");
    }

    public bool IsRightType(Component candidate)
    {
        return candidate.GetComponent<ElectroBattary>() != null;
    }
}
