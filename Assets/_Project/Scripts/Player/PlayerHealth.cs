using System;
using Ricochet.Core;
using UnityEngine;

namespace Ricochet.Player
{
    /// <summary>
    /// The player's health. Implements IDamageable so enemies and acid globs can hurt it
    /// without knowing it is the player. Raises events that the HUD and feedback listen to.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;

        bool _invulnerable = true;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsAlive => Current > 0f;

        /// <summary>(current, max)</summary>
        public event Action<float, float> HealthChanged;
        public event Action<DamageInfo> Damaged;
        public event Action<float> Healed;
        public event Action Died;

        void Awake() => Current = maxHealth;

        public void ResetHealth(float newMax)
        {
            maxHealth = newMax;
            Current = newMax;
            HealthChanged?.Invoke(Current, maxHealth);
        }

        /// <summary>Damage is ignored outside of play (menus, countdown, death sequence).</summary>
        public void SetInvulnerable(bool invulnerable) => _invulnerable = invulnerable;

        public void TakeDamage(DamageInfo info)
        {
            if (_invulnerable || !IsAlive || info.Amount <= 0f) return;

            Current = Mathf.Max(0f, Current - info.Amount);
            HealthChanged?.Invoke(Current, maxHealth);
            Damaged?.Invoke(info);

            if (Current <= 0f) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            HealthChanged?.Invoke(Current, maxHealth);
            Healed?.Invoke(amount);
        }
    }
}
