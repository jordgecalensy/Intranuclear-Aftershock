using UnityEngine;

[CreateAssetMenu(fileName = "ExplosiveObjectData", menuName = "ScriptableObjects/ExplosiveObject")]
public class ExplosiveObjectData : ScriptableObject
{
    [Header("Base explosin setting")]
    [SerializeField] private int _explosionDamage;
    [SerializeField] private float _explosionRadius;
    [SerializeField] private float _explosionForce;
    [Header("Explosin post effect")]
    [SerializeField] private float _durationPostEffect;
    [SerializeField] private GameObject _postEffect;
    [Header("On Eney Effect")]
    [SerializeField] private float _durationOnEnemyEffect;
    [SerializeField] private GameObject _onEnemyEffect;
    [Header("Explosin VFX effect")]
    [SerializeField] private float _durationVFX;
    [SerializeField] private GameObject _explsiveVFX;//можно сделать автоматически но андрей бяка

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
    public float DurationPostEffect
    {
        get
        {
            return _durationPostEffect;
        }
    }
    public GameObject PostEffect
    {
        get
        {
            return _postEffect;
        }
    }
    public float DurationOnEnemyEffect
    {
        get
        {
            return _durationOnEnemyEffect;
        }
    }
    public GameObject OnEnemyEffect
    {
        get
        {
            return _onEnemyEffect;
        }
    }
    public float DurationVFX
    {
        get
        {
            return _durationVFX;
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
