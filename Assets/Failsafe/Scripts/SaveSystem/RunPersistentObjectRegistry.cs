using System;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.SaveSystem
{
    /// <summary>
    /// Keeps the persistent objects for the current scene without rescanning the
    /// whole hierarchy for every checkpoint.
    /// </summary>
    public sealed class RunPersistentObjectRegistry : IDisposable
    {
        private readonly HashSet<RunPersistentObject> _objects = new();

        private bool _hasScannedScene;
        private bool _isDisposed;

        public RunPersistentObjectRegistry()
        {
            RunPersistentObject.BecameAvailable += Register;
            RunPersistentObject.WasDestroyed += Unregister;
        }

        public void GetObjects(List<RunPersistentObject> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RunPersistentObjectRegistry));

            EnsureSceneScanned();
            results.Clear();

            foreach (RunPersistentObject persistentObject in _objects)
            {
                if (persistentObject != null)
                    results.Add(persistentObject);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            RunPersistentObject.BecameAvailable -= Register;
            RunPersistentObject.WasDestroyed -= Unregister;
            _objects.Clear();
        }

        private void EnsureSceneScanned()
        {
            if (_hasScannedScene)
                return;

            RunPersistentObject[] sceneObjects =
                UnityEngine.Object.FindObjectsByType<RunPersistentObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < sceneObjects.Length; i++)
                Register(sceneObjects[i]);

            _hasScannedScene = true;
        }

        private void Register(RunPersistentObject persistentObject)
        {
            if (_isDisposed || persistentObject == null)
                return;

            _objects.Add(persistentObject);
        }

        private void Unregister(RunPersistentObject persistentObject)
        {
            if (_isDisposed)
                return;

            _objects.Remove(persistentObject);
        }
    }
}
