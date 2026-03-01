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

    private int _pointIndex;

    private bool _isMoving = false; // ✅ Защита от повторных инвоков старта/стопа

    void Start()
    {
        _startFloor--;
        _pointIndex = _startFloor;
        transform.position = _points[_pointIndex].position;
    }

    void FixedUpdate()
    {
        Debug.Log(_isPowered);
        if (!_isPowered) return;
        if (_isMoving && Vector3.Distance(transform.position, _points[_pointIndex].position) < 0.01f)
        {
            _canMove = false;
            _isMoving = false;
             OnMoveStop?.Invoke(); // уведомляем звук, что лифт остановился
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

        Debug.Log("Elevator moving up");
        _pointIndex++;
        StartMovement();
    }
    public void OnButtonDownPress()
    {
        if (!_isPowered) return;
        if (_canMove) return;
        if (_pointIndex == 0) return;

        Debug.Log("Elevator moving down");
        _pointIndex--;
        StartMovement();
    }
    public void CallElevatorButton(int floorNumber)
    {
        if (!_isPowered) return;
        if (_canMove) return;
        if (_pointIndex == floorNumber) return;

        Debug.Log("Elevator calling");
        _pointIndex = floorNumber;
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
        Debug.Log($"{gameObject} power on");
        _isPowered = true;
    }
    public void OffPowered()
    {
        Debug.Log($"{gameObject} power off");
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
