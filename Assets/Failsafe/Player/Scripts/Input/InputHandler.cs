using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Класс работы с инпутом от игрока
/// </summary>
public class InputHandler : System.IDisposable
{
    private readonly InputActionAsset _playerControls;
    private InputActionMap _playerActionMap;
    private bool _isDisposed;

    public InputHandler(InputActionAsset playerControls)
    {
        _playerControls = playerControls;
        Init();
        SubscribeOnActionsPerformed();
        SetGameplayInputEnabled(true);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        UnsubscribeActionValuesFromInputEvents();
        UnsubscribeOnActionsPerformed();
        ResetCachedInputState();

        _isDisposed = true;
    }

    private const string _actionMapName = "Player";
    private const string _movement = "Movement";
    private const string _rotation = "Rotation";
    private const string _jump = "Jump";
    private const string _sprint = "Sprint";
    private const string _crouch = "Crouch";
    private const string _grabOrDrop = "GrabOrDrop";
    private const string _attack = "FlashLight";
    private const string _grabLedge = "GrabLedge";
    private const string _zoom = "Zoom";
    private const string _use = "Attack";
    private const string _altMode = "AltMode";
    private const string _visor = "Visor"; ///Добавил 
    private const string _throwObject = "ThrowObject"; ///временно
    private const string _slantRight = "SlantRight"; ///временно
    private const string _slantLeft = "SlantLeft"; ///временно

    private InputAction _movementAction;
    private InputAction _rotationAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _crouchAction;
    public InputAction GrabOrDropAction;
    public InputAction FlashLightAction;
    private InputAction _grabLedgeAction;
    private InputAction _zoomAction;
    private InputAction _attackAction;
    private InputAction _altModeAction;
    private InputAction _visorAction; ///Добавил
    public InputAction ThrowObjectAction; ///временно
    private InputAction _slantRightAction;
    private InputAction _slantLeftAction;


    public List<InputAction> PerformedActions = new List<InputAction>();

