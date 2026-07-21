using System;
using UnityEngine;

namespace Failsafe.Scripts.SaveSystem
{
    [Obsolete("Use IRunSaveService from gameplay code. This component is a temporary UnityEvent bridge.")]
    [RequireComponent(typeof(SaveLoadManager))]
    public sealed class SaveLoadGame : MonoBehaviour
    {
        private SaveLoadManager _saveLoadManager;

        private void Awake()
        {
            _saveLoadManager = GetComponent<SaveLoadManager>();
        }

        public void SavePlayerState()
        {
            _saveLoadManager.SaveCurrentCheckpoint();
        }

        public void LoadGame()
        {
            _saveLoadManager.LoadGame();
        }
    }
}
