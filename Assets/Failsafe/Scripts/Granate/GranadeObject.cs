using UnityEngine;
using System.Collections;

public class GranadeObject : ExplosiveObgect
{
    public void ActivesionGranade(ThrowGranadeData granadeData)
    {
        StartCoroutine(ExplosionGranadeTimer(granadeData));
        Debug.Log("Tik tak");
    }
    protected IEnumerator ExplosionGranadeTimer(ThrowGranadeData granadeData)
    {
        yield return new WaitForSeconds(granadeData.GranadeTimer);
        Explosion();
    }
}
