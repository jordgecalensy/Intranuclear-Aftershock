using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Failsafe.Inventory.Presentation
{
    [CreateAssetMenu(menuName = "Failsafe/Inventory/Ruvi Emotion Library")]
    public sealed class RuviEmotionLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Emotion
        {
            public string Name;
            public VideoClip Clip;
            [Min(0)] public int Weight;
        }

        [SerializeField] private Texture2D _neutralFace;
        [SerializeField] private Emotion[] _emotions = Array.Empty<Emotion>();

        public Texture2D NeutralFace => _neutralFace;
        public IReadOnlyList<Emotion> Emotions => _emotions;

        public int TotalWeight
        {
            get
            {
                long total = 0;
                foreach (Emotion emotion in _emotions)
                    if (IsAvailable(emotion))
                        total += emotion.Weight;
                return total <= int.MaxValue ? (int)total : 0;
            }
        }

        public bool TrySelect(int roll, out Emotion selected)
        {
            selected = null;
            if (roll < 0 || roll >= TotalWeight)
                return false;

            foreach (Emotion emotion in _emotions)
            {
                if (!IsAvailable(emotion))
                    continue;
                if (roll < emotion.Weight)
                {
                    selected = emotion;
                    return true;
                }
                roll -= emotion.Weight;
            }
            return false;
        }

        private static bool IsAvailable(Emotion emotion) =>
            emotion != null && emotion.Clip != null && emotion.Weight > 0;
    }
}
