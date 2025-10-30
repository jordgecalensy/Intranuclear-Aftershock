
using Failsafe.PlayerMovements;
using Failsafe.Scripts.EffectSystem;

namespace Failsafe.Items
{
    public class Tushkan : IUsable
    {
        private TushkanData _data;
        private readonly IEffectManager _effectManager;
        private TushkanEffect _effect;

        public Tushkan(TushkanData data, PlayerMovementParameters playerMovementParameters, IEffectManager effectManager)
        {
            _data = data;
            _effectManager = effectManager;
            _effect = new TushkanEffect(_data.Duration, playerMovementParameters, _data.JumpMultiplier);
        }


        public ItemUseResult Use()
        {
            _effectManager.ApplyEffect(_effect);
            return ItemUseResult.Consumed;
        }

        public void GetItemUseDelays(out float startUseDelay, out float useDelay)
        {
            startUseDelay = _data.StartUseDelay;
            useDelay = _data.UseDelay;
        }
    }
}
