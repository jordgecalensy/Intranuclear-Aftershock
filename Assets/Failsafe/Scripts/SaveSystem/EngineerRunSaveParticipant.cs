using System;
using System.Collections.Generic;
using Assets.Failsafe.Scripts.RandomGeneration;
using Cysharp.Threading.Tasks;
using Failsafe.Scripts.Configs;
using Failsafe.Scripts.EffectSystem;
using VContainer.Unity;

namespace Failsafe.Scripts.SaveSystem
{
    public sealed class EngineerRunSaveParticipant :
        IRunSaveParticipant,
        IInitializable,
        IDisposable
    {
        private const int EngineerRestoreOrder = 100;

        private readonly EngineerSelectionState _selectionState;
        private readonly GameConfig _gameConfig;
        private readonly RunSaveParticipantRegistry _participantRegistry;

        private IDisposable _registration;

        public string ParticipantId => RunSaveParticipantIds.Engineer;
        public int RestoreOrder => EngineerRestoreOrder;

        public EngineerRunSaveParticipant(
            EngineerSelectionState selectionState,
            GameConfig gameConfig,
            RunSaveParticipantRegistry participantRegistry)
        {
            _selectionState = selectionState ??
                throw new ArgumentNullException(nameof(selectionState));
            _gameConfig = gameConfig ??
                throw new ArgumentNullException(nameof(gameConfig));
            _participantRegistry = participantRegistry ??
                throw new ArgumentNullException(nameof(participantRegistry));
        }

        public void Initialize()
        {
            _registration = _participantRegistry.Register(this);
        }

        public void Dispose()
        {
            _registration?.Dispose();
            _registration = null;
        }

        public void Capture(RunCheckpointData checkpoint)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            EngineerBuild selectedEngineer = _selectionState.SelectedEngineer;
            checkpoint.engineer = selectedEngineer == null
                ? new EngineerStateData()
                : CreateSavedState(selectedEngineer);
        }

        public UniTask RestoreAsync(
            RunCheckpointData checkpoint,
            RunLoadContext context)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!TryRestoreBeforeSceneLoad(
                    checkpoint.engineer,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }

