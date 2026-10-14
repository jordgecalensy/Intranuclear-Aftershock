using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// MOTOR: Отвечает за физическое перемещение, вращение, прыжки и синхронизацию с NavMesh.
/// Реализует логику Stop-Turn-Go.
/// </summary>
public class EnemyMovement
{
    private readonly Transform _transform;
    private readonly NavMeshAgent _agent;
    private readonly Rigidbody _rb;
    private readonly MonoBehaviour _coroutineRunner; // Ссылка на Enemy.cs для запуска корутин
    private readonly bool _useRootMotion;
    private readonly EnemyLinkTraverser _linkTraverser;
    private Vector3 _requestedDestination;
    private bool _hasRequestedDestination;

    // Настройки (можно вынести в ScriptableObject)
    private const float TurnThreshold = 45f; // Угол, при котором начинаем разворот на месте
    private const float MoveThreshold = 10f; // Угол, при котором разрешаем идти
    private const float RotationSpeed = 300f; // Скорость вращения (град/сек)

    // Состояние
    public bool IsRotatingInPlace { get; private set; }
    public bool IsBusy { get; private set; } // Флаг для прыжков/спецдействий
    public float CurrentSpeed => _agent.velocity.magnitude;
    public Vector3 DesiredVelocity => _agent.desiredVelocity;

    public EnemyMovement(
        Transform transform,
        NavMeshAgent agent,
        Rigidbody rb,
        MonoBehaviour runner,
        bool useRootMotion,
        EnemyLinkTraverser linkTraverser = null)
    {
        _transform = transform;
        _agent = agent;
        _rb = rb;
        _coroutineRunner = runner;
        _useRootMotion = useRootMotion;
        _linkTraverser = linkTraverser;

        // Полный контроль над вращением и позицией
        _agent.updateRotation = false; 
        if (_useRootMotion) _agent.updatePosition = false; 
    }

    /// <summary>
    /// Основной метод обновления физики. Вызывать в Update().
    /// </summary>
    public void HandleRotationAndMovement()
    {
        if (IsBusy || !_agent.enabled || !_agent.isOnNavMesh) return;
        // Только остановку для собственного разворота мотор снимает сам.
        // Stop от патруля/боя действует до следующей команды движения.
        if (_agent.isStopped && !IsRotatingInPlace) return;

        // 1. Вычисляем цель поворота
        Vector3 targetDir = GetTargetDirection();
        float angleToTarget = CalculateAngle(targetDir);

        // 2. Логика Stop-Turn-Go
        if (!IsRotatingInPlace)
        {
            // Если движемся, но угол стал слишком большим -> СТОП
            if (Mathf.Abs(angleToTarget) > TurnThreshold && HasInput())
            {
                IsRotatingInPlace = true;
            }
        }
        else
        {
            // Если вращаемся и почти довернулись -> ИДТИ
            if (Mathf.Abs(angleToTarget) < MoveThreshold)
            {
                IsRotatingInPlace = false;
            }
        }

        // 3. Применение физики
        if (IsRotatingInPlace)
        {
            StopNavMeshAgent(true);
            RotateTowards(targetDir);
        }
        else
        {
            StopNavMeshAgent(false); // Разрешаем движение
            
            // Доворачиваем на ходу для плавности
            if (_agent.velocity.sqrMagnitude > 0.1f)
            {
                RotateTowards(_agent.velocity.normalized);
            }
        }
    }

    /// <summary>
    /// Команда на перемещение в точку.
    /// </summary>
    public void MoveTo(Vector3 destination, float speed)
    {
        if (!_agent.isOnNavMesh || IsBusy) return;
        _requestedDestination = ProjectWalkingDestination(_agent, destination);
        _hasRequestedDestination = true;

        if (_linkTraverser != null)
        {
            _linkTraverser.SetDestination(destination, speed);
            return;
        }

        _agent.speed = speed;
        _agent.isStopped = false;
        _agent.SetDestination(destination);
    }

    /// <summary>
    /// Команда полной остановки (для Idle, Attack).
    /// </summary>
    public void Stop()
    {
        _hasRequestedDestination = false;
        IsRotatingInPlace = false;
        _linkTraverser?.ClearDestination();

        if (_agent.isOnNavMesh) 
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Команда "Посмотреть в точку" (для Атаки стоя).
    /// </summary>
    public void LookAt(Vector3 targetPoint)
    {
        if (IsBusy) return;
        Vector3 dir = targetPoint - _transform.position;
        RotateTowards(dir);
    }

    /// <summary>
    /// Проверка достижения цели.
    /// </summary>
    public bool IsPointReached(float stoppingDistance = 0.5f)
    {
        if (_linkTraverser != null)
            return _linkTraverser.IsDestinationReached(stoppingDistance);
        return _hasRequestedDestination && HasReachedPosition(_agent, _requestedDestination, stoppingDistance);
    }

    internal static bool HasReachedPosition(NavMeshAgent agent, Vector3 destination, float stoppingDistance)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending ||
            agent.isOnOffMeshLink || agent.pathStatus != NavMeshPathStatus.PathComplete)
            return false;
        float tolerance = Mathf.Max(0.05f, stoppingDistance);
        if (agent.hasPath && (float.IsInfinity(agent.remainingDistance) || agent.remainingDistance > tolerance))
            return false;

        // remainingDistance == 0 бывает также при ResetPath/потере пути.
        // Сверяем фактическое положение с конечной целью, а не концом partial path.
        Vector3 offset = destination - (agent.nextPosition - Vector3.up * agent.baseOffset);
        return offset.sqrMagnitude <= tolerance * tolerance;
    }

    internal static Vector3 ProjectWalkingDestination(NavMeshAgent agent, Vector3 destination)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        return NavMesh.SamplePosition(destination, out NavMeshHit hit, 1f, filter) ? hit.position : destination;
    }

    public void SetTraversalBusy(bool isBusy)
    {
        IsBusy = isBusy;
        if (isBusy)
            IsRotatingInPlace = false;
    }

    /// <summary>
    /// Синхронизация Root Motion (вызывать из OnAnimatorMove).
    /// </summary>
    public void ApplyRootMotion(Vector3 deltaPosition)
    {
        if (!_useRootMotion || IsBusy) return;

        _agent.nextPosition = _transform.position + deltaPosition;
        _transform.position = _agent.nextPosition;
    }

    // --- Внутренние методы ---

    private Vector3 GetTargetDirection()
    {
        // steeringTarget стабильнее, чем desiredVelocity
        if (!_agent.hasPath || _agent.pathPending) return _agent.desiredVelocity;
        return (_agent.steeringTarget - _transform.position).normalized;
    }

    private float CalculateAngle(Vector3 targetDir)
    {
        targetDir.y = 0;
        if (targetDir.sqrMagnitude < 0.001f) return 0f;
        return Vector3.SignedAngle(_transform.forward, targetDir, Vector3.up);
    }

    private bool HasInput() => _agent.desiredVelocity.sqrMagnitude > 0.01f;

    private void StopNavMeshAgent(bool stop)
    {
        if (_agent.isOnNavMesh) _agent.isStopped = stop;
    }

    private void RotateTowards(Vector3 dir)
    {
        dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        _transform.rotation = Quaternion.RotateTowards(_transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
    }
    

   
    
}
