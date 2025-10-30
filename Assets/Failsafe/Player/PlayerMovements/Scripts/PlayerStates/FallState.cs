using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.EffectSystem.Effects;
using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.Health;
using UnityEngine;

namespace Failsafe.PlayerMovements.States
{
    public class FallState : BehaviorState
    {
        private readonly InputHandler _input;
        private readonly CharacterController _cc;
        private readonly PlayerMovementController _pmc;
        private readonly PlayerMovementParameters _p;
        private readonly PlayerNoiseController _noise;
        private readonly IEffectManager _effects;
        private readonly IHealth _health;

        private float _fallProgress;
        private Vector3 _initialVelXZ;
        private Vector3 _startPos;

        private bool _flybyNoiseTriggered;
        private bool _wasGroundedLastFrame;

        private enum LandingKind { None, DamageOnly, MinorSlow, HeavyRecover }
        private LandingKind _landingDecision = LandingKind.None;

        public float FallHeight => _startPos.y - _cc.transform.position.y;

        // 👉 этим флагом пользуемся в переходах
        public bool ShouldRecover => _landingDecision == LandingKind.HeavyRecover;

        public FallState(
            InputHandler input,
            CharacterController characterController,
            PlayerMovementController movementController,
            PlayerMovementParameters movementParameters,
            PlayerNoiseController noiseController,
            IEffectManager effectManager,
            IHealth health)
        {
            _input   = input;
            _cc      = characterController;
            _pmc     = movementController;
            _p       = movementParameters;
            _noise   = noiseController;
            _effects = effectManager;
            _health  = health;
        }

        public override void Enter()
        {
            _fallProgress = 0f;
            _flybyNoiseTriggered = false;
            _landingDecision = LandingKind.None;

            var v = _pmc.Velocity;
            _initialVelXZ = new Vector3(v.x, 0f, v.z);
            _startPos = _cc.transform.position;

            _pmc.SetGravity(_p.InitialGravityStrength * _p.GravityForce * Vector3.down);

            _wasGroundedLastFrame = _pmc.IsGrounded || _cc.isGrounded;
        }

        public override void Update()
        {
            _fallProgress += Time.deltaTime;

            // Гравитация → 100%
            float gK = Mathf.Lerp(_p.InitialGravityStrength, 1f, _fallProgress / _p.TimeToFullGravityForce);
            Vector3 gravity = _p.GravityForce * gK * Vector3.down;

            // Управление в воздухе
            Vector3 air = _pmc.GetRelativeMovement(_input.MovementInput) * _p.AirMovementSpeed;

            _pmc.Move(_initialVelXZ + air);
            _pmc.SetGravity(gravity);

            // Fly-by шум (один раз)
            if (!_flybyNoiseTriggered && FallHeight > _p.FlybyNoiseHeight)
            {
                _noise.CreateNoise(_p.FlybyNoiseRadius, 2);
                _flybyNoiseTriggered = true;
            }

            // 🔎 Детект "только что приземлились" (без ожидания FixedUpdate)
            bool groundedNow = _pmc.IsGrounded || _cc.isGrounded;
            if (groundedNow && !_wasGroundedLastFrame && _landingDecision == LandingKind.None)
            {
                float h = FallHeight;

                // Урон — считаем сразу (если включён)
                if (_p.FallDamageEnabled && h >= _p.FallDamageStartHeight)
                {
                    float steps = 0f;
                    if (_p.FallDamageHeightStep > 0.0001f)
                        steps = Mathf.Max(0f, Mathf.Floor((h - _p.FallDamageStartHeight) / _p.FallDamageHeightStep));

                    float damage = _p.FallDamageBase + steps * _p.FallDamageStepAmount;
                    if (damage > 0.01f && _health != null)
                        _health.AddHealth(-damage);
                }

                // Выбор типа приземления (фиксируем решение)
                if (h >= _p.HeavyLandingHeight)
                    _landingDecision = LandingKind.HeavyRecover;
                else if (h >= _p.SlowMinorHeight)
                    _landingDecision = LandingKind.MinorSlow;
                else if (h >= _p.FallDamageStartHeight)
                    _landingDecision = LandingKind.DamageOnly;
                else
                    _landingDecision = LandingKind.None;

                // Мгновенно применяем MINOR-slow (уровень 2) — Recover сделает MAIN-slow позже
                if (_landingDecision == LandingKind.MinorSlow)
                {
                    var minor = new SlowMovementEffect(_pmc, _p.MinorSlowDuration, _p.MinorSlowMultiplier, unique: true);
                    _effects.ApplyEffect(minor);
                }

                // Базовый шум приземления
                _noise.CreateNoise(h, 2);
            }

            _wasGroundedLastFrame = groundedNow;
        }

        public override void Exit()
        {
            _pmc.SetGravityDefault();
            // Ничего больше здесь НЕ делаем: урон/минор слоу/шум уже обработаны в момент приземления.
        }
    }
}