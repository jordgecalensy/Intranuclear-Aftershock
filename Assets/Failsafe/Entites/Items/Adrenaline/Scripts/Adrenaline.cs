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

        public void AltMode() { }

        public void ParseItem(Item item_object) { }
        public void GetItemUseDelays(out float startUseDelay, out float useDelay)
        {
            startUseDelay = _data.StartUseDelay;
            useDelay = _data.UseDelay;
        }
    }
}
