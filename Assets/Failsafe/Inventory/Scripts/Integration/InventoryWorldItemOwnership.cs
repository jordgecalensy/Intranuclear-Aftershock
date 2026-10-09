using UnityEngine;

namespace Failsafe.Inventory.Integration
{
    [DisallowMultipleComponent]
    public sealed class InventoryWorldItemOwnership : MonoBehaviour
    {
        public bool IsInventoryOwned { get; private set; }
        public bool IsRuntimeGenerated { get; private set; }
        public string SourcePersistentId { get; private set; }

        private bool _originInitialized;

        public void Claim(
            string sourcePersistentId,
            bool runtimeGenerated)
        {
            if (!_originInitialized)
            {
                SourcePersistentId = sourcePersistentId?.Trim();
                IsRuntimeGenerated = runtimeGenerated;
                _originInitialized = true;
            }

            IsInventoryOwned = true;
        }

        public void Release()
        {
            IsInventoryOwned = false;
        }
    }
}
