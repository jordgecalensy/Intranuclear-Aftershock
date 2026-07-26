using System;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(UIController))]
public class ElectricalPanelScript :
    Interactable,
    IEnterable,
    IRunPersistentStateProvider
{
    private const string PersistentStateTypeId = "electrical-panel";
    private const int PersistentStateVersion = 1;

    [SerializeField]private PowerSource _powerSource;
    [SerializeField]private bool _isEnable = false;
    [SerializeField] private UIController _uiController;
    [SerializeField] private Animation _switchAnimation;
    private bool _isBatteryInsert = false;
    [Header("InsertTrigger")]
    [SerializeField] private Collider _triggerCollider;
    [SerializeField] private Transform _holdPoint;

    private InsertTrigger _insertTrigger;

    public string StateTypeId => PersistentStateTypeId;
    public int StateVersion => PersistentStateVersion;

    private void Awake()
    {
        EnsureReferences();
    }

    private void Start()
    {
        EnsureReferences();
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
        if (!_isBatteryInsert) return;
        if (_isEnable)
            OnDisablePowerSource();
        else
            OnEnablePowerSource();
    }

    public void OnEntered()
    {
        _isBatteryInsert = true;
        _uiController.BattaryOn();
    }

    public void OnExited()
    {
        _isBatteryInsert = false;
        if (_isEnable) OnDisablePowerSource();
        _uiController.BattaryOff();
    }

    public bool IsRightType(Component candidate)
    {
        return candidate.GetComponent<ElectroBattary>() != null;
    }

    public bool IsEmpty()
    {
        return !_isBatteryInsert;
    }

    public string CapturePersistentState()
    {
        EnsureReferences();

        if (_isEnable && !_isBatteryInsert)
        {
            throw new InvalidOperationException(
                $"Electrical panel '{name}' is enabled without an inserted battery.");
        }

        ElectricalPanelPersistentState state =
            new ElectricalPanelPersistentState
            {
                isEnabled = _isEnable
            };

        if (_isBatteryInsert)
        {
            Insertable insertable = _insertTrigger.CurrentInsertable;
            if (insertable == null || !insertable.IsInserted)
            {
                throw new InvalidOperationException(
                    $"Electrical panel '{name}' reports an inserted battery, " +
                    "but its insert trigger has no attached object.");
            }

            if (insertable.GetComponent<ElectroBattary>() == null)
            {
                throw new InvalidOperationException(
                    $"Electrical panel '{name}' contains an unsupported insertable object.");
            }

            RunPersistentObject persistentObject =
                insertable.GetComponent<RunPersistentObject>();

            if (persistentObject == null ||
                string.IsNullOrWhiteSpace(persistentObject.PersistentId))
            {
                throw new InvalidOperationException(
                    $"Battery '{insertable.name}' has no persistent identity.");
            }

            state.insertedBatteryId = persistentObject.PersistentId.Trim();
        }

        return JsonUtility.ToJson(state);
    }

    public void RestorePersistentState(
        string serializedState,
        int stateVersion)
    {
        EnsureReferences();

        if (stateVersion != PersistentStateVersion)
        {
            throw new InvalidOperationException(
                $"Electrical panel state version {stateVersion} is not supported. " +
                $"Expected {PersistentStateVersion}.");
        }

        if (string.IsNullOrWhiteSpace(serializedState))
            throw new InvalidOperationException("Saved electrical panel state is empty.");

        ElectricalPanelPersistentState state =
            JsonUtility.FromJson<ElectricalPanelPersistentState>(
                serializedState);

        if (state == null)
            throw new InvalidOperationException("Saved electrical panel state is invalid.");

        string insertedBatteryId = state.insertedBatteryId?.Trim();
        bool hasInsertedBattery =
            !string.IsNullOrWhiteSpace(insertedBatteryId);

        if (state.isEnabled && !hasInsertedBattery)
        {
            throw new InvalidOperationException(
                $"Electrical panel '{name}' cannot restore enabled without a battery.");
        }

        if (hasInsertedBattery)
        {
            Insertable insertable =
                ResolveSavedBattery(insertedBatteryId);

            _insertTrigger.RestoreInserted(insertable);
            _isBatteryInsert = true;
        }
        else
        {
            _insertTrigger.RestoreEmpty();
            _isBatteryInsert = false;
        }

        _isEnable = state.isEnabled;
        _powerSource?.RestoreEnabledState(_isEnable);

        ApplyRestoredPresentation();
    }

    private void EnsureReferences()
    {
        if (_uiController == null)
            _uiController = GetComponent<UIController>();

        if (_triggerCollider == null)
        {
            throw new InvalidOperationException(
                $"Electrical panel '{name}' has no insert trigger collider.");
        }

        if (_holdPoint == null)
        {
            throw new InvalidOperationException(
                $"Electrical panel '{name}' has no battery hold point.");
        }

        _insertTrigger = InsertTrigger.GetOrCreate(
            _triggerCollider.gameObject,
            this,
            _holdPoint);
    }

    private Insertable ResolveSavedBattery(string persistentId)
    {
        RunPersistentObject[] persistentObjects =
            FindObjectsByType<RunPersistentObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        RunPersistentObject matchedObject = null;

        for (int i = 0; i < persistentObjects.Length; i++)
        {
            string candidateId =
                persistentObjects[i].PersistentId?.Trim();

            if (!string.Equals(
                    candidateId,
                    persistentId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (matchedObject != null)
            {
                throw new InvalidOperationException(
                    $"Battery persistent ID '{persistentId}' occurs more than once.");
            }

            matchedObject = persistentObjects[i];
        }

        if (matchedObject == null)
        {
            throw new InvalidOperationException(
                $"Saved battery '{persistentId}' is missing from the loaded scene.");
        }

        Insertable insertable = matchedObject.GetComponent<Insertable>();
        if (insertable == null ||
            matchedObject.GetComponent<ElectroBattary>() == null)
        {
            throw new InvalidOperationException(
                $"Persistent object '{persistentId}' is not an electrical battery.");
        }

        return insertable;
    }

    private void ApplyRestoredPresentation()
    {
        if (_isBatteryInsert)
        {
            _uiController.BattaryOn();

            if (_isEnable)
                _uiController.PullLever();
            else
                _uiController.OffLever();
        }
        else
        {
            _uiController.BattaryOff();
        }

        ApplySwitchAnimationPose(
            _isEnable
                ? "SwitchOn"
                : "SwitchOff");
    }

    private void ApplySwitchAnimationPose(string clipName)
    {
        if (_switchAnimation == null)
            return;

        AnimationState animationState = _switchAnimation[clipName];
        if (animationState == null)
            return;

        _switchAnimation.Play(clipName);
        animationState.normalizedTime = 1f;
        _switchAnimation.Sample();
        _switchAnimation.Stop();
    }

    [Serializable]
    private sealed class ElectricalPanelPersistentState
    {
        public bool isEnabled;
        public string insertedBatteryId;
    }
}
