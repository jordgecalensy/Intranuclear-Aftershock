using UnityEngine;
using System.Collections;

public class GranadeObject : ExplosiveObgect
{
    public void ActivesionGranade(GranadeData granadeData)
    {
        Debug.Log("Tik tak");
        StartCoroutine(ExplosionGranadeTimer(granadeData));
    }
    private IEnumerator ExplosionGranadeTimer(GranadeData granadeData)
    {
        yield return new WaitForSeconds(granadeData.GranadeTimer);
        Explosion();
    }
}
