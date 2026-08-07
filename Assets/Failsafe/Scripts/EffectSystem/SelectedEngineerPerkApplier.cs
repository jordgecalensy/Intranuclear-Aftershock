using Assets.Failsafe.Scripts.RandomGeneration;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    public sealed class SelectedEngineerPerkApplier : IStartable
    {
        private readonly EngineerSelectionState _selectionState;
        private readonly IEffectApplicationService _effectApplicationService;
        private readonly CharacterController _target;

        public SelectedEngineerPerkApplier(
            EngineerSelectionState selectionState,
            IEffectApplicationService effectApplicationService,
            CharacterController target)
        {
            _selectionState = selectionState;
            _effectApplicationService = effectApplicationService;
            _target = target;
        }

        public void Start()
        {
            EngineerBuild selectedEngineer = _selectionState?.SelectedEngineer;

            if (selectedEngineer == null ||
                selectedEngineer.Perks == null ||
                _effectApplicationService == null ||
                _target == null)
            {
                return;
            }

            Transform targetTransform = _target.transform;
            Vector3 direction = targetTransform.forward;

            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.forward;

            var context = new EffectContext(
                _target.gameObject,
                _target,
                targetTransform.position,
                Vector3.up,
                direction.normalized,
                1f);

            for (int perkIndex = 0;
                 perkIndex < selectedEngineer.Perks.Count;
                 perkIndex++)
            {
                PerkDefinition perkDefinition =
                    selectedEngineer.Perks[perkIndex]?.Definition;
                EffectBundle bundle = perkDefinition?.EffectBundle;

                if (bundle != null)
                    _effectApplicationService.Apply(bundle, context);
            }
        }
    }
}