    public Vector2 MovementInput { get; private set; }
    public bool MoveForward => MovementInput.y > 0;
    public bool MoveBack => MovementInput.y < 0;
    public Vector2 RotationInput { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool SprintTriggered { get; private set; }
    public InputTrigger CrouchTrigger { get; private set; } = new InputTrigger();
    public bool FlashLightTriggered { get; private set; }
    public InputTrigger GrabLedgeTrigger { get; private set; } = new InputTrigger();
    public bool ZoomTriggered { get; private set; }
    public InputTrigger AttackTrigger { get; private set; } = new InputTrigger();
    public InputTrigger AltModeTrigger { get; private set; } = new InputTrigger();
    public InputTrigger VisorTrigger { get; private set; } = new InputTrigger();
    public bool SlantRightTrigger { get; private set; } 
    public bool SlantLeftTrigger { get; private set; }


    /// <summary>
    /// Преобразовать MovementInput к нужному Transform
    /// </summary>
    /// <param name="transform"></param>
    /// <returns></returns>
    public Vector3 GetRelativeMovement(Transform transform)
    {
        return Vector3.ClampMagnitude(MovementInput.x * transform.right + MovementInput.y * transform.forward, 1);
    }

    private void Init()
    {
        _playerActionMap = _playerControls.FindActionMap(_actionMapName);

        _movementAction = _playerActionMap.FindAction(_movement);
        _rotationAction = _playerActionMap.FindAction(_rotation);
        _jumpAction = _playerActionMap.FindAction(_jump);
        _sprintAction = _playerActionMap.FindAction(_sprint);
        _crouchAction = _playerActionMap.FindAction(_crouch);
        GrabOrDropAction = _playerActionMap.FindAction(_grabOrDrop);
        FlashLightAction = _playerActionMap.FindAction(_attack);
        _grabLedgeAction = _playerActionMap.FindAction(_grabLedge);
        _zoomAction = _playerActionMap.FindAction(_zoom);
        _attackAction = _playerActionMap.FindAction(_use);
        _altModeAction = _playerActionMap.FindAction(_altMode);
        _visorAction = _playerActionMap.FindAction(_visor);
        ThrowObjectAction = _playerActionMap.FindAction(_throwObject);
        _slantRightAction = _playerActionMap.FindAction(_slantRight);
        _slantLeftAction = _playerActionMap.FindAction(_slantLeft);

        SubscribeActionValuesToInputEvents();
    }

    private void SubscribeOnActionsPerformed()
    {
        foreach (var actionMap in _playerControls.actionMaps)
        {
            foreach (var action in actionMap.actions)
            {
                action.performed += AddPerformedAction;
                action.canceled += RemovePerformedAction;
            }
        }
    }

    private void UnsubscribeOnActionsPerformed()
    {
        foreach (var actionMap in _playerControls.actionMaps)
        {
            foreach (var action in actionMap.actions)
            {
                action.performed -= AddPerformedAction;
                action.canceled -= RemovePerformedAction;
            }
        }
    }

    public void AddPerformedAction(InputAction.CallbackContext context)
    { if (!PerformedActions.Contains(context.action)) PerformedActions.Add(context.action); }

    public void RemovePerformedAction(InputAction.CallbackContext context) =>
        PerformedActions.Remove(context.action);


    private void SubscribeActionValuesToInputEvents()
    {
        _movementAction.performed += OnMovementPerformed;
        _movementAction.canceled += OnMovementCanceled;

        _rotationAction.performed += OnRotationPerformed;
        _rotationAction.canceled += OnRotationCanceled;

        _jumpAction.performed += OnJumpPerformed;
        _jumpAction.canceled += OnJumpCanceled;

        _sprintAction.performed += OnSprintPerformed;
        _sprintAction.canceled += OnSprintCanceled;

        _crouchAction.performed += CrouchTrigger.OnInputStart;
        _crouchAction.canceled += CrouchTrigger.OnInputCancel;

        FlashLightAction.performed += OnFlashLightPerformed;
        FlashLightAction.canceled += OnFlashLightCanceled;

        _grabLedgeAction.performed += GrabLedgeTrigger.OnInputStart;
        _grabLedgeAction.canceled += GrabLedgeTrigger.OnInputCancel;

        _zoomAction.performed += OnZoomPerformed;
        _zoomAction.canceled += OnZoomCanceled;

        _attackAction.performed += AttackTrigger.OnInputStart;
        _attackAction.canceled += AttackTrigger.OnInputCancel;

        _altModeAction.performed += AltModeTrigger.OnInputStart;
        _altModeAction.canceled += AltModeTrigger.OnInputCancel;

        _visorAction.performed += VisorTrigger.OnInputStart;
        _visorAction.canceled += VisorTrigger.OnInputCancel;

        _slantRightAction.performed += OnSlantRightPerformed;
        _slantRightAction.canceled += OnSlantRightCanceled;

        _slantLeftAction.performed += OnSlantLeftPerformed;
        _slantLeftAction.canceled += OnSlantLeftCanceled;
    }

    private void UnsubscribeActionValuesFromInputEvents()
    {
        _movementAction.performed -= OnMovementPerformed;
        _movementAction.canceled -= OnMovementCanceled;

        _rotationAction.performed -= OnRotationPerformed;
        _rotationAction.canceled -= OnRotationCanceled;

        _jumpAction.performed -= OnJumpPerformed;
        _jumpAction.canceled -= OnJumpCanceled;

        _sprintAction.performed -= OnSprintPerformed;
        _sprintAction.canceled -= OnSprintCanceled;

        _crouchAction.performed -= CrouchTrigger.OnInputStart;
        _crouchAction.canceled -= CrouchTrigger.OnInputCancel;

        FlashLightAction.performed -= OnFlashLightPerformed;
        FlashLightAction.canceled -= OnFlashLightCanceled;

        _grabLedgeAction.performed -= GrabLedgeTrigger.OnInputStart;
        _grabLedgeAction.canceled -= GrabLedgeTrigger.OnInputCancel;

        _zoomAction.performed -= OnZoomPerformed;
        _zoomAction.canceled -= OnZoomCanceled;

        _attackAction.performed -= AttackTrigger.OnInputStart;
        _attackAction.canceled -= AttackTrigger.OnInputCancel;

        _altModeAction.performed -= AltModeTrigger.OnInputStart;
        _altModeAction.canceled -= AltModeTrigger.OnInputCancel;

        _visorAction.performed -= VisorTrigger.OnInputStart;
        _visorAction.canceled -= VisorTrigger.OnInputCancel;

        _slantRightAction.performed -= OnSlantRightPerformed;
        _slantRightAction.canceled -= OnSlantRightCanceled;

        _slantLeftAction.performed -= OnSlantLeftPerformed;
        _slantLeftAction.canceled -= OnSlantLeftCanceled;
    }

    private void OnMovementPerformed(InputAction.CallbackContext context)
    {
        MovementInput = context.ReadValue<Vector2>();
    }

    private void OnMovementCanceled(InputAction.CallbackContext context)
    {
        MovementInput = Vector2.zero;
    }

    private void OnRotationPerformed(InputAction.CallbackContext context)
    {
        RotationInput = context.ReadValue<Vector2>();
    }

    private void OnRotationCanceled(InputAction.CallbackContext context)
    {
        RotationInput = Vector2.zero;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        JumpTriggered = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        JumpTriggered = false;
    }

    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        SprintTriggered = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        SprintTriggered = false;
    }

