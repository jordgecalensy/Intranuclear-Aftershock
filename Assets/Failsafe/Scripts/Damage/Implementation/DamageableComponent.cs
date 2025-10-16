using System;
using UnityEngine;

namespace Failsafe.Scripts.Damage.Implementation
{
	public class DamageableComponent : MonoBehaviour, IDamageable
	{
		public event Action<IDamage> OnTakeDamage = delegate { };
		
		public void TakeDamage(IDamage damage)
		{
			OnTakeDamage?.Invoke(damage);
		}
	}
}