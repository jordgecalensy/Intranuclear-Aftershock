using UnityEngine;

[CreateAssetMenu(fileName = "ExplosiveObjectData", menuName = "ScriptableObjects/ExplosiveObject")]
public class ExplosiveObjectData : ScriptableObject
{
    [SerializeField] private int _explosionDamage;
    [SerializeField] private float _explosionRadius;
    [SerializeField] private float _explosionForce;

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
}
