using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Resolves the shared effect service without searching every frame.
    /// </summary>
    public sealed class EffectApplicationServiceResolver
    {
        private const float RetryDelay = 1f;

        private readonly GameObject _owner;
        private float _nextAttemptTime;

        public IEffectApplicationService Service { get; private set; }

        public EffectApplicationServiceResolver(
            GameObject owner,
            IEffectApplicationService initialService = null)
        {
            _owner = owner != null
                ? owner
                : throw new ArgumentNullException(nameof(owner));
            Service = initialService;
        }

        public void Set(IEffectApplicationService service)
        {
            if (service != null)
                Service = service;
        }

        public void TryResolve(float currentTime, bool force)
        {
            if (Service != null)
                return;

            if (!force && currentTime < _nextAttemptTime)
                return;

            _nextAttemptTime = currentTime + RetryDelay;

            LifetimeScope scope =
                _owner.GetComponentInParent<LifetimeScope>();

            if (scope == null)
                scope = LifetimeScope.Find<LifetimeScope>(_owner.scene);

            if (scope == null || scope.Container == null)
                return;

            try
            {
                Service =
                    scope.Container.Resolve<IEffectApplicationService>();
            }
            catch
            {
                Service = null;
            }
        }
    }
}
