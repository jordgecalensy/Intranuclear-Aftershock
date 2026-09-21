using UnityEngine;

namespace Failsafe.Player.View
{
    public sealed class PlayerCameraPoseOverride
    {
        private Object _owner;
        private Transform _anchor;
        private bool _hasOwner;

        public bool HasOverride
        {
            get
            {
                ClearInvalidOverride();
                return _hasOwner;
            }
        }

        public bool TryAcquire(
            Object owner,
            Transform anchor,
            out string error)
        {
            ClearInvalidOverride();

            if (owner == null)
            {
                error = "Camera pose override owner is missing.";
                return false;
            }

            if (anchor == null)
            {
                error = "Camera pose override anchor is missing.";
                return false;
            }

            if (_hasOwner && !ReferenceEquals(_owner, owner))
            {
                error =
                    $"Player camera pose is already owned by " +
                    $"'{_owner.name}'.";
                return false;
            }

            _owner = owner;
            _anchor = anchor;
            _hasOwner = true;
            error = null;
            return true;
        }

        public bool TryGetPose(out Pose pose)
        {
            ClearInvalidOverride();

            if (!_hasOwner)
            {
                pose = default;
                return false;
            }

            pose = new Pose(_anchor.position, _anchor.rotation);
            return true;
        }

        public bool Release(Object owner)
        {
            if (!_hasOwner || !ReferenceEquals(_owner, owner))
                return false;

            Clear();
            return true;
        }

        private void ClearInvalidOverride()
        {
            if (_hasOwner && (_owner == null || _anchor == null))
                Clear();
        }

        private void Clear()
        {
            _owner = null;
            _anchor = null;
            _hasOwner = false;
        }
    }
}
