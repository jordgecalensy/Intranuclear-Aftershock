using System;
using UnityEngine;

namespace Failsafe.Scripts.SaveSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class PlacedEnemySaveIdentity : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Stable ID of this scene enemy. Generated automatically; do not copy it between enemies.")]
        private string _instanceId;

        public string InstanceId => _instanceId;

        private void Reset()
        {
            EnsureInstanceId();
        }

        private void OnValidate()
        {
            EnsureInstanceId();
        }

        [ContextMenu("Regenerate Persistent Instance ID")]
        private void RegenerateInstanceId()
        {
            if (Application.isPlaying)
            {
                RunSaveLog.Warning(
                    RunSaveLog.Enemy,
                    "A placed enemy ID cannot be regenerated while the game is running.",
                    this);
                return;
            }

            _instanceId = CreateInstanceId();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void EnsureInstanceId()
        {
            if (string.IsNullOrWhiteSpace(_instanceId))
                _instanceId = CreateInstanceId();
        }

        private static string CreateInstanceId()
        {
            return $"placed:{Guid.NewGuid():N}";
        }
    }
}
