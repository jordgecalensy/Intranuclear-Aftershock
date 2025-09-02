using UnityEngine;

public class ElevatorController : MonoBehaviour
{
    public bool CanMove = false;
    // [SerializeField] private GameObject _points;
    [SerializeField] private float _speed = 2.0f;
    [SerializeField] private int _startPoint;
    [SerializeField] private Transform[] _points;

    private int _pointIndex;

    void Start()
    {
        _pointIndex = _startPoint;
        transform.position = _points[_pointIndex].position;
    }

    void FixedUpdate()
    {
        if (Vector3.Distance(transform.position, _points[_pointIndex].position) < 0.01f)
        {
            CanMove = false;
        }

        if (CanMove)
        {
            transform.position = Vector3.MoveTowards(transform.position, _points[_pointIndex].position, _speed * Time.deltaTime);
        }
    }

    public void OnButtonUpPress()
    {
        if (CanMove) return;
        if (_pointIndex == _points.Length - 1) return;

        Debug.Log("Elevator moving up");
        _pointIndex++;
        CanMove = true;
    }

    public void OnButtonDownPress()
    {
        if (CanMove) return;
        if (_pointIndex == 0) return;

        Debug.Log("Elevator moving down");
        _pointIndex--;
        CanMove = true;
    }
}
