using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Компонент, который вызывает стан врага, при столкновении врага с другими объектами
/// </summary>
public class EnemyPhysicsStunComponent : MonoBehaviour
{
    private Enemy_ScriptableObject _physicsStunData;
    private Collider[] _enemyColliders;
    private Enemy _enemy;
    private EnemyNavMeshActions _enemyNavMeshActions;
    void Start()
    {
        _enemyColliders = GetComponentsInChildren<Collider>();
        _enemy = GetComponent<Enemy>();
        _physicsStunData = _enemy.EnemyConfig;
        _enemyNavMeshActions = _enemy.EnemyNavMesh;
    }

    void OnCollisionEnter(Collision collision)
    {
        var stunTime = Mathf.Pow(collision.relativeVelocity.magnitude, 2) * collision.rigidbody.mass * _physicsStunData.StunMultiplier;
        Vector3 ImpactDirection = collision.impulse.normalized * -5f; //5 случайное значение, нам важно само направление
        if (stunTime > _physicsStunData.MinStunTime)
        {
            stunTime = Mathf.Min(stunTime, (float)_physicsStunData.MaxStunTime);
            _enemy.StunnedState(ImpactDirection, stunTime / 1000);
            Debug.Log("Стан: " + stunTime / 1000 + "с");
        }
        else
        {
            _enemyNavMeshActions.RotateToPoint(transform.position + ImpactDirection);
        }
    }
}