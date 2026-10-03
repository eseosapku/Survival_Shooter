using UnityEngine;

namespace Ricochet.Data
{
    /// <summary>Tuning numbers for one enemy type (one asset for Walker, one for Spitter).</summary>
    [CreateAssetMenu(menuName = "Ricochet/Enemy Stats", fileName = "EnemyStats_")]
    public class EnemyStats : ScriptableObject
    {
        [SerializeField, Min(1)] int hitsToKill = 3;
        [SerializeField, Min(0.05f)] float moveSpeed = 0.5f;
        [Tooltip("Walker: claw reach. Spitter: distance at which it stops and starts shooting.")]
        [SerializeField, Min(0.1f)] float attackRange = 0.8f;
        [SerializeField, Min(0f)] float damage = 10f;
        [SerializeField, Min(0.1f)] float attackCooldown = 1.5f;
        [SerializeField, Min(0)] int scoreValue = 100;
        [Tooltip("Only used by shooters.")]
        [SerializeField, Min(0.1f)] float projectileSpeed = 3f;

        public int HitsToKill => hitsToKill;
        public float MoveSpeed => moveSpeed;
        public float AttackRange => attackRange;
        public float Damage => damage;
        public float AttackCooldown => attackCooldown;
        public int ScoreValue => scoreValue;
        public float ProjectileSpeed => projectileSpeed;
    }
}
