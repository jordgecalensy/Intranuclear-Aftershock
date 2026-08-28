using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Player.UI
{
    public static class SpriteAccentColorUtility
    {
        private const float BottomStripRatio = 0.15f;
        private const float MinimumAlpha = 0.1f;
        private const float MinimumSaturation = 0.2f;
        private const float MinimumBrightness = 0.18f;
        private const int HueBinCount = 24;

        private static readonly Dictionary<Sprite, CachedColor> Cache = new();

        public static bool TryGetBottomEdgeColor(
            Sprite sprite,
            out Color color)
        {
            color = default;

            if (sprite == null)
                return false;

            if (Cache.TryGetValue(sprite, out CachedColor cachedColor))
            {
                color = cachedColor.Color;
                return cachedColor.HasColor;
            }

            bool hasColor = TryExtractBottomEdgeColor(sprite, out color);
            Cache[sprite] = new CachedColor(hasColor, color);

            return hasColor;
        }

        private static bool TryExtractBottomEdgeColor(
            Sprite sprite,
            out Color color)
        {
            color = default;

            Texture2D texture = sprite.texture;

            if (texture == null || !texture.isReadable)
                return false;

            Rect textureRect;

            try
            {
                textureRect = sprite.textureRect;
            }
            catch (UnityException)
            {
                textureRect = sprite.rect;
            }

            int x = Mathf.Clamp(
                Mathf.FloorToInt(textureRect.x),
                0,
                texture.width - 1);
            int y = Mathf.Clamp(
                Mathf.FloorToInt(textureRect.y),
                0,
                texture.height - 1);
            int width = Mathf.Clamp(
                Mathf.CeilToInt(textureRect.width),
                1,
                texture.width - x);
            int height = Mathf.Clamp(
                Mathf.CeilToInt(textureRect.height * BottomStripRatio),
                1,
                texture.height - y);

            Color[] pixels;

            try
            {
                pixels = texture.GetPixels(x, y, width, height);
            }
            catch (UnityException)
            {
                return false;
            }

            var hueWeights = new float[HueBinCount];
            var weightedColors = new Color[HueBinCount];

            foreach (Color pixel in pixels)
            {
                if (pixel.a < MinimumAlpha)
                    continue;

                Color.RGBToHSV(pixel, out float hue, out float saturation, out float brightness);

                if (saturation < MinimumSaturation ||
                    brightness < MinimumBrightness)
                {
                    continue;
                }

                int hueBin = Mathf.Clamp(
                    Mathf.FloorToInt(hue * HueBinCount),
                    0,
                    HueBinCount - 1);

                float weight =
                    pixel.a *
                    Mathf.Lerp(0.25f, 1f, saturation) *
                    brightness;

                Color opaquePixel = pixel;
                opaquePixel.a = 1f;

                hueWeights[hueBin] += weight;
                weightedColors[hueBin] += opaquePixel * weight;
            }

            int dominantBin = -1;
            float dominantWeight = 0f;

            for (int i = 0; i < HueBinCount; i++)
            {
                if (hueWeights[i] <= dominantWeight)
                    continue;

                dominantBin = i;
                dominantWeight = hueWeights[i];
            }

            if (dominantBin < 0 || dominantWeight <= 0f)
                return false;

            color = weightedColors[dominantBin] / dominantWeight;
            color.a = 1f;

            return true;
        }

        private readonly struct CachedColor
        {
            public readonly bool HasColor;
            public readonly Color Color;

            public CachedColor(bool hasColor, Color color)
            {
                HasColor = hasColor;
                Color = color;
            }
        }
    }
}
