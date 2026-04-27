using System.Collections;
using UnityEngine;

namespace Assets.Failsafe.Scripts.interaction_System
{
    public class ItemPlaceArea : Interactable
    {
        private IEnterable _station;
        private Item _itemInside = null;
        public bool IsEmpty => _station.IsEmpty();

        private void Awake()
        {
            _station = GetComponentInParent<IEnterable>();
        }
        protected override void Interact()
        {
            base.Interact();
        }

        public Transform TryGetItemPlace(Item item)
        {
            if (_station.IsRightType(item))
            {
                return this.transform;
            }
            else
                return null;
        }

        public void PutItemInside(Item item)
        {
            _itemInside = item;
            _station.OnEntered();
        }

        public Item TakeItem()
        {
            if (_station.IsEmpty())
                return null;
            Item return_item = _itemInside;
            _itemInside = null;
            _station.OnExited();
            return return_item;
        }
    }
}