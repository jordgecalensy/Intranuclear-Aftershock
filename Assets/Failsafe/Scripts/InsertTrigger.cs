using UnityEngine;

public sealed class InsertTrigger : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float speed = 2f;
    [SerializeField] private float delayTime = 2f;
    private bool _isHolding;
    private IInsertable _currentInsertable;

    private void FixedUpdate()
    {
        if (_currentInsertable != null) {
            if (!_isHolding && !_currentInsertable.IsGrabbed)
            {
                _currentInsertable.OnInserted(holdPoint, speed, delayTime);
                _isHolding = true;
            }

            if (_isHolding && _currentInsertable.IsGrabbed)
            {
                _currentInsertable.OnEjected();
                _isHolding = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_currentInsertable != null) return;

        _currentInsertable = other.GetComponent<IInsertable>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (_currentInsertable != null && other.GetComponent<IInsertable>() == _currentInsertable)
        {
            _currentInsertable = null;
        }
    }
}