using UnityEngine;
using UnityEngine.UI;

namespace Failsafe.Inventory.Presentation
{
    public static class InventoryIconRawImageUtility
    {
        public static bool TryApply(
            RawImage target,
            Sprite icon,
            out string error)
        {
            if (target == null)
            {
                error = "Icon target is not assigned.";
                return false;
            }

            if (icon == null || icon.texture == null)
            {
                Clear(target);
                error = "Inventory icon is not assigned.";
                return false;
            }

            Texture2D texture = icon.texture;
            Rect spriteRect = icon.rect;

            target.texture = texture;
            target.material = null;
            target.uvRect = new Rect(
                spriteRect.x / texture.width,
                spriteRect.y / texture.height,
                spriteRect.width / texture.width,
                spriteRect.height / texture.height);
            target.color = Color.white;
            target.enabled = true;

            error = null;
            return true;
        }

        public static void Clear(RawImage target)
        {
            if (target == null)
                return;

            target.texture = null;
            target.material = null;
            target.uvRect = new Rect(0f, 0f, 1f, 1f);
            target.enabled = false;
        }
    }
}
