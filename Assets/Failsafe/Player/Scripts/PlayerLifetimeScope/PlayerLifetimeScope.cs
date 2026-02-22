using Failsafe.Items;
using Failsafe.Player.Model;
using Failsafe.Player.Scripts;
using Failsafe.Player.Scripts.Interaction;
using Failsafe.Player.UI;
using Failsafe.Player.View;
using Failsafe.PlayerMovements;
using Failsafe.Scripts.Health;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.EffectSystem;

namespace Failsafe.Player
{
    /// <summary>
    /// Регистрация компонентов игрового персонажа
    /// </summary>
    public class PlayerLifetimeScope : LifetimeScope
    {
        [SerializeReference] private PlayerModelParameters _playerModelParameters;
        [SerializeReference] private PlayerMovementParameters _playerMovementParameters;
        [SerializeReference] private PlayerNoiseParameters _playerNoiseParameters;
        
        [SerializeField] private ScriptableObject[] _playerItemsData;
        [SerializeField] private PlayerView _playerView;
        [SerializeField] private InputActionAsset _inputActionAsset;
        [SerializeField] private DamageableComponent _damageable;
        [SerializeField] private Camera _playerCam;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_playerModelParameters);
            builder.RegisterInstance(_playerMovementParameters);
            builder.RegisterInstance(_playerNoiseParameters);
            builder.RegisterComponent(_playerView);
            builder.RegisterComponent(_damageable);
            builder.RegisterComponent(_inputActionAsset);

            var cc = _playerView != null ? _playerView.CharacterController : null;
            if (cc == null) Debug.LogError("[PlayerLifetimeScope] PlayerView.CharacterController не задан");
            builder.RegisterInstance(cc);

            var cam = _playerView != null ? _playerView.PlayerCamera?.GetComponent<Camera>() : null;
            builder.RegisterInstance(cam);

            builder.Register<InputHandler>(Lifetime.Scoped);
            builder.Register<IHealth, PlayerHealth>(Lifetime.Singleton).AsSelf()
                   .WithParameter(_playerModelParameters.MaxHealth);
            builder.Register<IStamina, PlayerStamina>(Lifetime.Singleton).AsSelf()
                   .WithParameter(_playerModelParameters.MaxStamina);
            
            builder.RegisterEntryPoint<PlayerDamageable>(Lifetime.Scoped);
            builder.RegisterEntryPoint<PlayerStaminaController>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<PlayerController>(Lifetime.Scoped).AsSelf();
            builder.Register<PlayerHandsContainer>(Lifetime.Scoped);
            builder.RegisterEntryPoint<PlayerHandsSystem>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<PlayerAnimationController>(Lifetime.Scoped);
            builder.RegisterEntryPoint<PlayerCameraController>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<PlayerUIController>();
            builder.RegisterComponentInHierarchy<PlayerCrosshairRaycaster>();
            builder.RegisterEntryPoint<PlayerUIPresenter>();
            
            // Регистрируем контроллер перемещения
            builder.Register<PlayerMovementController>(Lifetime.Scoped);

            // Регистрируем менеджер эффектов
            builder.RegisterEntryPoint<EffectManager>(Lifetime.Scoped)
                   .As<IEffectManager>()
                   .AsSelf();

            // --- ИСПРАВЛЕНИЕ: Регистрация сигнала ---
            // 1. Создаем сам сигнал
            builder.Register<PlayerNoiseSignal>(Lifetime.Scoped).WithParameter(transform);

            // 2. Регистрируем специальный коннектор как EntryPoint (он запустится на Start)
            // Это гарантирует, что SignalManager уже успеет проснуться
            builder.RegisterEntryPoint<PlayerSignalConnector>(Lifetime.Scoped);

            RegisterItems(builder);
        }

        private void RegisterItems(IContainerBuilder builder)
        {
            foreach (var itemData in _playerItemsData)
            {
                builder.RegisterInstance(itemData).As(itemData.GetType());
            }
            builder.Register<Stimpack>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<StasisGun>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<Adrenaline>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<Tushkan>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<Gorilla>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        }
    }

    /// <summary>
    /// Вспомогательный класс для безопасного связывания сигнала при старте игры
    /// </summary>
    public class PlayerSignalConnector : IStartable
    {
        private readonly PlayerNoiseSignal _noiseSignal;
        private readonly PlayerMovementController _movementController;

        public PlayerSignalConnector(PlayerNoiseSignal noiseSignal, PlayerMovementController movementController)
        {
            _noiseSignal = noiseSignal;
            _movementController = movementController;
        }

        public void Start()
        {
            // 1. Связываем контроллер движения с сигналом
            _movementController.SetPlayerNoiseSignal(_noiseSignal);

            // 2. Безопасно добавляем сигнал в глобальный менеджер
            if (SignalManager.Instance != null)
            {
                SignalManager.Instance.PlayerNoiseChanel.AddConstant(_noiseSignal);
                // Debug.Log("[PlayerSignalConnector] Шум игрока успешно подключен к SignalManager.");
            }
            else
            {
                Debug.LogError("CRITICAL: SignalManager не найден на сцене! Проверьте, что он добавлен и активен.");
            }
        }
    }
}