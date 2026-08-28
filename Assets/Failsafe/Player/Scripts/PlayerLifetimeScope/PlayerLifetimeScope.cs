using Failsafe.Items;
using Failsafe.Player.Model;
using Failsafe.Player.Scripts;
using Failsafe.Player.Scripts.Interaction;
using Failsafe.Player.UI;
using Failsafe.Player.View;
using Failsafe.PlayerMovements;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Damage.Providers;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.Health;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Player
{
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

        [Header("Combat")]
        [SerializeField] private WeaponController _weaponController;

        protected override void Configure(IContainerBuilder builder)
        {
            var runtimeParameters = new PlayerRuntimeParameters(_playerModelParameters);

            builder.RegisterInstance(_playerModelParameters);
            builder.RegisterInstance(_playerMovementParameters);
            builder.RegisterInstance(_playerNoiseParameters);
            builder.RegisterInstance(runtimeParameters);

            builder.RegisterComponent(_playerView);
            builder.RegisterComponent(_damageable);
            builder.RegisterComponent(_inputActionAsset);

            if (_weaponController == null && _playerView != null)
                _weaponController = _playerView.GetComponent<WeaponController>();

            if (_weaponController != null)
                builder.RegisterComponent(_weaponController);
            else
                Debug.LogError("[PlayerLifetimeScope] WeaponController не назначен и не найден на PlayerView!");

            CharacterController characterController = _playerView != null
                ? _playerView.CharacterController
                : null;

            if (characterController == null)
                Debug.LogError("[PlayerLifetimeScope] PlayerView.CharacterController не задан.");

            builder.RegisterInstance(characterController);

            Camera camera = _playerView != null && _playerView.PlayerCamera != null
                ? _playerView.PlayerCamera.GetComponent<Camera>()
                : _playerCam;

            builder.RegisterInstance(camera);

            builder.Register<InputHandler>(Lifetime.Scoped);

            builder.Register<PlayerHealth>(Lifetime.Singleton)
                .As<IHealth>()
                .As<IRestorableHealth>()
                .AsSelf()
                .WithParameter(runtimeParameters.MaxHealth);

            builder.Register<PlayerStamina>(Lifetime.Singleton)
                .As<IStamina>()
                .As<IRestorableStamina>()
                .AsSelf();

            builder.Register<FlatDamageProvider>(Lifetime.Scoped)
                .As<IDamageProvider>();

            builder.Register<DamageInfoProvider>(Lifetime.Scoped)
                .As<IDamageProvider>();

            builder.Register<DamageService>(Lifetime.Scoped)
                .As<IDamageService>()
                .AsSelf();

            builder.RegisterEntryPoint<PlayerDamageable>(Lifetime.Scoped);

            builder.RegisterEntryPoint<PlayerStaminaController>(Lifetime.Scoped)
                .AsSelf();

            builder.RegisterEntryPoint<PlayerHealthRegenerationController>(Lifetime.Scoped);

            builder.RegisterEntryPoint<PlayerController>(Lifetime.Scoped)
                .AsSelf();

            builder.Register<PlayerHandsContainer>(Lifetime.Scoped);

            builder.RegisterEntryPoint<PlayerHandsSystem>(Lifetime.Scoped)
                .AsSelf();

            builder.RegisterEntryPoint<PlayerAnimationController>(Lifetime.Scoped);

            builder.RegisterEntryPoint<PlayerCameraController>(Lifetime.Scoped);

            builder.RegisterComponentInHierarchy<PlayerUIController>();

            builder.RegisterComponentInHierarchy<PlayerCrosshairRaycaster>();
            builder.RegisterComponentInHierarchy<PhysicsInteraction>();

            builder.RegisterEntryPoint<PlayerUIPresenter>();
            
            builder.RegisterComponentInHierarchy<PlayerControlBlocker>();
            builder.RegisterComponentInHierarchy<global::CursorLock>();

            builder.Register<PlayerMovementController>(Lifetime.Scoped);

            builder.RegisterEntryPoint<PlayerRunSaveParticipant>(Lifetime.Scoped);
            builder.RegisterEntryPoint<PlayerRunTerminationHandler>(Lifetime.Scoped);

            DeathScreenView deathScreenView =
                GetComponentInChildren<DeathScreenView>(true);

            if (deathScreenView != null)
            {
                builder.RegisterComponent(deathScreenView);
                builder.RegisterEntryPoint<DeathScreenPresenter>(Lifetime.Scoped);
            }
            else
            {
                RunSaveLog.Warning(
                    RunSaveLog.DeathScreen,
                    $"{nameof(DeathScreenView)} is not configured on the player prefab. " +
                    "The run will still end on death, but the death screen will not be shown.",
                    this);
            }

            builder.RegisterEntryPoint<PlayerRunCheckpointSafetyPolicy>(Lifetime.Scoped)
                .As<IRunCheckpointSafetyPolicy>()
                .AsSelf();

            builder.RegisterEntryPoint<RunAutosaveController>(Lifetime.Scoped);

            builder.RegisterEntryPoint<EffectManager>(Lifetime.Scoped)
                .As<IEffectManager>()
                .AsSelf();

            builder.RegisterEntryPoint<SelectedEngineerPerkApplier>(Lifetime.Scoped);

            builder.Register<PlayerNoiseSignal>(Lifetime.Scoped)
                .WithParameter(transform);

            builder.RegisterEntryPoint<PlayerSignalConnector>(Lifetime.Scoped);
            
            RegisterItems(builder);
        }

        private void RegisterItems(IContainerBuilder builder)
        {
            foreach (ScriptableObject itemData in _playerItemsData)
                builder.RegisterInstance(itemData).As(itemData.GetType());

            builder.Register<GunUsable>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<Stimpack>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<StasisGun>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<Adrenaline>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<Tushkan>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<Gorilla>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<EmpGrenade>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<FragGrenade>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<FireGrenade>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<StasisGrenade>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<ScanGrenade>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<Card>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<Circular>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<Wrench>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

        }
    }

    public class PlayerSignalConnector : IStartable
    {
        private readonly PlayerNoiseSignal _noiseSignal;
        private readonly PlayerMovementController _movementController;

        public PlayerSignalConnector(
            PlayerNoiseSignal noiseSignal,
            PlayerMovementController movementController)
        {
            _noiseSignal = noiseSignal;
            _movementController = movementController;
        }

        public void Start()
        {
            _movementController.SetPlayerNoiseSignal(_noiseSignal);

            if (SignalManager.Instance != null)
                SignalManager.Instance.PlayerNoiseChanel.AddConstant(_noiseSignal);
            else
                Debug.LogError("CRITICAL: SignalManager не найден на сцене. Проверьте, что он добавлен и активен.");
        }
    }
}
