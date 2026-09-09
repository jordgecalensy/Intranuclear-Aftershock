using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Owns the changing radius and intensity of one fire area.
    /// </summary>
    public sealed class FireAreaLifecycle
    {
        public float Radius { get; private set; }
        public float Intensity { get; private set; }
        public bool IsBurningOut { get; private set; }

        public void Initialize(
            float initialRadius,
            float maxRadius,
            float initialIntensity)
        {
            Radius = Mathf.Clamp(
                initialRadius,
                0.1f,
                Mathf.Max(0.1f, maxRadius));
            Intensity = Mathf.Max(0f, initialIntensity);
            IsBurningOut = false;
        }

        public void Tick(
            float deltaTime,
            float maxRadius,
            float radiusGrowthPerSecond,
            float intensityGrowthPerSecond,
            float peakIntensity,
            float sustainIntensity,
            float burnoutDecayPerSecond)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);

            if (Radius < maxRadius)
            {
                Radius = Mathf.Min(
                    maxRadius,
                    Radius + Mathf.Max(0f, radiusGrowthPerSecond) * safeDeltaTime);
            }

            if (!IsBurningOut)
            {
                TickGrowth(
                    safeDeltaTime,
                    intensityGrowthPerSecond,
                    peakIntensity);
                return;
            }

            if (Intensity > sustainIntensity)
            {
                Intensity = Mathf.Max(
                    sustainIntensity,
                    Intensity - Mathf.Max(0f, burnoutDecayPerSecond) * safeDeltaTime);
            }
        }

        public void SetIntensity(float value)
        {
            Intensity = Mathf.Max(0f, value);
        }

        public void AddExtinguishImpulse(float amount)
        {
            if (amount <= 0f)
                return;

            Intensity = Mathf.Max(0f, Intensity - amount);
            IsBurningOut = true;
        }

        public FireAreaAdvanced.Tier GetTier(
            float mediumThreshold,
            float strongThreshold,
            float peakIntensity)
        {
            if (Intensity < mediumThreshold)
                return FireAreaAdvanced.Tier.Weak;

            if (Intensity < strongThreshold)
                return FireAreaAdvanced.Tier.Medium;

            float bigThreshold = Mathf.Max(
                strongThreshold + 0.0001f,
                peakIntensity * 0.95f);

            return Intensity < bigThreshold
                ? FireAreaAdvanced.Tier.Strong
                : FireAreaAdvanced.Tier.Big;
        }

        private void TickGrowth(
            float deltaTime,
            float growthPerSecond,
            float peakIntensity)
        {
            if (Intensity < peakIntensity)
            {
                Intensity = Mathf.Min(
                    peakIntensity,
                    Intensity + Mathf.Max(0f, growthPerSecond) * deltaTime);

                if (Mathf.Approximately(Intensity, peakIntensity))
                    IsBurningOut = true;

                return;
            }

            IsBurningOut = true;
        }
    }
}
