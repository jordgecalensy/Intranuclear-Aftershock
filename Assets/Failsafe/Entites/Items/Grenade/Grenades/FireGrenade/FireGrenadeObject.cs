using UnityEngine;

public class FireGrenadeObject : GrеnadeObject
{
    protected override void SingleExplosionEffect()
    {
        var fire = Instantiate(Data.PostEffect, gameObject.transform.position, Quaternion.identity);
        Destroy(fire, Data.DurationPostEffect);
        base.SingleExplosionEffect();
    }
}
