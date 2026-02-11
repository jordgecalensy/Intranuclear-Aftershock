using Failsafe.Items;
using Failsafe.Player.Model;
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
    /// <para/>Дочерний скоуп к <see cref="Failsafe.GameSceneServices.GameSceneLifetimeScope"/>
    /// </summary>
    public class PlayerLifetimeScope : LifetimeScope
    {
        [SerializeReference] private PlayerModelParameters _playerModelParameters;
        [SerializeReference] private PlayerMovementParameters _playerMovementParameters;
        [SerializeReference] private PlayerNoiseParameters _playerNoiseParameters;
        // Параметры предметов
        [SerializeField] private ScriptableObject[] _playerItemsData;

        [SerializeField] private PlayerView _playerView;
        [SerializeField] private InputActionAsset _inputActionAsset;
        [SerializeField] private DamageableComponent _damageable;

        [SerializeField] private Camera _playerCam;

      
protected override void Configure(IContainerBuilder builder)
{
    // твои регистрации параметров/компонентов
    builder.RegisterInstance(_playerModelParameters);
    builder.RegisterInstance(_playerMovementParameters);
    builder.RegisterInstance(_playerNoiseParameters);
    builder.RegisterComponent(_playerView);
    builder.RegisterComponent(_damageable);
    builder.RegisterComponent(_inputActionAsset);

    // Берём нужные зависимости для PlayerMovementController из PlayerView
    var cc = _playerView != null ? _playerView.CharacterController : null;
    if (cc == null) Debug.LogError("[PlayerLifetimeScope] PlayerView.CharacterController не задан");
    builder.RegisterInstance(cc);

    // (Если используешь камеру в других местах)
    var cam = _playerView != null ? _playerView.PlayerCamera?.GetComponent<Camera>() : null;
    if (cam == null) Debug.LogWarning("[PlayerLifetimeScope] PlayerCamera не найден (не критично для движения)");
    builder.RegisterInstance(cam);

    // остальное как у тебя...
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

    builder.Register<PlayerMovementController>(Lifetime.Scoped);  

    // ✅ Менеджер эффектов (он же ITickable через EntryPoint)
    builder.RegisterEntryPoint<EffectManager>(Lifetime.Scoped)
           .As<IEffectManager>()
           .AsSelf();

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
            builder.Register<NewTestGranade>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        }
    }
}
