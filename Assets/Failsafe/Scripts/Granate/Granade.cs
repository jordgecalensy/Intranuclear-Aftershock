using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;
using System.Collections;

public abstract class Granade : MonoBehaviour
{
    [SerializeField] protected int ExplosionDamage;
    [SerializeField] protected float ExplosionRadius;
    public void Explosion()
    {
        Collider[] hitsInfo = Physics.OverlapSphere(transform.position, ExplosionRadius);
        foreach (var hitInfo in hitsInfo)
        {
            if (hitInfo.GetComponentInChildren<DamageableComponent>() != null)
            {
                DamageableComponent damageableComponent = hitInfo.GetComponent<DamageableComponent>();
                damageableComponent.TakeDamage(new FlatDamage(ExplosionDamage));
                Debug.Log($"{hitInfo} Take {ExplosionDamage} Damage");
                ExplosionEffect();
            }
        }
        Destroy(gameObject);
    }
    protected virtual void ExplosionEffect()
    {
        //тут пишутся эффекты для разных гранат
    }
}

