using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Items
{
    public class StasisGun : IUsable, ITickable
    {
        private StasisGunData _data;
        private Item _stasisGunItem;
        EnergyContainer _energyContainer;
        private bool _isDefaultMode = true;
        float _fireRateTimer = 0;

        [Inject] Camera _playerCam;

        public StasisGun(StasisGunData data)
        {
            _data = data;
            _energyContainer = new EnergyContainer(_data);
        }

        public void Tick()
        {
            if (_fireRateTimer > 0)
            {
                _fireRateTimer -= Time.deltaTime;
            }
        }


        public ItemUseResult Use()
        {
            Shoot(Raycast());
            return new ItemUseResult { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.ClickToUse };
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
            if (_fireRateTimer <= 0 && !_energyContainer.IsEmpty())
            {
                _fireRateTimer = _data.FireRate;
                _energyContainer.UseChargeAmount();
                SoundUtils3D.Play(_stasisGunItem.gameObject, _data.GunshotSFX);
                if (hit.collider != null)
                {
                    if (_isDefaultMode)
                        DefaultMode(hit);
                    else
                        AltMode(hit);
                }
            }
            else if (_energyContainer.IsEmpty()) SoundUtils3D.Play(_stasisGunItem.gameObject, _data.EmptyShotSFX);
        }

        void DefaultMode(RaycastHit hit)
        {
            if (hit.collider.GetComponent<Stasisable>() != null)
            {
                hit.collider.GetComponent<Stasisable>().StartStasis(_data.StasisDuration);
            }
            else if (hit.collider.GetComponentInParent<Enemy>() != null)
            {
                hit.collider.GetComponentInParent<Enemy>().DisableState(_data.StasisDuration);
            }
        }

        void AltMode(RaycastHit hit)
        {
            if (hit.collider.GetComponent<Stasisable>() != null)
            {
                hit.collider.GetComponent<Stasisable>().StartStasisWithInertion(_data.StasisDuration);
            }
            else if (hit.collider.GetComponentInParent<Enemy>() != null)
            {
                hit.collider.GetComponentInParent<Enemy>().DisableState(_data.StasisDuration);
            }
        }

        RaycastHit Raycast()
        {
            Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);
            //маска чтобы рейкаст точно игнорировал игрока
            LayerMask mask = ~(1 << 5);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100, mask))
            {
                Debug.Log("Object ahead: " + hit.collider.name);
            }
            else
            {

                Debug.Log("No Object!");
            }
            return hit;
        }

        public void GetItemUseDelays(out float startUseDelay, out float useDelay)
        {
            startUseDelay = _data.StartUseDelay;
            useDelay = _data.UseDelay;
        }
    }

}
