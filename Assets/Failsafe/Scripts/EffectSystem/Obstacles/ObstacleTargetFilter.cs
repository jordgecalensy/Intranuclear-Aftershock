using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Decides which roots an obstacle may affect.
    /// </summary>
    public sealed class ObstacleTargetFilter
    {
        private readonly bool _affectPlayers;
        private readonly bool _affectEnemies;
        private readonly LayerMask _playerLayers;
        private readonly LayerMask _enemyLayers;
        private readonly string _playerTag;
        private readonly string _enemyTag;

        public ObstacleTargetFilter(
            bool affectPlayers,
            bool affectEnemies,
            LayerMask playerLayers,
            LayerMask enemyLayers,
            string playerTag,
            string enemyTag)
        {
            _affectPlayers = affectPlayers;
            _affectEnemies = affectEnemies;
            _playerLayers = playerLayers;
            _enemyLayers = enemyLayers;
            _playerTag = playerTag;
            _enemyTag = enemyTag;
        }

        public bool IsAllowed(GameObject target)
        {
            if (_affectPlayers && IsPlayer(target))
                return true;

            return _affectEnemies && IsEnemy(target);
        }

        public bool IsPlayer(GameObject target)
        {
            return Matches(
                target,
                _playerLayers,
                _playerTag);
        }

        public bool IsEnemy(GameObject target)
        {
            return Matches(
                target,
                _enemyLayers,
                _enemyTag);
        }

        private static bool Matches(
            GameObject target,
            LayerMask layers,
            string tag)
        {
            if (target == null)
                return false;

            if (layers.value != 0)
                return (layers.value & (1 << target.layer)) != 0;

            if (string.IsNullOrWhiteSpace(tag))
                return false;

            return target.CompareTag(tag) ||
                   target.transform.root.CompareTag(tag);
        }
    }
}
