using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;
using System.Collections;

public abstract class Granade : MonoBehaviour
{
    [SerializeField] protected GranadeData Data;
    protected bool ItsMineState = false;
    protected bool InstaledMine = false;
    public void Explosion()
    {
        Collider[] hitsInfo = Physics.OverlapSphere(transform.position, Data.ExplosionRadius);
        foreach (var hitInfo in hitsInfo)
        {
            if (hitInfo.GetComponentInChildren<DamageableComponent>() != null)
            {
                Vector3 directionToEnemy = (hitInfo.transform.position - transform.position).normalized;
                RaycastHit hit;
                if (Physics.Raycast(transform.position, directionToEnemy, out hit, Data.ExplosionRadius))
                {
                    Debug.Log($"{hitInfo.gameObject.name} за препядствием");
                    continue;
                }
                DamageableComponent damageableComponent = hitInfo.GetComponent<DamageableComponent>();
                damageableComponent.TakeDamage(new FlatDamage(Data.ExplosionDamage));
                Debug.Log($"{hitInfo} Take {Data.ExplosionDamage} Damage");
                ExplosionEffect();
            }
        }
        Destroy(gameObject);
    }
    public void ActiveMineState()
    {
        ItsMineState = true;
    }
    protected virtual void ExplosionEffect()
    {
        //тут пишутся эффекты для разных гранат
    }
    protected void OnCollisionEnter(Collision collision)
    {
        if (!ItsMineState) return;
        if (collision.gameObject.tag == "Player") return;
        Debug.Log("collide " + gameObject + " With " + collision.gameObject.name);
        transform.SetParent(collision.transform);
        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }
}