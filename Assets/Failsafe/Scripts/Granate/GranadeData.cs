using UnityEngine;

[CreateAssetMenu(fileName = "New GranadeData", menuName = "Granade Data", order = 51)]
public class GranadeData : ScriptableObject
{
    [SerializeField] private GameObject _granadePref;

    [SerializeField] private float _throwForce;
    [SerializeField] private float _granadeTimer;
    [SerializeField] private int _explosionDamage;
    [SerializeField] private float _explosionRadius;

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

}
