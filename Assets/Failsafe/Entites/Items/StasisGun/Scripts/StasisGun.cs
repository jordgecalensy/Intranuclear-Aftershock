using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Items
{
    public class StasisGun : IUsable, ITickable
    {
        private readonly Camera _playerCam;
        private readonly IEffectApplicationService _effects;

        private Item _item;
        private bool _isDefaultMode = true;
        private float _fireRateTimer;

        public StasisGun(
            Camera playerCam,
            IEffectApplicationService effects)
        {
            _playerCam = playerCam;
            _effects = effects;
        }

        public void Tick()
        {
            if (_fireRateTimer > 0f)
                _fireRateTimer -= Time.deltaTime;
        }

        public ItemUseResult Use()
        {
            if (_item == null || _item.ItemData == null)
                return HoldClickResult();

            TryShoot();

            return HoldClickResult();
        }

        public void AltMode()
        {
            if (_item == null || _item.ItemData == null)
                return;

            _isDefaultMode = !_isDefaultMode;

            if (!_item.ItemData.ModeSwitchSFX.IsNull)
                SoundUtils3D.Play(_item.gameObject, _item.ItemData.ModeSwitchSFX);

            Debug.Log($"[StasisGun] Default mode = {_isDefaultMode}", _item);
        }

        public void ParseItem(Item item_object)
        {
            _item = item_object;
            _isDefaultMode = true;
            _fireRateTimer = 0f;

            if (_item == null)
                return;

            if (_item.ItemData == null)
            {
                Debug.LogError("[StasisGun] ItemData is null.", _item);
                return;
            }

            if (_item.ItemData.Type != ItemType.StasisGun)
                Debug.LogWarning($"[StasisGun] Item type is {_item.ItemData.Type}, expected StasisGun.", _item);
        }

        public void GetItemUseDelays(out float startDelay, out float useDelay)
        {
            if (_item == null || _item.ItemData == null)
            {
                startDelay = 0f;
                useDelay = 0f;
                return;
            }

            startDelay = Mathf.Max(0f, _item.ItemData.StartUseDelay);
            useDelay = Mathf.Max(0f, _item.ItemData.UseDelay);
        }

        private void TryShoot()
        {
            if (_fireRateTimer > 0f)
                return;

            if (_playerCam == null)
            {
                Debug.LogError("[StasisGun] Player camera is null.");
                return;
            }

            if (_effects == null)
            {
                Debug.LogError("[StasisGun] IEffectApplicationService is null.");
                return;
            }

            ItemData data = _item.ItemData;

            if (data.UsesEnergy && !_item.TryUseEnergy())
            {
                if (!data.EmptyUseSFX.IsNull)
                    SoundUtils3D.Play(_item.gameObject, data.EmptyUseSFX);

                Debug.Log("[StasisGun] Empty energy.", _item);
                return;
            }

            _fireRateTimer = Mathf.Max(0.01f, data.UseDelay);

            if (!data.UseSFX.IsNull)
                SoundUtils3D.Play(_item.gameObject, data.UseSFX);

            RaycastHit hit = Raycast(data);

            if (hit.collider == null)
                return;

            EffectBundle bundle = _isDefaultMode
                ? data.DefaultModeEffects
                : data.AlternativeModeEffects;

            if (bundle == null)
            {
                Debug.LogWarning("[StasisGun] EffectBundle is not assigned.", _item);
                return;
            }

            Vector3 direction = hit.point - _playerCam.transform.position;

            if (direction.sqrMagnitude <= 0.0001f)
                direction = _playerCam.transform.forward;

            direction.Normalize();

            var context = new EffectContext(
                _item.gameObject,
                hit.collider,
                hit.point,
                hit.normal,
                direction,
                1f);

            _effects.Apply(bundle, context);
        }

        private RaycastHit Raycast(ItemData data)
        {
            Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);

            float range = Mathf.Max(0.1f, data.UseRange);
            LayerMask mask = data.UseMask;

            if (Physics.Raycast(ray, out RaycastHit hit, range, mask))
            {
                Debug.Log("[StasisGun] Object ahead: " + hit.collider.name);
                return hit;
            }

            Debug.Log("[StasisGun] No object!");
            return default;
        }

        private static ItemUseResult HoldClickResult()
        {
            return new ItemUseResult
            {
                ItemStateAfterUse = ItemState.Hold,
                UsageType = UsageType.ClickToUse
            };
        }
    }
}