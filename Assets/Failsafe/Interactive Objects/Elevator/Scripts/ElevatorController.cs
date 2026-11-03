using UnityEngine;
using System;

public class ElevatorController : MonoBehaviour
{
    public event Action OnMoveStart;
    public event Action OnMoveStop;
    public event Action OnPowerOff;
    private bool _canMove = false;

    [SerializeField] private bool _isPowered;
    [SerializeField] private float _speed = 2.0f;
    [SerializeField] private int _startFloor;
    [SerializeField] private Transform[] _points;
    [SerializeField] private bool _loop;

    private int _pointIndex;
    private int _travelDirection = 1;
    private bool _loopStarted;

    private bool _isMoving = false; // ✅ Защита от повторных инвоков старта/стопа

    void Start()
    {
        _startFloor--;
        _pointIndex = _startFloor;
        transform.position = _points[_pointIndex].position;
    }

    void FixedUpdate()
    {
        if (!_isPowered) return;
        if (_isMoving && Vector3.Distance(transform.position, _points[_pointIndex].position) < 0.01f)
        {
            _canMove = false;
            _isMoving = false;
             OnMoveStop?.Invoke(); // уведомляем звук, что лифт остановился
            return;
        }

        if (_loop && _loopStarted && !_canMove && !_isMoving && _points.Length > 1)
        {
            if (_pointIndex == _points.Length - 1)
            {
                _travelDirection = -1;
            }
            else if (_pointIndex == 0)
            {
                _travelDirection = 1;
            }

            _pointIndex += _travelDirection;
            StartMovement();
        }

        if (_canMove)
        {
            transform.position = Vector3.MoveTowards(transform.position, _points[_pointIndex].position, _speed * Time.deltaTime);
        }
    }
    public void OnButtonUpPress()
    {
        if (!_isPowered) return;
        if (_canMove) return;
        if (_pointIndex == _points.Length - 1) return;

        global::Failsafe.Debugging.GameplayLog.Trace("Elevator moving up");
        _pointIndex++;
        StartMovementFromButton(1);
    }
    public void OnButtonDownPress()
    {
        if (!_isPowered) return;
        if (_canMove) return;
        if (_pointIndex == 0) return;

        global::Failsafe.Debugging.GameplayLog.Trace("Elevator moving down");
        _pointIndex--;
        StartMovementFromButton(-1);
    }
    public void CallElevatorButton(int floorNumber)
    {
        if (!_isPowered) return;
        if (_canMove) return;
        if (_pointIndex == floorNumber) return;

        global::Failsafe.Debugging.GameplayLog.Trace("Elevator calling");
        int direction = floorNumber > _pointIndex ? 1 : -1;
        _pointIndex = floorNumber;
        StartMovementFromButton(direction);

    }

    private void StartMovementFromButton(int direction)
    {
        _travelDirection = direction;
        _loopStarted = _loop;
        StartMovement();
    }

    private void StartMovement()
    {
        _canMove = true;

        //  Старт ОДИН раз
        if (!_isMoving)
        {
            _isMoving = true;
            OnMoveStart?.Invoke();
        }
    }
    public void OnPowered()
    {
        global::Failsafe.Debugging.GameplayLog.Trace($"{gameObject} power on");
        _isPowered = true;
    }
    public void OffPowered()
    {
        global::Failsafe.Debugging.GameplayLog.Trace($"{gameObject} power off");
        _isPowered = false;

         // если отключили во время движения — корректно останавливаем и даём стоп
        if (_isMoving)
        {
            _canMove = false;
            _isMoving = false;
            OnMoveStop?.Invoke();
        }
    }
}
