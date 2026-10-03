using UnityEngine;

namespace Ricochet.Data
{
    /// <summary>Tuning numbers for the player's laser blaster and its bolts.</summary>
    [CreateAssetMenu(menuName = "Ricochet/Weapon Stats", fileName = "WeaponStats")]
    public class WeaponStats : ScriptableObject
    {
        [Header("Bolt")]
        [SerializeField, Min(0.5f)] float boltSpeed = 8f;
        [SerializeField, Min(0.1f)] float boltLifetime = 2.5f;
        [SerializeField, Min(0)] int maxBounces = 3;
        [SerializeField, Min(0.005f)] float boltRadius = 0.03f;

        [Header("Firing")]
        [SerializeField, Min(0.5f)] float shotsPerSecond = 5f;
        [SerializeField, Range(1f, 20f)] float spreadAngle = 8f;
        [SerializeField, Range(1, 4)] int baseSpreadTier = 1;

        [Header("Heat")]
        [SerializeField, Range(0f, 1f)] float heatPerShot = 0.08f;
        [SerializeField, Min(0f)] float coolPerSecond = 0.35f;
        [SerializeField, Min(0f)] float overheatLockTime = 1.5f;

        [Header("Ricochet score multiplier by bounce count (index = bounces)")]
        [SerializeField] float[] ricochetMultipliers = { 1f, 1.5f, 2f, 3f };

        public float BoltSpeed => boltSpeed;
        public float BoltLifetime => boltLifetime;
        public int MaxBounces => maxBounces;
        public float BoltRadius => boltRadius;
        public float FireInterval => 1f / shotsPerSecond;
        public float SpreadAngle => spreadAngle;
        public int BaseSpreadTier => baseSpreadTier;
        public float HeatPerShot => heatPerShot;
        public float CoolPerSecond => coolPerSecond;
        public float OverheatLockTime => overheatLockTime;

        public float MultiplierForBounces(int bounces)
        {
            if (ricochetMultipliers == null || ricochetMultipliers.Length == 0) return 1f;
            return ricochetMultipliers[Mathf.Clamp(bounces, 0, ricochetMultipliers.Length - 1)];
        }
    }
}
