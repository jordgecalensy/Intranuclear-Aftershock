using System;
using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    public sealed class InventoryModelViewDefinition
    {
        public GameObject ModelPrefab { get; }
        public Quaternion BaseRotation { get; }
        public Vector3 OffsetInCells { get; }
        public float ScaleMultiplier { get; }
        public float FitPaddingRatio { get; }
        public float MaxDepthInCells { get; }

        public InventoryModelViewDefinition(
            GameObject modelPrefab,
            Quaternion baseRotation,
            Vector3 offsetInCells,
            float scaleMultiplier = 1f,
            float fitPaddingRatio = 0.08f,
            float maxDepthInCells = 0.75f)
        {
            if (modelPrefab == null)
                throw new ArgumentNullException(nameof(modelPrefab));

            if (scaleMultiplier <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scaleMultiplier),
                    "Scale multiplier must be greater than zero.");
            }

            if (fitPaddingRatio < 0f || fitPaddingRatio >= 0.5f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fitPaddingRatio),
                    "Fit padding ratio must be at least zero and less than 0.5.");
            }

            if (maxDepthInCells <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDepthInCells),
                    "Maximum model depth must be greater than zero.");
            }

            ModelPrefab = modelPrefab;
            BaseRotation = baseRotation;
            OffsetInCells = offsetInCells;
            ScaleMultiplier = scaleMultiplier;
            FitPaddingRatio = fitPaddingRatio;
            MaxDepthInCells = maxDepthInCells;
        }
    }
}
