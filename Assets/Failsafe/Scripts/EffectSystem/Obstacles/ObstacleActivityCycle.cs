using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Owns the active/inactive cycle of one obstacle.
    /// </summary>
    public sealed class ObstacleActivityCycle
    {
        private const float MinimumDuration = 0.01f;

        private readonly GameObject _owner;
        private readonly Collider _contactTrigger;
        private readonly GameObject _visualModel;

        private Renderer[] _ownerRenderers;
        private bool[] _ownerRendererStates;
        private float _timer;

        public bool IsActive { get; private set; } = true;

        public ObstacleActivityCycle(
            GameObject owner,
            Collider contactTrigger,
            GameObject visualModel)
        {
            _owner = owner;
            _contactTrigger = contactTrigger;
            _visualModel = visualModel;
        }

        public void Initialize(float activeDuration)
        {
            IsActive = true;
            _timer = NormalizeDuration(activeDuration);
        }

        /// <summary>
        /// Returns true only when this tick changed the obstacle to inactive.
        /// </summary>
        public bool Tick(
            float deltaTime,
            bool cycleEnabled,
            float activeDuration,
            float inactiveDuration)
        {
            if (!cycleEnabled)
                return false;

            _timer -= deltaTime;

            if (_timer > 0f)
                return false;

            SetActive(
                !IsActive,
                activeDuration,
                inactiveDuration);
            return !IsActive;
        }

        private void SetActive(
            bool isActive,
            float activeDuration,
            float inactiveDuration)
        {
            IsActive = isActive;
            _timer = NormalizeDuration(
                isActive ? activeDuration : inactiveDuration);

            if (_contactTrigger != null)
                _contactTrigger.enabled = isActive;

            if (_visualModel != null && _visualModel != _owner)
            {
                _visualModel.SetActive(isActive);
                return;
            }

            SetOwnerRenderersActive(isActive);
        }

        private void SetOwnerRenderersActive(bool isActive)
        {
            EnsureOwnerRenderers();

            for (int i = 0; i < _ownerRenderers.Length; i++)
            {
                Renderer renderer = _ownerRenderers[i];

                if (renderer == null)
                    continue;

                renderer.enabled = isActive
                    ? _ownerRendererStates[i]
                    : false;
            }
        }

        private void EnsureOwnerRenderers()
        {
            if (_ownerRenderers != null)
                return;

            if (_owner == null)
            {
                _ownerRenderers = System.Array.Empty<Renderer>();
                _ownerRendererStates = System.Array.Empty<bool>();
                return;
            }

            _ownerRenderers =
                _owner.GetComponentsInChildren<Renderer>(true);
            _ownerRendererStates = new bool[_ownerRenderers.Length];

            for (int i = 0; i < _ownerRenderers.Length; i++)
            {
                Renderer renderer = _ownerRenderers[i];
                _ownerRendererStates[i] =
                    renderer != null && renderer.enabled;
            }
        }

        private static float NormalizeDuration(float duration)
        {
            return Mathf.Max(MinimumDuration, duration);
        }
    }
}
