using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;
using System.Collections;

public abstract class Granade : MonoBehaviour
{
    [SerializeField] protected GranadeData Data;
    [SerializeField] protected GameObject MineTrigger;

    protected bool ItsMineState = false;
    protected bool InstaledMine = false;
    public void Explosion()
    {
        //Collider[] hitsInfo = Physics.OverlapSphere(transform.position, Data.ExplosionRadius);
        //foreach (var hitInfo in hitsInfo)
        //{
        //    Vector3 directionToEnemy = (hitInfo.transform.position - transform.position).normalized;
        //    RaycastHit hit;
        //    if (Physics.Raycast(transform.position, directionToEnemy, out hit, Data.ExplosionRadius))
        //    {
        //        if (hitInfo.name != hit.collider.name)
        //        {
        //            if (hitInfo.tag == "Player" || hitInfo.tag == "Enemy")
        //                Debug.Log($"{hitInfo.gameObject.name} за препядствием {hit.collider.name}");
        //            continue;
        //        }
        //        if (hit.collider.GetComponent<DamageableComponent>() != null)
        //        {
        //            DamageableComponent damageableComponent = hit.collider.GetComponent<DamageableComponent>();
        //            damageableComponent.TakeDamage(new FlatDamage(Data.ExplosionDamage));
        //            Debug.Log($"{hit.collider.name} Take {Data.ExplosionDamage} Damage");
        //            ExplosionEffect();
        //        }
        //        if (hit.collider.GetComponent<Rigidbody>() != null)
        //        {
        //            Debug.Log($"Rigidbody: {hit.collider.name}");
        //            hit.collider.GetComponent<Rigidbody>().AddForce(directionToEnemy * Data.ExplosionForce, ForceMode.Impulse); 
        //        }
        //    }
        //}
        SingleExplosionEffect();
        Destroy(gameObject);
    }
    public void ActiveMineState()
    {
        ItsMineState = true;
    }
    protected virtual void ExplosionEffect()
    {
        //эффекты для разных гранат
    }
    protected virtual void SingleExplosionEffect()
    {
        //Одиночный эффект перед взрывом гранаты
    }
    protected void OnCollisionEnter(Collision collision)
    {
        if (!ItsMineState) return;
        if (collision.gameObject.tag == "Player") return;
        Debug.Log("collide " + gameObject + " With " + collision.gameObject.name);
        transform.SetParent(collision.transform);
        gameObject.GetComponent<Rigidbody>().isKinematic = true;
        gameObject.GetComponent<Collider>().enabled = false;
        MineTrigger.SetActive(true);
    }
}