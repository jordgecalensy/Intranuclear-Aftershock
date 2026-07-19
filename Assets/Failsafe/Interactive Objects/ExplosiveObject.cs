using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.EffectSystem;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public abstract class ExplosiveObject : MonoBehaviour
{
    [SerializeField] protected ExplosiveObjectData Data;
    protected List<GameObject> DamagedObjects = new List<GameObject>();

    private IEffectApplicationService _effects;

    [Inject]
    public void Construct(IEffectApplicationService effects)
    {
        _effects = effects;
    }

    public void Explosion()
    {
        if (Data == null)
        {
            Destroy(gameObject);
            return;
        }

        Collider[] hitsInfo = Physics.OverlapSphere(transform.position, Data.ExplosionRadius);
        DamagedObjects.Clear();
        EffectBundle explosionEffects = ResolveExplosionEffects();
        bool useEffectBundle = explosionEffects != null && ResolveEffectsIfNeeded();

        foreach (var hitInfo in hitsInfo)
        {
            Vector3 directionToEnemy = (hitInfo.transform.position - transform.position).normalized;
            RaycastHit hit;          
            if (Physics.Raycast(transform.position, directionToEnemy, out hit, Data.ExplosionRadius))
            {
                if (HitsChecking(hit, hitInfo)) continue;

                if (useEffectBundle)
                    BundleExplosionEffect(explosionEffects, hitInfo, directionToEnemy);
                else
                {
                    DamagebleExplosionEffect(hitInfo);
                    PhysicsExplosionEffect(hitInfo, directionToEnemy);
                }

            }
        }
        SingleExplosionEffect();
        Destroy(gameObject);
    }
    protected virtual bool HitsChecking(RaycastHit hit, Collider hitInfo)
    {
        if (!Data.IgnoreCollision)
        {
            if (hitInfo.name != hit.collider.name)
            {
                if (hitInfo.tag == "Player" || hitInfo.tag == "Enemy")
                    Debug.Log($"{hitInfo.gameObject.name} за препядствием {hit.collider.name}");
                return true;
            }
            return false;
        }
        else
        {
            if (hitInfo.tag != "Enemy")
            {
                return true;
            }
            Debug.Log($"{hitInfo.gameObject.name}");
            return false; 
        }
    }
    protected virtual void DamagebleExplosionEffect(Collider hitInfo)
    {
        DamageableComponent damageableComponent = hitInfo.GetComponentInParent<DamageableComponent>();
        if (damageableComponent == null || DamagedObjects.Contains(damageableComponent.gameObject)) return;
        damageableComponent.TakeDamage(new FlatDamage(Data.ExplosionDamage));
        Debug.Log($"{hitInfo.name} Take {Data.ExplosionDamage} Damage");
        DamagedObjects.Add(damageableComponent.gameObject);
    }
    protected virtual void PhysicsExplosionEffect(Collider hitInfo, Vector3 directionToEnemy)
    {
        if (hitInfo.GetComponent<Rigidbody>() == null) return;
        Debug.Log($"Rigidbody: {hitInfo.name}");
        hitInfo.GetComponent<Rigidbody>().AddForce(directionToEnemy * Data.ExplosionForce, ForceMode.Impulse);
    }

    protected virtual EffectBundle ResolveExplosionEffects()
    {
        return Data != null ? Data.ExplosionEffects : null;
    }

    protected virtual void OnBundleExplosionEffectApplied(Collider hitInfo, Vector3 directionToEnemy)
    {
    }

    private void BundleExplosionEffect(
        EffectBundle explosionEffects,
        Collider hitInfo,
        Vector3 directionToEnemy)
    {
        if (explosionEffects == null || hitInfo == null)
            return;

        GameObject target = ResolveExplosionTarget(hitInfo);

        if (target != null && DamagedObjects.Contains(target))
            return;

        Vector3 point = hitInfo.ClosestPoint(transform.position);
        Vector3 direction = directionToEnemy.sqrMagnitude > 0.0001f
            ? directionToEnemy.normalized
            : (hitInfo.transform.position - transform.position).normalized;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.up;

        float radius = Mathf.Max(0.01f, Data.ExplosionRadius);
        float distance = Vector3.Distance(transform.position, point);
        float power = Mathf.Clamp01(1f - distance / radius);

        var context = new EffectContext(
            gameObject,
            hitInfo,
            point,
            -direction,
            direction,
            power);

        _effects.Apply(explosionEffects, context);

        if (target != null)
            DamagedObjects.Add(target);

        OnBundleExplosionEffectApplied(hitInfo, direction);
    }

    private static GameObject ResolveExplosionTarget(Collider hitInfo)
    {
        if (hitInfo == null)
            return null;

        DamageableComponent damageableComponent = hitInfo.GetComponentInParent<DamageableComponent>();

        if (damageableComponent != null)
            return damageableComponent.gameObject;

        if (hitInfo.attachedRigidbody != null)
            return hitInfo.attachedRigidbody.gameObject;

        return hitInfo.transform.root != null
            ? hitInfo.transform.root.gameObject
            : hitInfo.gameObject;
    }

    private bool ResolveEffectsIfNeeded()
    {
        if (_effects != null)
            return true;

        LifetimeScope scope = GetComponentInParent<LifetimeScope>();

        if (scope == null && gameObject.scene.IsValid())
            scope = LifetimeScope.Find<LifetimeScope>(gameObject.scene);

        if (scope == null || scope.Container == null)
            return false;

        try
        {
            _effects = scope.Container.Resolve<IEffectApplicationService>();
        }
        catch
        {
            _effects = null;
        }

        return _effects != null;
    }

    protected virtual void SingleExplosionEffect()
    {
        if (Data.PostEffects != null)
        {
            foreach (var postEffect in Data.PostEffects)
            {
                var fire = Instantiate(postEffect.Effect, gameObject.transform.position, Quaternion.identity);
                Destroy(fire, postEffect.Duration);
            }
        }
        if (Data.ExplosiveVfx != null)
        {
            foreach (var vfx in Data.ExplosiveVfx)
            {
                var Vfx = Instantiate(vfx.Effect, gameObject.transform.position, Quaternion.identity);
                Destroy(Vfx, vfx.Duration);
                // визуалный эффект после взрыва гранаты
            }
        }
        if (Data.ExplsiveSFXEvent.Guid != System.Guid.Empty)
            SoundUtils3D.Play(gameObject, Data.ExplsiveSFXEvent);
    }
}
