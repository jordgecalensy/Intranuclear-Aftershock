using Failsafe.Scripts.Modifiebles;
using Failsafe.Scripts.EffectSystem;
using Failsafe.PlayerMovements;
using UnityEngine;

namespace Failsafe.Items
{
    public class Adrenaline : IUsable
    {
        private readonly AdrenalineData _data;
        private readonly IEffectManager _effectManager;
        private Item _item;

        //Т.к. эффект уникальный, можно создать его один раз и не пересоздавать при каждом применении
        private readonly AdrenalineEffect _effect;


        public Adrenaline(AdrenalineData data, PlayerMovementParameters playerMovementParameters, IEffectManager effectManager)
        {
            _data = data;
            _effectManager = effectManager;
            _effect = new AdrenalineEffect(_data.Duration, playerMovementParameters, _data.SpeedMultiplier);
        }

        public ItemUseResult Use()
        {
            // Если эффекты должны складываться друг с другом, то нужно убрать у эффекта IsUniqueEffect = true;
            // и создавать новый экземпляр перед каждым применением (в этом случае можно создать Pooling для оптимизации)
            _effectManager.ApplyEffect(_effect);
            Debug.Log("Adrenaline used");
            return ItemUseResult.Consumed;
        }

        public ItemUseResult AltMode()
        {
            return new ItemUseResult() { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.ClickToUse };
        }

        public void ParseItem(Item item_object)
        {
            _item = item_object;
        }
        public void GetItemUseDelays(out float startDelay, out float useDelay, out float startAltUseDelay, out float altUseDelay)
        {
            if (_item == null || _item.ItemData == null)
            {
                startDelay = 0f;
                useDelay = 0f;
                startAltUseDelay = 0f;
                altUseDelay = 0f;
                return;
            }

            startDelay = Mathf.Max(0f, _item.ItemData.StartUseDelay);
            useDelay = Mathf.Max(0f, _item.ItemData.UseDelay);
            startAltUseDelay = Mathf.Max(0f, _item.ItemData.StartAltUseDelay);
            altUseDelay = Mathf.Max(0f, _item.ItemData.UseAltDelay);
        }
    }
}
