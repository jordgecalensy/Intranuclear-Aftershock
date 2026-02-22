using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class ExplosiveObgect : MonoBehaviour
{
    [SerializeField] protected ExplosiveObjectData Data;
    public void Explosion()
    {
        Collider[] hitsInfo = Physics.OverlapSphere(transform.position, Data.ExplosionRadius);
        foreach (var hitInfo in hitsInfo)
        {
            Vector3 directionToEnemy = (hitInfo.transform.position - transform.position).normalized;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToEnemy, out hit, Data.ExplosionRadius))
            {
                if (HitsChecking(hit, hitInfo)) continue;
                DamagebleExplosionEffect(hitInfo);
                PhysicsExplosionEffect(hitInfo, directionToEnemy);
            }
        }
        SingleExplosionEffect();
        Destroy(gameObject);
    }
    protected virtual bool HitsChecking(RaycastHit hit, Collider hitInfo)
    {
        if (hitInfo.name != hit.collider.name)
        {
            if (hitInfo.tag == "Player" || hitInfo.tag == "Enemy")
                Debug.Log($"{hitInfo.gameObject.name} за препядствием {hit.collider.name}");
            return true;
        }
        return false;
    }
    protected virtual void DamagebleExplosionEffect(Collider hitInfo)
    {
        if (hitInfo.GetComponent<DamageableComponent>() == null) return;
        DamageableComponent damageableComponent = hitInfo.GetComponent<DamageableComponent>();
        damageableComponent.TakeDamage(new FlatDamage(Data.ExplosionDamage));
        Debug.Log($"{hitInfo.name} Take {Data.ExplosionDamage} Damage");
    }
    protected virtual void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy)
    {
        if (hitInfo.GetComponent<Rigidbody>() == null) return;
        Debug.Log($"Rigidbody: {hitInfo.name}");
        hitInfo.GetComponent<Rigidbody>().AddForce(directionToEnemy * Data.ExplosionForce, ForceMode.Impulse);

    }
    protected virtual void SingleExplosionEffect()
    {
        if(Data.ExplosiveVFX != null)
        {
            var Vfx = Instantiate(Data.ExplosiveVFX, gameObject.transform.position, Quaternion.identity);
            Destroy(Vfx, Data.DurationVFX);
            //Одиночный эффект после взрыва гранаты
        }
    }
}
