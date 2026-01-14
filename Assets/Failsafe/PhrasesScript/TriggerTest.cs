using UnityEngine;
using UnityEngine.Events;

namespace Failsafe.PhrasesScript
{
    public class Triggertest : MonoBehaviour
    {
        [SerializeField] private UnityEvent onTriggerEnter;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                onTriggerEnter?.Invoke();
            }
        }

    }
}