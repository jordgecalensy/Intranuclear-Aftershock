using UnityEngine;

public class ElevatorController : MonoBehaviour
{
    private bool _canMove = false;

    [SerializeField] private bool _isPowered;
    [SerializeField] private float _speed = 2.0f;
    [SerializeField] private int _startFloor;
    [SerializeField] private Transform[] _points;

    private int _pointIndex;

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
        if (Vector3.Distance(transform.position, _points[_pointIndex].position) < 0.01f)
        {
            _canMove = false;
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
        _canMove = true;
    }
    public void OnButtonDownPress()
    {
        if (!_isPowered) return;
        if (_canMove) return;
        if (_pointIndex == 0) return;

        Debug.Log("Elevator moving down");
        _pointIndex--;
        _canMove = true;
    }
    public void CallElevatorButton(int floorNumber)
    {
        if (!_isPowered) return;
        if (_canMove) return;
        if (_pointIndex == floorNumber) return;

        Debug.Log("Elevator calling");
        _pointIndex = floorNumber;
        _canMove = true;

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
    }
}
