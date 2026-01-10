using UnityEngine;

public class CheckState : BehaviorState
{
    private Vector3 _originPoint;
    private Vector3 _targetPoint;
    private Vector3 _searchDirection;
    private float _checkTimer;

    private bool _hasReachedOrigin;
    private bool _isWaiting;
    private float _waitTimer;

    private Sensor[] _sensors;
    private EnemyMovePatterns _enemyMovePatterns;
    
    // ЗАМЕНА: Новый класс Motor
    private EnemyMovement _movement; 
    
    private Enemy_ScriptableObject _config;
    private Transform _transform;
    private EnemyAudioManager _audio;
    
    public bool CheckEnd() => _checkTimer >= _config.CheckDuration;

    // Конструктор обновлен
    public CheckState(Sensor[] sensors, Transform transform, EnemyMovePatterns enemyMovePatterns, 
                      EnemyMovement movement, Enemy_ScriptableObject config, EnemyAudioManager audio)
    {
        _sensors = sensors;
        _transform = transform;
        _enemyMovePatterns = enemyMovePatterns;
        _movement = movement;
        _config = config;
        _audio = audio;
    }

    public override void Enter()
    {
        base.Enter();
        _hasReachedOrigin = false;
        _isWaiting = false;
        _waitTimer = 0f;
        _checkTimer = 0f;
        _audio.PlayStateVoice(1);
        // Берём первую активную точку сигнала
        foreach (var sensor in _sensors)
        {
            if (sensor.IsActivated() && sensor.SignalSourcePosition.HasValue)
            {
                _originPoint = sensor.SignalSourcePosition.Value;
                _searchDirection = (sensor.SignalSourcePosition.Value - _transform.position).normalized;
                
                // Команда Мотору: Иди проверять шум
                _movement.MoveTo(_originPoint, _config.PatrolingSpeed);
                break;
            }
        }
    }

    public override void Update()
    {
        base.Update();

        // 1. Идем к источнику шума
        if (!_hasReachedOrigin)
        {
            if (_movement.IsPointReached(1.0f))
            {
                _movement.Stop();
                _hasReachedOrigin = true;
                _isWaiting = true;
                _waitTimer = _config.PatrollingWaitTime;
            }
            return;
        }

        // 2. Ждем на точке
        if (_isWaiting)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                _isWaiting = false;
                PickPoint(_transform.position);
            }
            return;
        }

        // 3. Идем в случайную точку рядом
        if (_movement.IsPointReached(1.0f))
        {
            _movement.Stop();
            _checkTimer += Time.deltaTime;
            _isWaiting = true;
            _waitTimer = _config.changePointInterval;
        }
    }

    private void PickPoint(Vector3 center)
    {
        // Ваша логика случайной точки вокруг
        _targetPoint = _enemyMovePatterns.RandomPointAround(_originPoint, _config.CheckRadius);
        _movement.MoveTo(_targetPoint, _config.PatrolingSpeed);
    }

    public override void Exit()
    {
        base.Exit();
        _isWaiting = false;
        _waitTimer = 0f;
        _checkTimer = 0f;
    }
}