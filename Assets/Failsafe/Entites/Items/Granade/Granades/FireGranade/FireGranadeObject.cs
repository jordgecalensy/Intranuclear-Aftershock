using UnityEngine;

public class FireGranadeObject : GranadeObject
{
    protected override void SingleExplosionEffect()
    {
        var fire = Instantiate(Data.PostEffect, gameObject.transform.position, Quaternion.identity);
        Destroy(fire, Data.DurationPostEffect);
        base.SingleExplosionEffect();
    }
}
