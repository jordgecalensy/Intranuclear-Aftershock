using UnityEngine;

[CreateAssetMenu(fileName = "GranadeData", menuName = "ScriptableObjects/Entities/Items/GranadeData")]
public class GranadeData : ScriptableObject
{
    [SerializeField] private GameObject _granadePref;

    [SerializeField] private float _throwForce;
    [SerializeField] private float _granadeTimer;
    [SerializeField] private int _explosionDamage;
    [SerializeField] private float _explosionRadius;
    [SerializeField] private float _explosionForce;
    [SerializeField] private float _startUseDelay;
    [SerializeField] private float _useDelay;

    public GameObject GranadePref 
    {
    get
        {
            return _granadePref; 
        } 
    }
    public float ThrowForce
    {
        get
        {
            return _throwForce;
        }
    }
    public float GranadeTimer 
    {
        get
        {
            return _granadeTimer; 
        } 
    }
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
    public float StartUseDelay
    {
        get
        {
            return _startUseDelay;
        }
    }
    public float UseDelay
    {
        get
        {
            return _useDelay;
        }
    }

}
