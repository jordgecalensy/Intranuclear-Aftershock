using System.Collections;
using UnityEngine;

public abstract class Thrower : MonoBehaviour
{
    [SerializeField] protected GameObject GranadePref;

    [SerializeField] private float _granadeTimer;
    [SerializeField] private float _throwForce;
    [SerializeField] private Transform _throwPoint;

    protected void Throw(bool ItAltUse)
    {
        GameObject Granade = Instantiate(GranadePref, _throwPoint.position, _throwPoint.rotation);
        Rigidbody rbGranade = Granade.GetComponent<Rigidbody>();
        rbGranade.AddForce(_throwPoint.forward *  _throwForce);
        if (!ItAltUse)
            StartCoroutine(ExplosionGranadeTimer(Granade));
        else
            Debug.Log("AltUse");
    }
    private IEnumerator ExplosionGranadeTimer(GameObject granade)
    {
        yield return new WaitForSeconds(_granadeTimer);
        Granade scriptGranade = granade.GetComponent<Granade>();
        if (scriptGranade != null)
            scriptGranade.Explosion();
    }
}