            return UniTask.CompletedTask;
        }

        public bool TryRestoreBeforeSceneLoad(
            EngineerStateData savedState,
            out string error)
        {
            if (savedState == null || !savedState.hasState)
            {
                _selectionState.Clear();
                error = null;
                return true;
            }

            EngineerGenerationConfig generationConfig =
                _gameConfig.EngineerGenerationConfig;

            if (generationConfig == null)
            {
                error = "EngineerGenerationConfig is not assigned in GameConfig.";
                return false;
            }

            if (!TryCreateEngineerBuild(
                    savedState,
                    generationConfig,
                    out EngineerBuild engineer,
                    out error))
            {
                return false;
            }

            _selectionState.RestoreSelectedEngineer(engineer);
            return true;
        }

        private static EngineerStateData CreateSavedState(
            EngineerBuild selectedEngineer)
        {
            var savedState = new EngineerStateData
            {
                hasState = true,
                name = selectedEngineer.Name,
                operatorCode = selectedEngineer.OperatorCode,
                totalWeight = selectedEngineer.TotalWeight,
                spentWeight = selectedEngineer.SpentWeight
            };

            if (selectedEngineer.Perks == null ||
                selectedEngineer.Perks.Count == 0)
            {
                throw new InvalidOperationException(
                    "The selected engineer has no perks to save.");
            }

            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);

            for (int perkIndex = 0;
                 perkIndex < selectedEngineer.Perks.Count;
                 perkIndex++)
            {
                string perkId =
                    selectedEngineer.Perks[perkIndex]?.Definition?.Id;

                if (string.IsNullOrWhiteSpace(perkId))
                {
                    throw new InvalidOperationException(
                        $"The selected engineer perk at index {perkIndex} " +
                        "has no stable ID.");
                }

                if (!uniqueIds.Add(perkId))
                {
                    throw new InvalidOperationException(
                        $"The selected engineer contains duplicate perk ID " +
                        $"'{perkId}'.");
                }

                savedState.perkIds.Add(perkId);
            }

            return savedState;
        }

        private static bool TryCreateEngineerBuild(
            EngineerStateData savedState,
            EngineerGenerationConfig generationConfig,
            out EngineerBuild engineer,
            out string error)
        {
            engineer = null;
            savedState.EnsureInitialized();

            if (savedState.perkIds.Count == 0)
            {
                error = "The saved engineer has no perk IDs.";
                return false;
            }

            if (!TryBuildPerkLookups(
                    generationConfig,
                    out Dictionary<string, PerkDefinition> definitionsById,
                    out Dictionary<string, RandomizationItem> itemsById,
                    out error))
            {
                return false;
            }

            var perks = new List<EngineerPerk>(savedState.perkIds.Count);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);

            for (int perkIndex = 0;
                 perkIndex < savedState.perkIds.Count;
                 perkIndex++)
            {
                string perkId = savedState.perkIds[perkIndex];

                if (string.IsNullOrWhiteSpace(perkId))
                {
                    error = $"Saved perk ID at index {perkIndex} is empty.";
                    return false;
                }

                if (!uniqueIds.Add(perkId))
                {
                    error = $"Saved perk ID '{perkId}' is duplicated.";
                    return false;
                }

                if (!definitionsById.TryGetValue(
                        perkId,
                        out PerkDefinition definition))
                {
                    error = $"Saved perk definition '{perkId}' was not found.";
                    return false;
                }

                if (!itemsById.TryGetValue(
                        perkId,
                        out RandomizationItem randomizationItem))
                {
                    error = $"Saved perk pool item '{perkId}' was not found.";
                    return false;
                }

                perks.Add(new EngineerPerk(randomizationItem, definition));
            }

            string engineerName = string.IsNullOrWhiteSpace(savedState.name)
                ? "Engineer"
                : savedState.name;

            engineer = new EngineerBuild(
                engineerName,
                savedState.operatorCode ?? string.Empty,
                savedState.totalWeight,
                savedState.spentWeight,
                perks);
            error = null;
            return true;
        }

        private static bool TryBuildPerkLookups(
            EngineerGenerationConfig generationConfig,
            out Dictionary<string, PerkDefinition> definitionsById,
            out Dictionary<string, RandomizationItem> itemsById,
            out string error)
        {
            definitionsById = new Dictionary<string, PerkDefinition>(
                StringComparer.Ordinal);
            itemsById = new Dictionary<string, RandomizationItem>(
                StringComparer.Ordinal);

            PerkDefinition[] definitions = generationConfig.PerkDefinitions;

            if (definitions == null || definitions.Length == 0)
            {
                error = "EngineerGenerationConfig has no perk definitions.";
                return false;
            }

            for (int definitionIndex = 0;
                 definitionIndex < definitions.Length;
                 definitionIndex++)
            {
                PerkDefinition definition = definitions[definitionIndex];

                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.Id))
                {
                    error =
                        $"Perk definition at index {definitionIndex} has no ID.";
                    return false;
                }

                if (!definitionsById.TryAdd(definition.Id, definition))
                {
                    error = $"Perk definition ID '{definition.Id}' is duplicated.";
                    return false;
                }
            }

            if (generationConfig.PerkPool == null ||
                generationConfig.PerkPool.GetItems == null)
            {
                error = "EngineerGenerationConfig has no perk pool.";
                return false;
            }

            IReadOnlyList<RandomizationItem> items =
                generationConfig.PerkPool.GetItems;

            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                RandomizationItem item = items[itemIndex];

                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    error = $"Perk pool item at index {itemIndex} has no ID.";
                    return false;
                }

                if (!itemsById.TryAdd(item.Name, item))
                {
                    error = $"Perk pool item ID '{item.Name}' is duplicated.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
