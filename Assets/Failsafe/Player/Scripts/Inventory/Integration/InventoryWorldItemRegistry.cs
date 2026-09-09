using System;
using System.Collections.Generic;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;

namespace Failsafe.Inventory.Integration
{
    internal sealed class InventoryWorldItemRegistry
    {
        public int Count => _itemsByInstanceId.Count;

        private readonly Dictionary<string, Item> _itemsByInstanceId =
            new Dictionary<string, Item>(StringComparer.Ordinal);

        private GameObject _storageRoot;

        public bool TryInitialize(Transform owner, out string error)
        {
            if (owner == null)
            {
                error = "Inventory world-item owner is not assigned.";
                return false;
            }

            _storageRoot = new GameObject(
                "Inventory Stored World Items");

            _storageRoot.transform.SetParent(owner, false);
            _storageRoot.SetActive(false);
            error = null;
            return true;
        }

        public bool TryGet(string instanceId, out Item worldItem)
        {
            worldItem = null;

            return !string.IsNullOrWhiteSpace(instanceId) &&
                   _itemsByInstanceId.TryGetValue(
                       instanceId,
                       out worldItem) &&
                   worldItem != null;
        }

        public bool TryGetInstanceId(
            Item worldItem,
            out string instanceId)
        {
            instanceId = null;

            if (worldItem == null)
                return false;

            foreach (KeyValuePair<string, Item> pair in _itemsByInstanceId)
            {
                if (pair.Value != worldItem)
                    continue;

                instanceId = pair.Key;
                return true;
            }

            return false;
        }

        public void Register(
            string instanceId,
            Item worldItem,
            bool runtimeGenerated)
        {
            _itemsByInstanceId[instanceId] = worldItem;

            Claim(
                worldItem,
                GetSourcePersistentId(worldItem, runtimeGenerated),
                runtimeGenerated);
        }

        public void AddRestored(
            string instanceId,
            Item worldItem,
            string sourcePersistentId,
            bool runtimeGenerated)
        {
            _itemsByInstanceId.Add(instanceId, worldItem);
            Claim(worldItem, sourcePersistentId, runtimeGenerated);
            worldItem.gameObject.SetActive(true);
            Store(worldItem);
        }

        public void Replace(string instanceId, Item replacement)
        {
            _itemsByInstanceId[instanceId] = replacement;
        }

        public bool Remove(string instanceId, out Item worldItem)
        {
            worldItem = null;

            if (string.IsNullOrWhiteSpace(instanceId) ||
                !_itemsByInstanceId.TryGetValue(
                    instanceId,
                    out worldItem))
            {
                return false;
            }

            _itemsByInstanceId.Remove(instanceId);
            return worldItem != null;
        }

        public bool TryEnsureStorage(out string error)
        {
            if (_storageRoot != null)
            {
                error = null;
                return true;
            }

            error = "Inventory world-item storage is not initialized.";
            return false;
        }

        public bool TryMoveToStorage(
            string instanceId,
            out string error)
        {
            if (!TryEnsureStorage(out error))
                return false;

            if (!TryGet(instanceId, out Item worldItem))
            {
                error =
                    $"No world item is registered for inventory item " +
                    $"'{instanceId}'.";

                return false;
            }

            Store(worldItem);
            error = null;
            return true;
        }

        public bool TryCreateStoredStackRepresentative(
            ItemData itemData,
            out Item worldItem,
            out string error)
        {
            worldItem = null;

            if (!TryEnsureStorage(out error))
                return false;

            if (itemData == null || itemData.WorldItemPrefab == null)
            {
                error = "Stacked inventory item has no World Item Prefab.";
                return false;
            }

            worldItem = UnityEngine.Object.Instantiate(
                itemData.WorldItemPrefab);

            worldItem.name =
                $"{itemData.WorldItemPrefab.name} (Stack Representative)";

            if (!HasMatchingDefinition(worldItem, itemData))
            {
                error =
                    $"World Item Prefab '{itemData.WorldItemPrefab.name}' " +
                    $"does not use ItemData '{itemData.name}'.";

                Destroy(worldItem);
                worldItem = null;
                return false;
            }

            Claim(
                worldItem,
                sourcePersistentId: null,
                runtimeGenerated: true);

            Store(worldItem);
            error = null;
            return true;
        }

