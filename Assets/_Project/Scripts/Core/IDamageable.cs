using UnityEngine;

namespace Ricochet.Core
{
    /// <summary>Everything that can be hurt: the player and every enemy. Callers don't need to know which.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }

    /// <summary>Describes one hit. ScoreMultiplier carries the laser's ricochet bonus to the enemy.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public float ScoreMultiplier;
        public int Bounces;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, float scoreMultiplier = 1f, int bounces = 0)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            ScoreMultiplier = scoreMultiplier;
            Bounces = bounces;
        }
    }
}
