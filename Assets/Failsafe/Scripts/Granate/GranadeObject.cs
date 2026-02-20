using UnityEngine;
using System.Collections;

public class GranadeObject : ExplosiveObgect
{
    [SerializeField] protected GameObject MineTrigger;

    protected bool ItsMineState = false;
    protected bool InstaledMine = false;

    public void ActivesionGranade(ThrowGranadeData granadeData, bool itsMineState)
    {
        if (itsMineState)
        {
            ItsMineState = itsMineState;
        }
        else
        {
            StartCoroutine(ExplosionGranadeTimer(granadeData));
            Debug.Log("Tik tak");
        }
    }
    protected IEnumerator ExplosionGranadeTimer(ThrowGranadeData granadeData)
    {
        yield return new WaitForSeconds(granadeData.GranadeTimer);
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
        MineTrigger.SetActive(true);
    }
}
