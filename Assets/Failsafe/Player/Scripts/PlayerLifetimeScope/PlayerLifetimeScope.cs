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
        
        // --- НОВОЕ: Ссылка на WeaponController ---
        [Header("Combat")]
        [SerializeField] private WeaponController _weaponController;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_playerModelParameters);
            builder.RegisterInstance(_playerMovementParameters);
            builder.RegisterInstance(_playerNoiseParameters);
            builder.RegisterComponent(_playerView);
            builder.RegisterComponent(_damageable);
            builder.RegisterComponent(_inputActionAsset);

            // --- НОВОЕ: Регистрируем WeaponController ---
            if (_weaponController == null) _weaponController = _playerView.GetComponent<WeaponController>();
            if (_weaponController != null)
            {
                builder.RegisterComponent(_weaponController);
            }
            else
            {
                Debug.LogError("[PlayerLifetimeScope] WeaponController не назначен и не найден на PlayerView!");
            }

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
            
            builder.Register<PlayerMovementController>(Lifetime.Scoped);

            builder.RegisterEntryPoint<EffectManager>(Lifetime.Scoped)
                   .As<IEffectManager>()
                   .AsSelf();

            builder.Register<PlayerNoiseSignal>(Lifetime.Scoped).WithParameter(transform);
            builder.RegisterEntryPoint<PlayerSignalConnector>(Lifetime.Scoped);

            RegisterItems(builder);
        }

        private void RegisterItems(IContainerBuilder builder)
        {
            foreach (var itemData in _playerItemsData)
            {
                builder.RegisterInstance(itemData).As(itemData.GetType());
            }

            // --- НОВОЕ: Регистрируем универсальный обработчик оружия ---
            // Он автоматически получит WeaponController и Camera через конструктор
            builder.Register<GunUsable>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // Остальные предметы
            builder.Register<Stimpack>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<StasisGun>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf(); // Если это старая реализация, можно убрать
            builder.Register<Adrenaline>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<Tushkan>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<Gorilla>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<EmpGrenade>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<FragGrenade>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<FireGrenade>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<StasisGrenade>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<ScanGrenade>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        }
    }

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
            _movementController.SetPlayerNoiseSignal(_noiseSignal);

            if (SignalManager.Instance != null)
            {
                SignalManager.Instance.PlayerNoiseChanel.AddConstant(_noiseSignal);
            }
            else
            {
                Debug.LogError("CRITICAL: SignalManager не найден на сцене! Проверьте, что он добавлен и активен.");
            }
        }
    }
}