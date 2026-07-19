using UnityEngine;

namespace Assets.Failsafe.Scripts.interaction_System
{
    [RequireComponent(typeof(BoxCollider))]
    public class CarryObjectPlaceArea : Interactable
    {
        [SerializeField] private Transform _placePoint;
        [SerializeField] private DoorScript _doorScript;
        [SerializeField] private CarryObjectPlaceArea[] _linkedPlaceAreas;

        private Item _placedItem;
        private BoxCollider _boxCollider;

        public bool IsEmpty => _placedItem == null && LinkedPlaceAreasAreEmpty();

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();

            if (_placePoint == null)
            {
                _placePoint = transform;
            }
        }

        public Transform TryGetItemPlace(Item item)
        {
            if (_doorScript != null && !_doorScript.IsPowered)
            {
                return null;
            }

            if (item.GetComponent<Item>().PropData.Name == "Card")
            {
                return this.transform;
            }
            else
                return null;
        }

        public void PutItemInside(Item item)
        {
            _placedItem = item;

            if (_placedItem == null)
            {
                return;
            }

            _placedItem.transform.SetParent(_placePoint, true);
            _placedItem.transform.SetPositionAndRotation(_placePoint.position, _placePoint.rotation);
            _placedItem.SetKinematic(true);

            BoxCollider boxCollider = _placedItem.GetComponentInChildren<BoxCollider>();
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }

            SetSlotColliderEnabled(false);
        }

        public void SetSlotColliderEnabled(bool isEnabled)
        {
            SetOwnSlotColliderEnabled(isEnabled);

            if (_linkedPlaceAreas == null)
            {
                return;
            }

            foreach (CarryObjectPlaceArea linkedPlaceArea in _linkedPlaceAreas)
            {
                if (linkedPlaceArea != null)
                {
                    linkedPlaceArea.SetOwnSlotColliderEnabled(isEnabled);
                }
            }
        }

        private void SetOwnSlotColliderEnabled(bool isEnabled)
        {
            if (_boxCollider == null)
            {
                _boxCollider = GetComponent<BoxCollider>();
            }

            if (_boxCollider != null)
            {
                _boxCollider.enabled = isEnabled;
            }
        }

        private bool LinkedPlaceAreasAreEmpty()
        {
            if (_linkedPlaceAreas == null)
            {
                return true;
            }

            foreach (CarryObjectPlaceArea linkedPlaceArea in _linkedPlaceAreas)
            {
                if (linkedPlaceArea != null && linkedPlaceArea._placedItem != null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
