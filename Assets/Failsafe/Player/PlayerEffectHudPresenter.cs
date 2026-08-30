using System;
using System.Collections.Generic;
using Failsafe.Player.UI;
using Failsafe.Player.View;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Player.Scripts
{
    public sealed class PlayerEffectHudPresenter : IInitializable, IDisposable
    {
        private readonly PlayerEffectHudView _view;
        private readonly IEffectPresentationSource _effectSource;
        private readonly PlayerView _playerView;
        private readonly List<EffectPresentation> _activeEffectsBuffer = new();

        public PlayerEffectHudPresenter(
            PlayerEffectHudView view,
            IEffectPresentationSource effectSource,
            PlayerView playerView)
        {
            _view = view;
            _effectSource = effectSource;
            _playerView = playerView;
        }

        public void Initialize()
        {
            _effectSource.EffectAdded += OnEffectAdded;
            _effectSource.EffectRefreshed += OnEffectRefreshed;
            _effectSource.EffectRemoved += OnEffectRemoved;

            _effectSource.GetActiveEffects(_activeEffectsBuffer);

            foreach (EffectPresentation presentation in _activeEffectsBuffer)
                TryShow(presentation);

            _activeEffectsBuffer.Clear();
        }

        public void Dispose()
        {
            _effectSource.EffectAdded -= OnEffectAdded;
            _effectSource.EffectRefreshed -= OnEffectRefreshed;
            _effectSource.EffectRemoved -= OnEffectRemoved;

            _view.ClearImmediate();
            _activeEffectsBuffer.Clear();
        }

        private void OnEffectAdded(EffectPresentation presentation)
        {
            TryShow(presentation);
        }

        private void OnEffectRefreshed(EffectPresentation presentation)
        {
            if (ShouldDisplay(presentation))
                _view.Refresh(presentation);
        }

        private void OnEffectRemoved(EffectPresentation presentation)
        {
            _view.Hide(presentation);
        }

        private void TryShow(EffectPresentation presentation)
        {
            if (ShouldDisplay(presentation))
                _view.Show(presentation);
        }

        private bool ShouldDisplay(EffectPresentation presentation)
        {
            if (presentation?.Definition == null ||
                !presentation.Definition.ShowInHud ||
                presentation.Target == null ||
                _playerView == null)
            {
                return false;
            }

            Transform playerTransform = _playerView.PlayerTransform != null
                ? _playerView.PlayerTransform
                : _playerView.transform;

            return presentation.Target.transform.root == playerTransform.root;
        }
    }
}
