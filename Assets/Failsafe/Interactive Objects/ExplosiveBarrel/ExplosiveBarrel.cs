using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;

[RequireComponent(typeof(DamageableComponent))]
public class ExplosiveBarrel : ExplosiveObgect
{
    private bool _isExplosive = false;
    private void Start()
    {
        gameObject.GetComponent<DamageableComponent>().OnTakeDamage += ftp;
    }
    private void ftp(IDamage damage)
    {
        if (_isExplosive) return;
        _isExplosive = true;
        Explosion();
    }
    private void OnDestroy()
    {
        gameObject.GetComponent<DamageableComponent>().OnTakeDamage -= ftp;
    }
}
