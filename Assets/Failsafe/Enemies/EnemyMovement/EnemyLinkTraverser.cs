using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Отвечает за преодоление NavMeshLink (прыжки уступов, пропастей).
/// </summary>
public class EnemyLinkTraverser : MonoBehaviour
{
    [Header("Настройки прыжка")]
    [Tooltip("Кривая высоты прыжка (ось X - время от 0 до 1, ось Y - высота)")]
    [SerializeField] private AnimationCurve _jumpCurve = new AnimationCurve(
        new Keyframe(0f, 0f), 
        new Keyframe(0.5f, 1f), 
        new Keyframe(1f, 0f)
    );
    
    [SerializeField] private float _baseJumpDuration = 0.5f;
    [SerializeField] private float _heightMultiplier = 2.0f;

    private NavMeshAgent _agent;
    private EnemyAnimator _animator;
    private bool _isTraversing = false;

    public bool IsTraversing => _isTraversing;

    public void Initialize(NavMeshAgent agent, EnemyAnimator animator)
    {
        _agent = agent;
        _animator = animator;
        _agent.autoTraverseOffMeshLink = false; // Отключаем стандартное (телепортирующее) перемещение
    }

    public void CheckAndTraverseLink()
    {
        if (_isTraversing || !_agent.isOnOffMeshLink) return;

        StartCoroutine(TraverseOffMeshLinkRoutine());
    }

    private IEnumerator TraverseOffMeshLinkRoutine()
    {
        _isTraversing = true;
        
        OffMeshLinkData data = _agent.currentOffMeshLinkData;
        Vector3 startPos = _agent.transform.position;
        // Конечная точка с учетом смещения агента
        Vector3 endPos = data.endPos + Vector3.up * _agent.baseOffset; 
        
        // Поворачиваемся лицом к точке приземления
        Vector3 lookDirection = endPos - startPos;
        lookDirection.y = 0;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        // Запускаем анимацию
         _animator.Jump();

        float distance = Vector3.Distance(startPos, endPos);
        float duration = Mathf.Max(_baseJumpDuration, distance / _agent.speed);
        float normalizedTime = 0.0f;

        while (normalizedTime < 1.0f)
        {
            normalizedTime += Time.deltaTime / duration;
            
            // Получаем высоту из кривой
            float yOffset = _jumpCurve.Evaluate(normalizedTime) * _heightMultiplier;
            
            // Перемещаем трансформ линейно + добавляем высоту
            Vector3 targetPosition = Vector3.Lerp(startPos, endPos, normalizedTime);
            targetPosition.y += yOffset;
            
            transform.position = targetPosition;
            
            yield return null;
        }

        transform.position = endPos;
        _animator.Land();
        
        _agent.CompleteOffMeshLink();
        _isTraversing = false;
    }
}