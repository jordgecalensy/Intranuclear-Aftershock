using System;
using System.Collections.Generic;
using Failsafe.Scripts.Damage.Implementation;
using FMODUnity;
using UnityEngine;

namespace Failsafe.Player.View
{
    /// <summary>
    /// Представление персонажа
    /// </summary>
    /// <remarks>
    /// Должен содержать компоненты специфичные для движка Unity: Рендер, Анимации, Звук.
    /// Логика должна быть вынесена в отдельные Модели и Контроллеры
    /// </remarks>
    public class PlayerView : MonoBehaviour
    {
        private const string EnemySensorPointPrefix = "Sensor_Point_";
        private const string EnemyChestPointName = "Sensor_Point_Chest";

        private readonly List<Transform> _enemySensorTargets = new();
        private Transform _enemySensorTargetRoot;
        private Transform _enemyChestTarget;

        /// <summary>
        /// Игровой персонаж
        /// </summary>
        public Transform PlayerTransform;
        /// <summary>
        /// Голова модели персонажа
        /// </summary>
        public Transform PlayerModelHead;
        /// <summary>
        /// Голова рига персонажа
        /// </summary>
        /// <remarks>
        /// Задает поворот камеры и головы модели
        /// </remarks>
        public Transform PlayerRigHead;
        /// <summary>
        /// Камера персонажа
        /// </summary>
        public Transform PlayerCamera;
        /// <summary>
        /// Тело персонажа
        /// </summary>
        public Transform Body;
        /// <summary>
        /// Место для предмета в правой руке
        /// </summary>
        public Transform RightHandItemPlace;

        public Animator Animator;
        public WeaponController WeaponController;

        /// <summary>
        /// Точка захвата
        /// </summary>
        public Transform PlayerGrabPoint;

        public CharacterController CharacterController;

        public EventReference FootstepEvent;

        public bool TryGetEnemySensorTargets(
            out Transform targetRoot,
            out Transform chestTarget,
            out IReadOnlyList<Transform> sensorTargets)
        {
            targetRoot = PlayerTransform != null ? PlayerTransform : transform;

            if (!HasValidEnemySensorTargetCache(targetRoot))
                RebuildEnemySensorTargetCache(targetRoot);

            chestTarget = _enemyChestTarget;
            sensorTargets = _enemySensorTargets;
            return _enemySensorTargets.Count > 0;
        }

        private bool HasValidEnemySensorTargetCache(Transform targetRoot)
        {
            if (_enemySensorTargetRoot != targetRoot ||
                _enemySensorTargets.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _enemySensorTargets.Count; i++)
            {
                if (_enemySensorTargets[i] == null)
                    return false;
            }

            return true;
        }

        private void RebuildEnemySensorTargetCache(Transform targetRoot)
        {
            _enemySensorTargetRoot = targetRoot;
            _enemyChestTarget = null;
            _enemySensorTargets.Clear();

            if (targetRoot == null)
                return;

            Transform[] descendants =
                targetRoot.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate != null &&
                    string.Equals(
                        candidate.name,
                        EnemyChestPointName,
                        StringComparison.Ordinal))
                {
                    _enemyChestTarget = candidate;
                    _enemySensorTargets.Add(candidate);
                    break;
                }
            }

            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate == null ||
                    candidate == _enemyChestTarget ||
                    !candidate.name.StartsWith(
                        EnemySensorPointPrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                _enemySensorTargets.Add(candidate);
            }
        }


        void OnValidate()
        {
            if (PlayerTransform == null)
                Debug.LogWarning($"Не задан компонент {nameof(PlayerView)}.{nameof(PlayerTransform)}");
            if (PlayerModelHead == null)
                Debug.LogWarning($"Не задан компонент {nameof(PlayerView)}.{nameof(PlayerModelHead)}");
            if (PlayerRigHead == null)
                Debug.LogWarning($"Не задан компонент {nameof(PlayerView)}.{nameof(PlayerRigHead)}");
            if (PlayerCamera == null)
                Debug.LogWarning($"Не задан компонент {nameof(PlayerView)}.{nameof(PlayerCamera)}");
            if (RightHandItemPlace == null)
                Debug.LogWarning($"Не задан компонент {nameof(PlayerView)}.{nameof(RightHandItemPlace)}");
            if (Animator == null)
                Debug.LogWarning($"Не задан компонент {nameof(PlayerView)}.{nameof(Animator)}");
            if (PlayerGrabPoint == null)
                Debug.LogWarning($"Не задан компонент {nameof(PlayerView)}.{nameof(PlayerGrabPoint)}");
            if (CharacterController == null)
                Debug.LogWarning($"Не задан компонент {nameof(PlayerView)}.{nameof(CharacterController)}");
            if (FootstepEvent.IsNull)
                Debug.LogWarning($"Не задан компонент {nameof(PlayerView)}.{nameof(FootstepEvent)}");

        }
    }

}
