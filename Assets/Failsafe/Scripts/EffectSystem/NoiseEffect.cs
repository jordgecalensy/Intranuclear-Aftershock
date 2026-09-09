using Failsafe.PlayerMovements.Controllers;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public class NoiseEffect : Effect
    {
        private readonly PlayerNoiseController _noiseController;
        private readonly SignalManager _signalManager;
        private readonly Transform _sourceTransform;

        private readonly float _strength;
        private readonly float _noiseDuration;
        private readonly bool _log;

        public NoiseEffect(
            PlayerNoiseController noiseController,
            float strength,
            float noiseDuration,
            bool log)
        {
            _noiseController = noiseController;
            _strength = Mathf.Max(0f, strength);
            _noiseDuration = Mathf.Max(0.01f, noiseDuration);
            _log = log;

            _duration = 0f;
            IsUniqueEffect = false;
        }

        public NoiseEffect(
            SignalManager signalManager,
            Transform sourceTransform,
            float strength,
            float noiseDuration,
            bool log)
        {
            _signalManager = signalManager;
            _sourceTransform = sourceTransform;
            _strength = Mathf.Max(0f, strength);
            _noiseDuration = Mathf.Max(0.01f, noiseDuration);
            _log = log;

            _duration = 0f;
            IsUniqueEffect = false;
        }

        public override void ApplyEffect()
        {
            if (_strength <= 0f)
            {
                if (_log)
                    EffectLog.Warning(EffectLog.Feedback, "[NoiseEffect] Noise strength <= 0. Noise was not created.");

                return;
            }

            if (_noiseController != null)
            {
                _noiseController.CreateNoise(
                    _strength,
                    _noiseDuration);

                if (_log)
                {
                    EffectLog.Info(EffectLog.Feedback,
                        $"[NoiseEffect] Create noise through PlayerNoiseController. Strength: {_strength:0.00}, duration: {_noiseDuration:0.00}s");
                }

                return;
            }

            if (_signalManager != null && _sourceTransform != null)
            {
                _signalManager.PlayerNoiseChanel.Add(
                    _sourceTransform.position,
                    _strength,
                    _noiseDuration);

                if (_log)
                {
                    EffectLog.Info(EffectLog.Feedback,
                        $"[NoiseEffect] Create noise through SignalManager fallback at {_sourceTransform.position}. Strength: {_strength:0.00}, duration: {_noiseDuration:0.00}s",
                        _sourceTransform);
                }

                return;
            }

            if (_log)
                EffectLog.Warning(EffectLog.Feedback, "[NoiseEffect] Noise was not created: no PlayerNoiseController or SignalManager fallback.");
        }

        public override void ClearEffect()
        {
        }
    }
}