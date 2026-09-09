using Assets.Failsafe.Scripts.RandomGeneration;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Integration;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    public sealed class SelectedEngineerStartingItemGranter : IStartable
    {
        private readonly EngineerSelectionState _selectionState;
        private readonly InventoryRuntimeController _inventory;

        public SelectedEngineerStartingItemGranter(
            EngineerSelectionState selectionState,
            InventoryRuntimeController inventory)
        {
            _selectionState = selectionState;
            _inventory = inventory;
        }

        public void Start()
        {
            EngineerBuild selectedEngineer =
                _selectionState?.SelectedEngineer;

            if (selectedEngineer?.Perks == null || _inventory == null)
                return;

            if (!_selectionState.TryClaimStartingItemsGrant())
                return;

            int grantedCount = 0;

            for (int perkIndex = 0;
                 perkIndex < selectedEngineer.Perks.Count;
                 perkIndex++)
            {
                PerkDefinition perk =
                    selectedEngineer.Perks[perkIndex]?.Definition;
                PerkStartingItemGrant[] grants = perk?.StartingItems;

                if (grants == null)
                    continue;

                for (int grantIndex = 0;
                     grantIndex < grants.Length;
                     grantIndex++)
                {
                    PerkStartingItemGrant grant = grants[grantIndex];

                    if (grant?.Item == null)
                    {
                        EffectLog.Error(EffectLog.Bundle,
                            $"[StartingItems] Perk '{perk.Id}' has an " +
                            $"empty starting item at index {grantIndex}.",
                            perk);
                        continue;
                    }

                    for (int itemIndex = 0;
                         itemIndex < grant.Quantity;
                         itemIndex++)
                    {
                        InventoryOperationResult result =
                            _inventory.CreateAndStoreRuntimeItem(
                                grant.Item,
                                out _,
                                out string error);

                        if (result.IsSuccess)
                        {
                            grantedCount++;
                            continue;
                        }

                        EffectLog.Error(EffectLog.Bundle,
                            $"[StartingItems] Could not grant " +
                            $"'{grant.Item.name}' from perk '{perk.Id}': " +
                            $"{error}",
                            _inventory);
                    }
                }
            }

            if (grantedCount > 0)
            {
                EffectLog.Info(EffectLog.Bundle,
                    $"[StartingItems] Granted {grantedCount} item(s) " +
                    $"to engineer '{selectedEngineer.Name}'.",
                    _inventory);
            }
        }
    }
}
