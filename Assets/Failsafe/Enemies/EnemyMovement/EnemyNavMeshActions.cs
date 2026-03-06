using UnityEngine;
using UnityEngine.AI;

public class EnemyNavMeshActions
{
    private NavMeshAgent _navMeshAgent;
    private Transform _enemyPos;

    public NavMeshAgent Agent => _navMeshAgent;
    public Transform   Model => _enemyPos;

    public EnemyNavMeshActions(NavMeshAgent navMeshAgent, Transform transform)
    {
        _navMeshAgent = navMeshAgent;
        _enemyPos =  transform;
    }

    public void MoveToPoint(Vector3 point, float speed)
    {
        if (_navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = speed;
            _navMeshAgent.SetDestination(point);
        }
    }

    public void StopMoving()
    {
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
        if (!_navMeshAgent.hasPath && !_navMeshAgent.pathPending) return true;

        if (_navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance)
        {
            if (!_navMeshAgent.hasPath || _navMeshAgent.velocity.sqrMagnitude < 0.05f)
            {
                return true;
            }
        }
        return false;
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