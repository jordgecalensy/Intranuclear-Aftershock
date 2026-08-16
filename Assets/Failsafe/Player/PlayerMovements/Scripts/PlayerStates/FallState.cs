using Failsafe.PlayerMovements.Controllers;
using Failsafe.Scripts.Damage;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.EffectSystem.Effects;
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

        private enum LandingKind
        {
            None,
            DamageOnly,
            MinorSlow,
            HeavyRecover
        }

        private LandingKind _landingDecision = LandingKind.None;

        public float FallHeight => _startPos.y - _cc.transform.position.y;

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
            _input = input;
            _cc = characterController;
            _pmc = movementController;
            _p = movementParameters;
            _noise = noiseController;
            _effects = effectManager;
            _health = health;
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

            float gravityMultiplier = Mathf.Lerp(
                _p.InitialGravityStrength,
                1f,
                _fallProgress / _p.TimeToFullGravityForce);

            Vector3 gravity = _p.GravityForce * gravityMultiplier * Vector3.down;

            Vector3 air = _pmc.GetRelativeMovement(_input.MovementInput) * _p.AirMovementSpeed;

            _pmc.Move(_initialVelXZ + air);
            _pmc.SetGravity(gravity);

            if (!_flybyNoiseTriggered && FallHeight > _p.FlybyNoiseHeight)
            {
                _noise.CreateNoise(_p.FlybyNoiseRadius, 2);
                _flybyNoiseTriggered = true;
            }

            bool groundedNow = _pmc.IsGrounded || _cc.isGrounded;

            if (groundedNow && !_wasGroundedLastFrame && _landingDecision == LandingKind.None)
            {
                float height = FallHeight;

                if (_p.FallDamageEnabled && height >= _p.FallDamageStartHeight)
                {
                    float steps = 0f;

                    if (_p.FallDamageHeightStep > 0.0001f)
                    {
                        steps = Mathf.Max(
                            0f,
                            Mathf.Floor((height - _p.FallDamageStartHeight) / _p.FallDamageHeightStep));
                    }

                    float damage = _p.FallDamageBase + steps * _p.FallDamageStepAmount;

                    if (damage > 0.01f && _health != null)
                    {
                        var target = new DamageTarget(
                            null,
                            _health,
                            _cc.gameObject);

                        var damageInfo = new DamageInfo(
                            damage,
                            DamageType.Environment,
                            DamageApplicationKind.Fall,
                            point: _cc.transform.position,
                            direction: Vector3.down);

                        DamageResistanceUtility.ApplyDamage(target, damageInfo);
                    }
                }

                if (height >= _p.HeavyLandingHeight)
                    _landingDecision = LandingKind.HeavyRecover;
                else if (height >= _p.SlowMinorHeight)
                    _landingDecision = LandingKind.MinorSlow;
                else if (height >= _p.FallDamageStartHeight)
                    _landingDecision = LandingKind.DamageOnly;
                else
                    _landingDecision = LandingKind.None;

                if (_landingDecision == LandingKind.MinorSlow)
                {
                    var minorSlow = new SpeedMultiplierEffect(
                        _pmc,
                        _p.MinorSlowDuration,
                        _p.MinorSlowMultiplier,
                        SpeedStackPolicy.Strongest);

                    _effects.ApplyEffect(minorSlow);
                }

                _noise.CreateNoise(height, 2);
            }

            _wasGroundedLastFrame = groundedNow;
        }

        public override void Exit()
        {
            _pmc.SetGravityDefault();
        }
    }
}
