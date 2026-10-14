using UnityEngine;
using UnityEngine.AI;

public class EnemyNavMeshActions
{
    private NavMeshAgent _navMeshAgent;
    private Transform _enemyPos;
    private EnemyLinkTraverser _linkTraverser;
    private Vector3 _requestedDestination;
    private bool _hasRequestedDestination;

    public NavMeshAgent Agent => _navMeshAgent;
    public Transform   Model => _enemyPos;

    public EnemyNavMeshActions(
        NavMeshAgent navMeshAgent,
        Transform transform,
        EnemyLinkTraverser linkTraverser = null)
    {
        _navMeshAgent = navMeshAgent;
        _enemyPos =  transform;
        _linkTraverser = linkTraverser;
    }

    public void MoveToPoint(Vector3 point, float speed, Transform targetRoot = null)
    {
        if (_navMeshAgent.isOnNavMesh)
        {
            _requestedDestination = EnemyMovement.ProjectWalkingDestination(_navMeshAgent, point);
            _hasRequestedDestination = true;
            if (_linkTraverser != null)
            {
                _linkTraverser.SetDestination(point, speed, targetRoot);
                return;
            }

            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = speed;
            _navMeshAgent.SetDestination(point);
        }
    }

    public void StopMoving()
    {
        _hasRequestedDestination = false;
        _linkTraverser?.ClearDestination();

        if (_navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
        }
    }

    public void ResumeMoving()
    {
        if (_navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = false;
        }
    }
    
    public bool IsPointReached()
    {
        if (_linkTraverser != null)
            return _linkTraverser.IsDestinationReached(_navMeshAgent.stoppingDistance);
        return _hasRequestedDestination && EnemyMovement.HasReachedPosition(
            _navMeshAgent, _requestedDestination, _navMeshAgent.stoppingDistance);
    }
    
    public void SetStoppingDistance(float distance)
    {
        if (_navMeshAgent != null)
        {
            _navMeshAgent.stoppingDistance = distance;
        }
    }

    public float GetStoppingDistance()
    {
        return _navMeshAgent != null ? _navMeshAgent.stoppingDistance : 0f;
    }
}
