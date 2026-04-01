using System.Collections;
using UnityEngine;

namespace Assets.Failsafe.Scripts.interaction_System
{
    public class ItemPlaceArea : Interactable
    {
        private IEnterable _station;

        private void Awake()
        {
            _station = GetComponentInParent<IEnterable>();
        }
        protected override void Interact()
        {
            base.Interact();
            if (_station.IsEmpty())
                _station.OnEntered();
            else
                _station.OnExited();
        }

        public Transform TryGetItemPlace(Item item)
        {
            if (_station.IsEmpty() && _station.IsRightType(item))
                return this.transform;
            else
                return null;
        }
    }
}