using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;

public abstract class ExplosiveObgect : MonoBehaviour
{
    [SerializeField] protected ExplosiveObjectData Data;
    public void Explosion()
    {
        Debug.Log("boom");
        Collider[] hitsInfo = Physics.OverlapSphere(transform.position, Data.ExplosionRadius);
        foreach (var hitInfo in hitsInfo)
        {
            Vector3 directionToEnemy = (hitInfo.transform.position - transform.position).normalized;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToEnemy, out hit, Data.ExplosionRadius))
            {
                if (hitInfo.name != hit.collider.name)
                {
                    if (hitInfo.tag == "Player" || hitInfo.tag == "Enemy")
                        Debug.Log($"{hitInfo.gameObject.name} за препядствием {hit.collider.name}");
                    continue;
                }
                if (hit.collider.GetComponent<DamageableComponent>() != null)
                {
                    DamageableComponent damageableComponent = hit.collider.GetComponent<DamageableComponent>();
                    damageableComponent.TakeDamage(new FlatDamage(Data.ExplosionDamage));
                    Debug.Log($"{hit.collider.name} Take {Data.ExplosionDamage} Damage");
                    ExplosionEffect();
                }
                if (hit.collider.GetComponent<Rigidbody>() != null)
                {
                    Debug.Log($"Rigidbody: {hit.collider.name}");
                    hit.collider.GetComponent<Rigidbody>().AddForce(directionToEnemy * Data.ExplosionForce, ForceMode.Impulse);
                }
            }
        }
        SingleExplosionEffect();
        Destroy(gameObject);
    }
    protected virtual void ExplosionEffect()
    {
        //эффекты для разных гранат
    }
    protected virtual void SingleExplosionEffect()
    {
        //Одиночный эффект перед взрывом гранаты
    }
}
