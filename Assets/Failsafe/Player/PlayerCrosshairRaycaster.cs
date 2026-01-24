using UnityEngine;
using VContainer;
using Failsafe.Player.UI;

namespace Failsafe.Player.Scripts.Interaction
{
    public class PlayerCrosshairRaycaster : MonoBehaviour
    {
        [SerializeField] private float _range = 5f;
        [SerializeField] private LayerMask _mask; // ОБЯЗАТЕЛЬНО: выбери слои объектов здесь! [cite: 23]
        [SerializeField] private int _interactableLayerIndex = 14; 
        
        [Inject] private PlayerUIController _ui;
        [Inject] private PlayerHandsContainer _hands; // Проверяем состояние рук [cite: 74]

        private void Update()
        {
            bool hasItem = _hands.State == PlayerHandsContainer.HandState.ItemInHand;
            
            Ray ray = new Ray(transform.position, transform.forward);
            bool isInteractable = false, isConsole = false, isEnemy = false;

            if (Physics.Raycast(ray, out RaycastHit hit, _range, _mask))
            {
                isConsole = hit.collider.CompareTag("Console");
                isEnemy = hit.collider.CompareTag("Enemy");
                // Интеракт если есть Rigidbody ИЛИ объект на спец. слое 
                isInteractable = hit.rigidbody != null || hit.collider.gameObject.layer == _interactableLayerIndex;
                
                // Debug для проверки: если в консоли пишет "Hit!", значит луч работает
                // Debug.Log($"Hit: {hit.collider.name} | Interactable: {isInteractable}");
            }

            _ui.UpdateCursorVisual(hasItem, isInteractable, isConsole, isEnemy);
        }
    }
}