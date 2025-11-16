using UnityEngine;

public class MineTrigger : MonoBehaviour
{
    [SerializeField] private Granade _granade;
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "Player" || other.gameObject.tag == "Enemy")
        {
            Vector3 directionToEnemy = (other.transform.position - transform.position).normalized;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToEnemy, out hit))
            {
                Debug.Log("trig" + other.name);
                _granade.Explosion();
            }
        }
    }
}
