using UnityEngine;

public class MineTrigger : MonoBehaviour
{
    [SerializeField] private GrеnadeObject _granade;

    private bool _itsTriggerActivated = false;
    private void OnTriggerStay(Collider other)
    {
        if (_itsTriggerActivated) return;
        if (other.gameObject.tag == "Player" || other.gameObject.tag == "Enemy")
        {
            Vector3 directionToEnemy = (other.transform.position - transform.position).normalized;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToEnemy, out hit))
            {
                _itsTriggerActivated = true;
                Debug.Log("trig " + other.name);
                _granade.Explosion();
            }
        }
    }
}