    private void OnFlashLightPerformed(InputAction.CallbackContext context)
    {
        FlashLightTriggered = true;
    }

    private void OnFlashLightCanceled(InputAction.CallbackContext context)
    {
        FlashLightTriggered = false;
    }

    private void OnZoomPerformed(InputAction.CallbackContext context)
    {
        ZoomTriggered = true;
    }

    private void OnZoomCanceled(InputAction.CallbackContext context)
    {
        ZoomTriggered = false;
    }

    private void OnSlantRightPerformed(InputAction.CallbackContext context)
    {
        SlantRightTrigger = true;
    }

    private void OnSlantRightCanceled(InputAction.CallbackContext context)
    {
        SlantRightTrigger = false;
    }

    private void OnSlantLeftPerformed(InputAction.CallbackContext context)
    {
        SlantLeftTrigger = true;
    }

    private void OnSlantLeftCanceled(InputAction.CallbackContext context)
    {
        SlantLeftTrigger = false;
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        if (_playerActionMap == null)
            return;

        if (enabled)
        {
            _playerActionMap.Enable();
            return;
        }

        ResetCachedInputState();
        _playerActionMap.Disable();
    }

    private void ResetCachedInputState()
    {
        MovementInput = Vector2.zero;
        RotationInput = Vector2.zero;
        JumpTriggered = false;
        SprintTriggered = false;
        FlashLightTriggered = false;
        ZoomTriggered = false;
        SlantRightTrigger = false;
        SlantLeftTrigger = false;

        CrouchTrigger.Reset();
        GrabLedgeTrigger.Reset();
        AttackTrigger.Reset();
        AltModeTrigger.Reset();
        VisorTrigger.Reset();
        PerformedActions.Clear();
    }

    public class InputTrigger
    {
        /// <summary>
        /// Инпут активирован
        /// </summary>
        public bool IsTriggered { get; private set; }
        /// <summary>
        /// Инпут удерживается
        /// </summary>
        public bool IsPressed { get; private set; }

        public void OnInputStart(InputAction.CallbackContext context)
        {
            IsTriggered = true;
            IsPressed = true;
        }

        public void OnInputCancel(InputAction.CallbackContext context)
        {
            IsTriggered = false; 
            IsPressed = false;
        }

        /// <summary>
        /// Вызывать когда инпут обработан.
        /// </summary>
        public void ReleaseTrigger()
        {
            IsTriggered = false;
        }

        public void Reset()
        {
            IsTriggered = false;
            IsPressed = false;
        }
    }
}

