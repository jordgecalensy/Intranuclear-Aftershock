using System;

namespace Assets.Failsafe.Scripts.RandomGeneration
{
    public sealed class EngineerSelectionState
    {
        public EngineerGenerationResult CurrentOffers { get; private set; }
        public EngineerBuild SelectedEngineer { get; private set; }

        public bool HasSelection => SelectedEngineer != null;

        public void RestoreSelectedEngineer(EngineerBuild selectedEngineer)
        {
            CurrentOffers = null;
            SelectedEngineer = selectedEngineer ??
                throw new ArgumentNullException(nameof(selectedEngineer));
        }

        public void SetOffers(EngineerGenerationResult offers)
        {
            CurrentOffers = offers ??
                throw new ArgumentNullException(nameof(offers));
            SelectedEngineer = null;
        }

        public bool TrySelectEngineer(int engineerIndex, out string error)
        {
            if (CurrentOffers == null || CurrentOffers.Engineers == null)
            {
                error = "Engineer offers have not been generated.";
                return false;
            }

            if (engineerIndex < 0 ||
                engineerIndex >= CurrentOffers.Engineers.Count)
            {
                error = $"Engineer index {engineerIndex} is out of range.";
                return false;
            }

            SelectedEngineer = CurrentOffers.Engineers[engineerIndex];

            if (SelectedEngineer == null)
            {
                error = $"Engineer {engineerIndex + 1} is null.";
                return false;
            }

            error = null;
            return true;
        }

        public void Clear()
        {
            CurrentOffers = null;
            SelectedEngineer = null;
        }
    }
}
