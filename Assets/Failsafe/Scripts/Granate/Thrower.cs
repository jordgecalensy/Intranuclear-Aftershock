using System.Collections;
using UnityEngine;

public abstract class Thrower : MonoBehaviour
{
    [SerializeField] protected GranadeData Data;

    //private Transform _throwPoint;

    protected void Throw(bool ItAltUse)
    {
        GameObject Granade = Instantiate(Data.GranadePref, Camera.main.transform.position, Camera.main.transform.rotation);
        Rigidbody rbGranade = Granade.GetComponent<Rigidbody>();
        Granade scriptGranade = Granade.GetComponent<Granade>();
        rbGranade.AddForce(Camera.main.transform.forward *  Data.ThrowForce);
        if (!ItAltUse)
            StartCoroutine(ExplosionGranadeTimer(scriptGranade));
        else
            scriptGranade.ActiveMineState();
    }
    private IEnumerator ExplosionGranadeTimer(Granade scriptGranade)
    {
        yield return new WaitForSeconds(Data.GranadeTimer);
        if (scriptGranade != null)
            scriptGranade.Explosion();
    }
}