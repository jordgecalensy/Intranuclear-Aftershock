using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Scripts.SaveSystem
{
    public sealed class PlacedEnemyRegistrationService : IStartable
    {
        private readonly EnemyRuntimeRegistry _registry;

        public PlacedEnemyRegistrationService(EnemyRuntimeRegistry registry)
        {
            _registry = registry;
        }

        public void Start()
        {
            PlacedEnemySaveIdentity[] identities =
                Object.FindObjectsByType<PlacedEnemySaveIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < identities.Length; i++)
            {
                PlacedEnemySaveIdentity identity = identities[i];
                if (!_registry.TryRegisterPlaced(identity, out _, out string error))
                {
                    RunSaveLog.Error(
                        RunSaveLog.Enemy,
                        $"{nameof(PlacedEnemyRegistrationService)}: {error}",
                        identity);
                }
            }

            WarnAboutUnregisteredSceneEnemies();
        }

        private static void WarnAboutUnregisteredSceneEnemies()
        {
            Enemy[] enemies = Object.FindObjectsByType<Enemy>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy.GetComponent<PlacedEnemySaveIdentity>() != null)
                    continue;

                RunSaveLog.Warning(
                    RunSaveLog.Enemy,
                    $"{nameof(PlacedEnemyRegistrationService)}: Scene enemy " +
                    $"'{enemy.name}' has no {nameof(PlacedEnemySaveIdentity)} and will not " +
                    "be included in run saves.",
                    enemy);
            }
        }
    }
}
