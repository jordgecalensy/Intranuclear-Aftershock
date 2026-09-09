using System.Collections.Generic;
using System.Reflection;
using Failsafe.Inventory.Core;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Inventory.Integration.Tests
{
    [TestFixture]
    public sealed class InventoryRuntimeControllerTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                Object target = _objects[index];

                if (target != null)
                    Object.DestroyImmediate(target);
            }

            _objects.Clear();
        }

        [Test]
        public void Initialize_CreatesPublicRuntimeParts()
        {
            InventoryRuntimeController controller = CreateController();

            Assert.That(
                controller.TryInitialize(out string error),
                Is.True,
                error);
            Assert.That(controller.IsInitialized, Is.True);
            Assert.That(controller.Grid, Is.Not.Null);
            Assert.That(controller.QuickSlots, Is.Not.Null);
            Assert.That(controller.Presenter, Is.Not.Null);
            Assert.That(controller.Presenter.IsInitialized, Is.True);
            Assert.That(controller.QuickBarPresenter, Is.Not.Null);
            Assert.That(
                controller.QuickBarPresenter.IsInitialized,
                Is.True);
            Assert.That(controller.RegisteredWorldItemCount, Is.Zero);
        }

        [Test]
        public void PresentationVisibility_UpdatesGridAndQuickBar()
        {
            InventoryRuntimeController controller =
                CreateInitializedController();

            Assert.That(controller.SetPresentationVisible(false), Is.True);
            Assert.That(controller.IsPresentationVisible, Is.False);
            Assert.That(
                controller.QuickBarPresenter.IsInventoryOpen,
                Is.False);

            Assert.That(controller.SetPresentationVisible(true), Is.True);
            Assert.That(controller.IsPresentationVisible, Is.True);
            Assert.That(
                controller.QuickBarPresenter.IsInventoryOpen,
                Is.True);
        }

        [Test]
        public void Initialize_DuplicateDefinitionIdsAreRejected()
        {
            ItemData first = CreateItemData("shared-definition");
            ItemData second = CreateItemData("shared-definition");
            InventoryRuntimeController controller = CreateController(
                new[] { first, second });

            Assert.That(
                controller.TryInitialize(out string error),
                Is.False);
            Assert.That(error, Does.Contain("shared-definition"));
            Assert.That(controller.IsInitialized, Is.False);
            Assert.That(controller.Grid, Is.Null);
            Assert.That(controller.Presenter, Is.Null);
        }

        [Test]
        public void StoreAndDetachWorldItem_PreservesOwnershipBoundary()
        {
            ItemData itemData = CreateItemData("tool");
            Item worldItem = CreateWorldItem("Tool", itemData);
            InventoryRuntimeController controller =
                CreateInitializedController();

            InventoryOperationResult storeResult =
                controller.StoreWorldItem(
                    worldItem,
                    out string instanceId,
                    out string storeError);

            Assert.That(storeResult.IsSuccess, Is.True, storeError);
            Assert.That(controller.RegisteredWorldItemCount, Is.EqualTo(1));
            Assert.That(
                controller.TryGetWorldItem(instanceId, out Item registered),
                Is.True);
            Assert.That(registered, Is.SameAs(worldItem));

            InventoryWorldItemOwnership ownership =
                worldItem.GetComponent<InventoryWorldItemOwnership>();

            Assert.That(ownership, Is.Not.Null);
            Assert.That(ownership.IsInventoryOwned, Is.True);
            Assert.That(worldItem.gameObject.activeInHierarchy, Is.False);

            InventoryOperationResult detachResult =
                controller.DetachWorldItem(
                    instanceId,
                    out Item detached,
                    out string detachError);

            Assert.That(detachResult.IsSuccess, Is.True, detachError);
            Assert.That(detached, Is.SameAs(worldItem));
            Assert.That(ownership.IsInventoryOwned, Is.False);
            Assert.That(worldItem.transform.parent, Is.Null);
            Assert.That(worldItem.gameObject.activeInHierarchy, Is.True);
            Assert.That(controller.RegisteredWorldItemCount, Is.Zero);
        }

        [Test]
        public void StackedDetach_ReplacesStoredRepresentative()
        {
            ItemData itemData = CreateItemData(
                "stacked-tool",
                maximumStack: 2,
                createWorldPrefab: true);
            Item first = CreateWorldItem("First Tool", itemData);
            Item second = CreateWorldItem("Second Tool", itemData);
            InventoryRuntimeController controller =
                CreateInitializedController();

            Assert.That(
                controller.StoreWorldItem(
                    first,
                    out string instanceId,
                    out string firstError).IsSuccess,
                Is.True,
                firstError);
            Assert.That(
                controller.StoreWorldItem(
                    second,
                    out string stackedInstanceId,
                    out string secondError).IsSuccess,
                Is.True,
                secondError);
            Assert.That(stackedInstanceId, Is.EqualTo(instanceId));
            Assert.That(second == null, Is.True);

            Assert.That(
                controller.Grid.TryGetItem(
                    instanceId,
                    out InventoryItemModel stackedItem),
                Is.True);
            Assert.That(stackedItem.Quantity, Is.EqualTo(2));

            InventoryOperationResult detachResult =
                controller.DetachWorldItem(
                    instanceId,
                    out Item detached,
                    out string detachError);

            Assert.That(detachResult.IsSuccess, Is.True, detachError);
            Assert.That(detached, Is.SameAs(first));
            Assert.That(
                controller.TryGetWorldItem(
                    instanceId,
                    out Item replacement),
                Is.True);
            Assert.That(replacement, Is.Not.SameAs(first));
            Assert.That(stackedItem.Quantity, Is.EqualTo(1));
            Assert.That(controller.RegisteredWorldItemCount, Is.EqualTo(1));
        }

        [Test]
        public void ClearForRestore_DestroysRuntimeGeneratedWorldItem()
        {
            ItemData itemData = CreateItemData(
                "runtime-tool",
                createWorldPrefab: true);
            InventoryRuntimeController controller =
                CreateInitializedController();

            InventoryOperationResult createResult =
                controller.CreateAndStoreRuntimeItem(
                    itemData,
                    out string instanceId,
                    out string createError);

            Assert.That(createResult.IsSuccess, Is.True, createError);
            Assert.That(
                controller.TryGetWorldItem(instanceId, out Item runtimeItem),
                Is.True);

            Assert.That(
                controller.TryClearForRestore(out string clearError),
                Is.True,
                clearError);
            Assert.That(runtimeItem == null, Is.True);
            Assert.That(controller.RegisteredWorldItemCount, Is.Zero);
            Assert.That(controller.Grid.Placements, Is.Empty);
        }

        private InventoryRuntimeController CreateInitializedController()
        {
            InventoryRuntimeController controller = CreateController();

            Assert.That(
                controller.TryInitialize(out string error),
                Is.True,
                error);

            return controller;
        }

        private InventoryRuntimeController CreateController(
            ItemData[] itemCatalog = null)
        {
            GameObject root = Track(new GameObject("Inventory Runtime Test"));
            root.SetActive(false);

            InventoryRuntimeController controller =
                root.AddComponent<InventoryRuntimeController>();

            SetPrivateField(controller, "_initializeOnAwake", false);

            if (itemCatalog != null)
                SetPrivateField(controller, "_itemCatalog", itemCatalog);

            root.SetActive(true);
            return controller;
        }

        private ItemData CreateItemData(
            string definitionId,
            int maximumStack = 1,
            bool createWorldPrefab = false)
        {
            ItemData itemData = Track(
                ScriptableObject.CreateInstance<ItemData>());

            itemData.name = $"Item Data {definitionId}";
            itemData.InventoryDefinitionId = definitionId;
            itemData.InventoryWidth = 1;
            itemData.InventoryHeight = 1;
            itemData.InventoryMaxStack = maximumStack;
            itemData.InventoryModelScaleMultiplier = 1f;
            itemData.InventoryModelFitPadding = 0.08f;
            itemData.InventoryModelMaxDepthInCells = 0.75f;
            itemData.InventoryModelPrefab = Track(
                GameObject.CreatePrimitive(PrimitiveType.Cube));
            itemData.InventoryModelPrefab.name =
                $"{definitionId} Inventory Model";

            if (createWorldPrefab)
            {
                itemData.WorldItemPrefab = CreateWorldItem(
                    $"{definitionId} World Prefab",
                    itemData);
            }

            return itemData;
        }

        private Item CreateWorldItem(string name, ItemData itemData)
        {
            GameObject gameObject = Track(new GameObject(name));
            gameObject.SetActive(false);
            gameObject.AddComponent<BoxCollider>();
            Item item = gameObject.AddComponent<Item>();
            item.ItemData = itemData;
            gameObject.SetActive(true);
            return item;
        }

        private static void SetPrivateField<T>(
            InventoryRuntimeController controller,
            string fieldName,
            T value)
        {
            FieldInfo field = typeof(InventoryRuntimeController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(controller, value);
        }

        private T Track<T>(T target) where T : Object
        {
            _objects.Add(target);
            return target;
        }
    }
}
