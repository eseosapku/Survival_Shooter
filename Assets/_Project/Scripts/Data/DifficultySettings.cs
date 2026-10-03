using UnityEngine;

namespace Ricochet.Data
{
    /// <summary>
    /// All difficulty-dependent numbers in one asset. Changing difficulty = swapping which asset is used.
    /// Spawn interval and Spitter chance are interpolated from "start" to "end" over the round.
    /// </summary>
    [CreateAssetMenu(menuName = "Ricochet/Difficulty Settings", fileName = "Difficulty_")]
    public class DifficultySettings : ScriptableObject
    {
        [SerializeField] string displayName = "Normal";

        [Header("Round")]
        [SerializeField, Min(10f)] float roundLength = 150f;

        [Header("Spawning")]
        [SerializeField, Min(0.2f)] float startSpawnInterval = 2.8f;
        [SerializeField, Min(0.2f)] float minSpawnInterval = 1.2f;
        [SerializeField, Min(1)] int maxEnemiesAlive = 9;
        [SerializeField, Range(0f, 1f)] float spitterChanceStart = 0.25f;
        [SerializeField, Range(0f, 1f)] float spitterChanceEnd = 0.45f;

        [Header("Balance")]
        [SerializeField, Min(0.1f)] float enemyDamageMultiplier = 1f;
        [SerializeField, Min(1f)] float playerMaxHealth = 100f;

        public string DisplayName => displayName;
        public float RoundLength => roundLength;
        public int MaxEnemiesAlive => maxEnemiesAlive;
        public float EnemyDamageMultiplier => enemyDamageMultiplier;
        public float PlayerMaxHealth => playerMaxHealth;

        /// <param name="progress01">0 at round start, 1 at round end.</param>
        public float SpawnIntervalAt(float progress01) => Mathf.Lerp(startSpawnInterval, minSpawnInterval, progress01);

        public float SpitterChanceAt(float progress01) => Mathf.Lerp(spitterChanceStart, spitterChanceEnd, progress01);
    }
}
