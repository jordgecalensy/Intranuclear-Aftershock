using UnityEngine;
using System.Collections;
using FMODUnity;

public class BaseGrеnadeObject : ExplosiveObject
{
    [SerializeField] protected GameObject MineTrigger;

    protected bool ItsMineState = false;
    protected bool InstaledMine = false;

    protected ThrowGrenadeData GrenadeData;

    public void ActivesionGranade(ThrowGrenadeData granadeData, bool itsMineState)
    {
        GrenadeData = granadeData;
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
        yield return new WaitForSeconds(GrenadeData.GrenadeTimer);
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
        MineTrigger.GetComponent<SphereCollider>().radius = GrenadeData.MineTriggerRadius;
        SoundUtils3D.Play(gameObject, GrenadeData.MinePinPull);
        MineTrigger.SetActive(true);
        MineTrigger.GetComponent<MineTrigger>().ActivateMineIndicationSfx(GrenadeData.MineIndication);
    }
}