        public void Store(Item worldItem)
        {
            worldItem.ToInventoryState();
            worldItem.transform.SetParent(
                _storageRoot.transform,
                true);
        }

        public void ReleaseToWorldIfStored(Item worldItem)
        {
            Release(worldItem);

            if (_storageRoot == null ||
                !worldItem.transform.IsChildOf(
                    _storageRoot.transform))
            {
                return;
            }

            worldItem.transform.SetParent(null, true);
            worldItem.ToWorldState();
        }

        public void RetireMerged(Item worldItem)
        {
            if (worldItem == null)
                return;

            InventoryWorldItemOwnership ownership =
                worldItem.GetComponent<InventoryWorldItemOwnership>();

            bool preservePersistentSource =
                ownership != null &&
                !ownership.IsRuntimeGenerated &&
                !string.IsNullOrWhiteSpace(
                    ownership.SourcePersistentId);

            if (!preservePersistentSource)
            {
                Destroy(worldItem);
                return;
            }

            ownership.Release();
            worldItem.transform.SetParent(null, true);
            worldItem.ToWorldState();
            worldItem.gameObject.SetActive(false);
        }

        public void ReleaseOrDestroyForRestore(Item worldItem)
        {
            InventoryWorldItemOwnership ownership =
                worldItem.GetComponent<InventoryWorldItemOwnership>();

            if (ownership != null &&
                !ownership.IsRuntimeGenerated &&
                !string.IsNullOrWhiteSpace(
                    ownership.SourcePersistentId))
            {
                ownership.Release();
                worldItem.transform.SetParent(null, true);
                worldItem.gameObject.SetActive(true);
                worldItem.ToWorldState();
                return;
            }

            Destroy(worldItem);
        }

        public void Clear()
        {
            _itemsByInstanceId.Clear();
        }

        public void Dispose()
        {
            _itemsByInstanceId.Clear();
            DestroyUnityObject(_storageRoot);
            _storageRoot = null;
        }

        public static bool HasMatchingDefinition(
            Item worldItem,
            ItemData itemData)
        {
            return worldItem != null &&
                   worldItem.ItemData != null &&
                   itemData != null &&
                   string.Equals(
                       worldItem.ItemData.InventoryDefinitionId?.Trim(),
                       itemData.InventoryDefinitionId?.Trim(),
                       StringComparison.Ordinal);
        }

        public static void Claim(
            Item worldItem,
            bool runtimeGenerated)
        {
            Claim(
                worldItem,
                GetSourcePersistentId(worldItem, runtimeGenerated),
                runtimeGenerated);
        }

        public static void Claim(
            Item worldItem,
            string sourcePersistentId,
            bool runtimeGenerated)
        {
            InventoryWorldItemOwnership ownership =
                worldItem.GetComponent<InventoryWorldItemOwnership>();

            if (ownership == null)
            {
                ownership = worldItem.gameObject
                    .AddComponent<InventoryWorldItemOwnership>();
            }

            ownership.Claim(sourcePersistentId, runtimeGenerated);
        }

        public static void Release(Item worldItem)
        {
            if (worldItem == null)
                return;

            InventoryWorldItemOwnership ownership =
                worldItem.GetComponent<InventoryWorldItemOwnership>();

            ownership?.Release();
        }

        public static void Destroy(Item worldItem)
        {
            if (worldItem != null)
                DestroyUnityObject(worldItem.gameObject);
        }

        private static string GetSourcePersistentId(
            Item worldItem,
            bool runtimeGenerated)
        {
            if (runtimeGenerated || worldItem == null)
                return null;

            RunPersistentObject persistentObject =
                worldItem.GetComponent<RunPersistentObject>();

            return persistentObject != null
                ? persistentObject.PersistentId
                : null;
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
