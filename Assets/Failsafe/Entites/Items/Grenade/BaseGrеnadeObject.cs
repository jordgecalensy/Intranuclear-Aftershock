using UnityEngine;
using System.Collections;

public class BaseGrеnadeObject : ExplosiveObject
{
    [SerializeField] protected GameObject MineTrigger;

    protected bool ItsMineState = false;
    protected bool InstaledMine = false;

    protected ThrowGrenadeData GranadeData;

    public void ActivesionGranade(ThrowGrenadeData granadeData, bool itsMineState)
    {
        GranadeData = granadeData;
        if (itsMineState)
        {
            ItsMineState = itsMineState;
        }
        else
        {
            StartCoroutine(ExplosionGranadeTimer());
            Debug.Log("Tik tak");
        }
    }
    protected IEnumerator ExplosionGranadeTimer()
    {
        yield return new WaitForSeconds(GranadeData.GrenadeTimer);
        Explosion();
    }
    protected void OnCollisionEnter(Collision collision)
    {
        if (!ItsMineState) return;
        if (collision.gameObject.tag == "Player") return;
        Debug.Log("collide " + gameObject + " With " + collision.gameObject.name);
        transform.SetParent(collision.transform);
        gameObject.GetComponent<Rigidbody>().isKinematic = true;
        gameObject.GetComponent<Collider>().enabled = false;
        MineTrigger.GetComponent<SphereCollider>().radius = GranadeData.MineTriggerRadius;
        MineTrigger.SetActive(true);
    }
}
