using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Items
{
    public class StasisGun : IUsable, ITickable
    {
        private readonly StasisGunData _data;
        private readonly EnergyContainer _energyContainer;

        private Item _stasisGunItem;
        private bool _isDefaultMode = true;
        private float _fireRateTimer;

        [Inject] private Camera _playerCam;
        [Inject] private IEffectApplicationService _effects;

        public StasisGun(StasisGunData data)
        {
            _data = data;
            _energyContainer = new EnergyContainer(_data);
        }

        public void Tick()
        {
            if (_fireRateTimer > 0f)
                _fireRateTimer -= Time.deltaTime;
        }

        public ItemUseResult Use()
        {
            Shoot(Raycast());

            return new ItemUseResult
            {
                ItemStateAfterUse = ItemState.Hold,
                UsageType = UsageType.ClickToUse
            };
        }

        public void AltMode()
        {
            SoundUtils3D.Play(_stasisGunItem.gameObject, _data.ModeSwitchSFX);
            _isDefaultMode = !_isDefaultMode;
            Debug.Log("Default mode is " + _isDefaultMode);
        }

        public void ParseItem(Item item)
        {
            _stasisGunItem = item;
        }

        public void Shoot(RaycastHit hit)
        {
            if (_fireRateTimer > 0f)
                return;

            if (_energyContainer.IsEmpty())
            {
                SoundUtils3D.Play(_stasisGunItem.gameObject, _data.EmptyShotSFX);
                return;
            }

            _fireRateTimer = _data.FireRate;
            _energyContainer.UseChargeAmount();

            SoundUtils3D.Play(_stasisGunItem.gameObject, _data.GunshotSFX);

            if (hit.collider == null)
                return;

            EffectBundle bundle = _isDefaultMode
                ? _data.DefaultModeEffects
                : _data.AlternativeModeEffects;

            var context = new EffectContext(
                _stasisGunItem.gameObject,
                hit.collider,
                hit.point,
                hit.normal,
                (hit.point - _playerCam.transform.position).normalized);

            _effects?.Apply(bundle, context);
        }

        private RaycastHit Raycast()
        {
            Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);
            LayerMask mask = ~(1 << 5);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, mask))
                Debug.Log("Object ahead: " + hit.collider.name);
            else
                Debug.Log("No Object!");

            return hit;
        }

        public void GetItemUseDelays(out float startUseDelay, out float useDelay)
        {
            startUseDelay = _data.StartUseDelay;
            useDelay = _data.UseDelay;
        }
    }
}