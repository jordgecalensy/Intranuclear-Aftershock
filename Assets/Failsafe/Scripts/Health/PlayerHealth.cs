using Failsafe.Scripts.Modifiebles;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace Failsafe.Scripts.Health
{
	[Serializable]
	public class PlayerHealth : IHealth
	{
		public event Action<float> OnHealthChanged = delegate { };
		public event Action OnDeath = delegate { };

		[SerializeField] private ModifiableField<float> _maxHealth;

		[SerializeField] private float _health;

		public float MaxHealth => _maxHealth;
		public float CurrentHealth => _health;
		[ShowInInspector] public bool IsDead => _health <= 0 || Mathf.Approximately(_health, 0f);

		public PlayerHealth(float maxHealth)
		{
			this._maxHealth = maxHealth;

			_health = maxHealth;
		}

		public void AddHealth(float toAdd)
		{
			if (IsDead)
			{
				return;
			}

			_health = Mathf.Clamp(_health + toAdd, 0f, MaxHealth);

			OnHealthChanged.Invoke(_health);

			if (IsDead)
			{
				OnDeath();
			}
		}

		public void ModifyMaxHealth(AdderFloat modificator)
		{
			_maxHealth.AddModificator(modificator);
		}
	}
}