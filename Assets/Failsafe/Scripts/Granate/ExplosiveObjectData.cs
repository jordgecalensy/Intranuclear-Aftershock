using UnityEngine;

[CreateAssetMenu(fileName = "ExplosiveObjectData", menuName = "ScriptableObjects/ExplosiveObject")]
public class ExplosiveObjectData : ScriptableObject
{
    [SerializeField] private int _explosionDamage;
    [SerializeField] private float _explosionRadius;
    [SerializeField] private float _explosionForce;

    [SerializeField] private float _lifeTimePostEffect;
    [SerializeField] private GameObject _postEffect;

    [SerializeField] private float _lifeTimeOnEnemyEffect;
    [SerializeField] private GameObject _onEnemyEffect;

    [SerializeField] private float _lifeTimeVFX;
    [SerializeField] private GameObject _explsiveVFX;

    public int ExplosionDamage
    {
        get
        {
            return _explosionDamage;
        }
    }
    public float ExplosionRadius
    {
        get
        {
            return _explosionRadius;
        }
    }
    public float ExplosionForce
    {
        get
        {
            return _explosionForce;
        }
    }
    public float LifeTimePostEffect
    {
        get
        {
            return _lifeTimePostEffect;
        }
    }
    public GameObject PostEffect
    {
        get
        {
            return _postEffect;
        }
    }
    public float LifeTimeOnEnemyEffect
    {
        get
        {
            return _lifeTimeOnEnemyEffect;
        }
    }
    public GameObject OnEnemyEffect
    {
        get
        {
            return _onEnemyEffect;
        }
    }
    public float LifeTimeVFX
    {
        get
        {
            return _lifeTimeVFX;
        }
    }
    public GameObject ExplosiveVFX
    {
        get
        {
            return _explsiveVFX;
        }
    }
}
