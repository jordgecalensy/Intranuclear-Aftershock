using UnityEngine;
using VContainer;
using Failsafe.Player.UI;

namespace Failsafe.Player.Scripts.Interaction
{
    public class PlayerCrosshairRaycaster : MonoBehaviour
    {
        [Header("Дистанции")]
        [SerializeField] private float _interactRange = 5f;
        [SerializeField] private float _combatRange = 50f;

        [Header("Масштаб: КРУГ (Без предмета)")]
        [SerializeField] private float _circleNormalScale = 1.0f; 
        [SerializeField] private float _circleHoverScale = 1.2f;

        [Header("Масштаб: ПЕРЕКРЕСТИЕ (С предметом)")]
        [SerializeField] private float _crossNormalScale = 0.8f; 
        [SerializeField] private float _crossHoverScale = 1.1f;

        [Header("Маски и Слои")]
        [SerializeField] private LayerMask _interactMask;
        [SerializeField] private LayerMask _combatMask;
        [SerializeField] private int _interactableLayerIndex = 14;

        [Inject] private PlayerUIController _ui;
        [Inject] private PlayerHandsContainer _hands;

        private void Update()
        {
            bool hasItem = _hands.State == PlayerHandsContainer.HandState.ItemInHand;
            Ray ray = new Ray(transform.position, transform.forward);
            
            bool isInteractable = false;
            bool isConsole = false;
            bool isEnemy = false;

            // 1. Короткий рейкаст на предметы
            if (Physics.Raycast(ray, out RaycastHit interactHit, _interactRange, _interactMask))
            {
                isConsole = interactHit.collider.CompareTag("Console");
                isInteractable = interactHit.rigidbody != null || 
                                 interactHit.collider.GetComponent<Interactable>() != null ||
                                 interactHit.collider.gameObject.layer == _interactableLayerIndex;
            }

            // 2. Длинный рейкаст на врагов
            if (Physics.Raycast(ray, out RaycastHit combatHit, _combatRange, _combatMask))
            {
                if (combatHit.collider.CompareTag("Enemy")) isEnemy = true;
            }

            // ВЫБОР МАСШТАБА
            bool isHovering = isEnemy || isInteractable || isConsole;
            float targetScale;

            if (hasItem)
            {
                targetScale = isHovering ? _crossHoverScale : _crossNormalScale;
            }
            else
            {
                targetScale = isHovering ? _circleHoverScale : _circleNormalScale;
            }

            _ui.SetTargetScale(targetScale);
            _ui.UpdateCursorVisual(hasItem, isInteractable, isConsole, isEnemy);
        }
    }
}