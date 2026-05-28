using Failsafe.Scripts.EffectSystem.Targets;
using FMODUnity;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Effects
{
    public sealed class StasisEffect : Effect, IReapplicableEffect
    {
        private readonly Rigidbody _rb;
        private readonly Enemy _enemy;
        private readonly DamageObstacle _obstacle;
        private readonly IStasisResponder _responder;
        private readonly Renderer[] _renderers;
        private readonly bool _restoreVelocityAfterEnd;
        private readonly Material _stasisMaterial;
        private readonly EventReference _endSound;
        private readonly GameObject _soundObject;

        private Vector3 _savedVelocity;
        private Vector3 _savedAngularVelocity;
        private bool _savedIsKinematic;
        private RigidbodyConstraints _savedConstraints;
        private Material[][] _originalMaterials;

        public StasisEffect(
            Rigidbody rb,
            Enemy enemy,
            DamageObstacle obstacle,
            IStasisResponder responder,
            Renderer[] renderers,
            GameObject soundObject,
            float duration,
            bool restoreVelocityAfterEnd,
            Material stasisMaterial,
            EventReference endSound)
        {
            _rb = rb;
            _enemy = enemy;
            _obstacle = obstacle;
            _responder = responder;
            _renderers = renderers;
            _soundObject = soundObject;
            _restoreVelocityAfterEnd = restoreVelocityAfterEnd;
            _stasisMaterial = stasisMaterial;
            _endSound = endSound;

            _duration = Mathf.Max(0f, duration);
            IsUniqueEffect = true;
        }

        public override void ApplyEffect()
        {
            _responder?.OnStasisStart();

            if (_enemy != null)
                _enemy.DisableState(_duration);

            if (_obstacle != null)
                _obstacle.SetStasis(true);

            FreezeRigidbody();
            ApplyVisual();
        }

        public override void ClearEffect()
        {
            RestoreRigidbody();

            if (_obstacle != null)
                _obstacle.SetStasis(false);

            _responder?.OnStasisEnd();

            RemoveVisual();

            if (_soundObject != null && !_endSound.IsNull)
                SoundUtils3D.Play(_soundObject, _endSound);
        }

        public void OnReapply(Effect newEffect)
        {
            if (newEffect is not StasisEffect reapplied)
                return;

            _duration = Mathf.Max(ElapsedAt - Time.time, 0f) + reapplied._duration;
        }

        private void FreezeRigidbody()
        {
            if (_rb == null)
                return;

            _savedVelocity = _rb.linearVelocity;
            _savedAngularVelocity = _rb.angularVelocity;
            _savedIsKinematic = _rb.isKinematic;
            _savedConstraints = _rb.constraints;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        private void RestoreRigidbody()
        {
            if (_rb == null)
                return;

            _rb.isKinematic = _savedIsKinematic;
            _rb.constraints = _savedConstraints;

            if (_restoreVelocityAfterEnd)
            {
                _rb.linearVelocity = _savedVelocity;
                _rb.angularVelocity = _savedAngularVelocity;
            }
        }

        private void ApplyVisual()
        {
            if (_stasisMaterial == null || _renderers == null || _renderers.Length == 0)
                return;

            _originalMaterials = new Material[_renderers.Length][];

            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];

                if (renderer == null)
                    continue;

                _originalMaterials[i] = renderer.materials;

                var newMaterials = new Material[_originalMaterials[i].Length + 1];
                _originalMaterials[i].CopyTo(newMaterials, 0);
                newMaterials[newMaterials.Length - 1] = _stasisMaterial;

                renderer.materials = newMaterials;
            }
        }

        private void RemoveVisual()
        {
            if (_renderers == null || _originalMaterials == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null || _originalMaterials[i] == null)
                    continue;

                _renderers[i].materials = _originalMaterials[i];
            }
        }
    }
}