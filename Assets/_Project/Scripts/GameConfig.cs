using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ricochet
{
    /// <summary>
    /// ONE ScriptableObject with every tuning number and asset reference in the game (data-driven design).
    /// Balancing the game = editing this asset, no code changes. Difficulty = picking one of the three entries.
    /// </summary>
    [CreateAssetMenu(menuName = "Ricochet/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Difficulties (0 Easy, 1 Normal, 2 Hard)")]
        [SerializeField] DifficultySettings[] difficulties = new DifficultySettings[3];

        [Header("Enemies")]
        [SerializeField] EnemyStats walker = new EnemyStats();
        [SerializeField] EnemyStats spitter = new EnemyStats();
        [SerializeField] Enemy walkerPrefab;
        [SerializeField] Enemy spitterPrefab;
        [SerializeField] Projectile acidPrefab;

        [Header("Player weapon")]
        [SerializeField] WeaponStats weapon = new WeaponStats();
        [SerializeField] Projectile laserBoltPrefab;

        [Header("Ability cards & gadgets")]
        [SerializeField] CardSettings cards = new CardSettings();
        [Tooltip("Order: MultiShot, Mirror, Prism, Freeze, MedKit")]
        [SerializeField] Transform[] cardPrefabs = new Transform[5];
        [SerializeField] Transform mirrorPrefab;
        [SerializeField] Transform prismPrefab;

        [Header("Audio")]
        [SerializeField] List<SoundEntry> sounds = new List<SoundEntry>();

        public int DifficultyCount => difficulties.Length;
        public DifficultySettings GetDifficulty(int index) => difficulties[Mathf.Clamp(index, 0, difficulties.Length - 1)];
        public EnemyStats StatsFor(EnemyType type) => type == EnemyType.Walker ? walker : spitter;
        public Enemy PrefabFor(EnemyType type) => type == EnemyType.Walker ? walkerPrefab : spitterPrefab;
        public Projectile AcidPrefab => acidPrefab;
        public WeaponStats Weapon => weapon;
        public Projectile LaserBoltPrefab => laserBoltPrefab;
        public CardSettings Cards => cards;
        public Transform[] CardPrefabs => cardPrefabs;
        public Transform MirrorPrefab => mirrorPrefab;
        public Transform PrismPrefab => prismPrefab;
        public IReadOnlyList<SoundEntry> Sounds => sounds;

#if UNITY_EDITOR
        public void EditorSetSounds(List<SoundEntry> entries) => sounds = entries;
#endif
    }

    [Serializable]
    public class DifficultySettings
    {
        [SerializeField] string displayName = "Normal";
        [SerializeField, Min(10f)] float roundLength = 150f;
        [SerializeField, Min(0.2f)] float startSpawnInterval = 2.8f;
        [SerializeField, Min(0.2f)] float minSpawnInterval = 1.2f;
        [SerializeField, Min(1)] int maxEnemiesAlive = 9;
        [SerializeField, Range(0f, 1f)] float spitterChanceStart = 0.25f;
        [SerializeField, Range(0f, 1f)] float spitterChanceEnd = 0.45f;
        [SerializeField, Min(0.1f)] float enemyDamageMultiplier = 1f;
        [SerializeField, Min(1f)] float playerMaxHealth = 100f;

        public string DisplayName => displayName;
        public float RoundLength => roundLength;
        public int MaxEnemiesAlive => maxEnemiesAlive;
        public float EnemyDamageMultiplier => enemyDamageMultiplier;
        public float PlayerMaxHealth => playerMaxHealth;

        /// <summary>Spawn interval shrinks linearly over the round (progress 0 → 1).</summary>
        public float SpawnIntervalAt(float progress01) => Mathf.Lerp(startSpawnInterval, minSpawnInterval, progress01);

        /// <summary>Spitter chance grows linearly over the round.</summary>
        public float SpitterChanceAt(float progress01) => Mathf.Lerp(spitterChanceStart, spitterChanceEnd, progress01);
    }

    [Serializable]
    public class EnemyStats
    {
        [SerializeField, Min(1)] int hitsToKill = 3;
        [SerializeField, Min(0.05f)] float moveSpeed = 0.5f;
        [Tooltip("Walker: claw reach. Spitter: distance where it stops and shoots.")]
        [SerializeField, Min(0.1f)] float attackRange = 0.8f;
        [SerializeField, Min(0f)] float damage = 10f;
        [SerializeField, Min(0.1f)] float attackCooldown = 1.5f;
        [SerializeField, Min(0)] int scoreValue = 100;
        [SerializeField, Min(0.1f)] float projectileSpeed = 3f;

        public int HitsToKill => hitsToKill;
        public float MoveSpeed => moveSpeed;
        public float AttackRange => attackRange;
        public float Damage => damage;
        public float AttackCooldown => attackCooldown;
        public int ScoreValue => scoreValue;
        public float ProjectileSpeed => projectileSpeed;
    }

    [Serializable]
    public class WeaponStats
    {
        [SerializeField, Min(0.5f)] float boltSpeed = 8f;
        [SerializeField, Min(0.1f)] float boltLifetime = 2.5f;
        [SerializeField, Min(0)] int maxBounces = 3;
        [SerializeField, Min(0.005f)] float boltRadius = 0.03f;
        [SerializeField, Min(0.5f)] float shotsPerSecond = 5f;
        [SerializeField, Range(1f, 20f)] float spreadAngle = 8f;
        [SerializeField, Range(0f, 1f)] float heatPerShot = 0.12f;
        [SerializeField, Min(0f)] float coolPerSecond = 0.35f;
        [SerializeField, Min(0f)] float overheatLockTime = 1.5f;
        [SerializeField] float prismSplitAngle = 25f;
        [Tooltip("Score multiplier by bounce count (index = bounces).")]
        [SerializeField] float[] ricochetMultipliers = { 1f, 1.5f, 2f, 3f };

        public float BoltSpeed => boltSpeed;
        public float BoltLifetime => boltLifetime;
        public int MaxBounces => maxBounces;
        public float BoltRadius => boltRadius;
        public float FireInterval => 1f / shotsPerSecond;
        public float SpreadAngle => spreadAngle;
        public float HeatPerShot => heatPerShot;
        public float CoolPerSecond => coolPerSecond;
        public float OverheatLockTime => overheatLockTime;
        public float PrismSplitAngle => prismSplitAngle;

        public float MultiplierForBounces(int bounces)
        {
            if (ricochetMultipliers == null || ricochetMultipliers.Length == 0) return 1f;
            return ricochetMultipliers[Mathf.Clamp(bounces, 0, ricochetMultipliers.Length - 1)];
        }
    }

    [Serializable]
    public class CardSettings
    {
        [SerializeField, Min(1f)] float spawnInterval = 20f;
        [SerializeField, Min(0f)] float firstSpawnDelay = 10f;
        [SerializeField, Min(0.2f)] float spawnRadius = 1.5f;
        [SerializeField, Min(0.1f)] float pickupDistance = 0.5f;
        [SerializeField, Min(1f)] float cardLifetime = 18f;
        [SerializeField, Min(1)] int maxOnFloor = 2;
        [SerializeField, Min(1f)] float gadgetLifetime = 30f;
        [SerializeField, Min(1f)] float multiShotDuration = 12f;
        [SerializeField, Min(0.1f)] float freezeRadius = 2f;
        [SerializeField, Range(0f, 1f)] float freezeSpeedFactor = 0.3f;
        [SerializeField, Min(0.1f)] float freezeDuration = 4f;
        [SerializeField, Min(1f)] float medKitHeal = 30f;

        public float SpawnInterval => spawnInterval;
        public float FirstSpawnDelay => firstSpawnDelay;
        public float SpawnRadius => spawnRadius;
        public float PickupDistance => pickupDistance;
        public float CardLifetime => cardLifetime;
        public int MaxOnFloor => maxOnFloor;
        public float GadgetLifetime => gadgetLifetime;
        public float MultiShotDuration => multiShotDuration;
        public float FreezeRadius => freezeRadius;
        public float FreezeSpeedFactor => freezeSpeedFactor;
        public float FreezeDuration => freezeDuration;
        public float MedKitHeal => medKitHeal;
    }

    /// <summary>Every sound in the game. Gameplay asks for an id, never for a clip.</summary>
    public enum SoundId
    {
        PlayerShoot, PlayerDeath, EnemySpawn, SpitterShoot, WalkerAttackHit, // required
        LaserBounce, EnemyHit, EnemyDeath, PlayerHurt, CardPickup, MirrorPlace,
        Overheat, CountdownBeep, CountdownGo, RoundWin, UIClick, Music
    }

    [Serializable]
    public class SoundEntry
    {
        public SoundId id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.8f;
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        [Tooltip("3D sounds play from a pooled source at the event position.")]
        public bool spatial;
    }
}
