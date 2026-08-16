using Failsafe.Player.Model;
using Failsafe.Scripts.Modifiebles;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace Failsafe.Scripts.Health
{
	[Serializable]
	public class PlayerHealth : IRestorableHealth
	{
		public event Action<float> OnHealthChanged = delegate { };
		public event Action OnDeath = delegate { };
		public event Action<float> OnStateRestored = delegate { };

		private bool _maxHealthAlreadyModified;

		[SerializeField] private ModifiableField<float> _maxHealth;

		[SerializeField] private float _health;

		public float MaxHealth => Mathf.Max(1f, _maxHealth);
		public float CurrentHealth => _health;
		[ShowInInspector] public bool IsDead => _health <= 0 || Mathf.Approximately(_health, 0f);

		public PlayerHealth(PlayerRuntimeParameters runtimeParameters)
		{
			_maxHealth = runtimeParameters.MaxHealth;

			_health = MaxHealth;
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
			if (!_maxHealthAlreadyModified) //Проверка на то что максимальное здоровье уже было модифицировано
			{
				AddMaxHealthModifier(modificator);
				_maxHealthAlreadyModified = true;
			}
		}

		public void AddMaxHealthModifier(IModificator<float> modificator)
		{
			ChangeMaxHealth(modificator, true);
		}

		public void RemoveMaxHealthModifier(IModificator<float> modificator)
		{
			ChangeMaxHealth(modificator, false);
		}

		private void ChangeMaxHealth(IModificator<float> modificator, bool add)
		{
			if (modificator == null)
				return;

			float previousMaxHealth = Mathf.Max(0.0001f, MaxHealth);
			float healthRatio = Mathf.Clamp01(_health / previousMaxHealth);

			if (add)
				_maxHealth.AddModificator(modificator);
			else
				_maxHealth.RemoveModificator(modificator);

			_health = Mathf.Clamp(MaxHealth * healthRatio, 0f, MaxHealth);
			OnHealthChanged.Invoke(_health);
		}

		public void RestoreState(float health)
		{
			_health = Mathf.Clamp(health, 0f, MaxHealth);
			OnStateRestored.Invoke(_health);
		}
	}
}
