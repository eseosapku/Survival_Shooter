using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ricochet
{
    [CreateAssetMenu(menuName = "Ricochet/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Difficulties")]
        [SerializeField] DifficultySettings[] difficulties = new DifficultySettings[3];

        [Header("Enemies")]
        [SerializeField] EnemyStats walker = new EnemyStats();
        [SerializeField] EnemyStats spitter = new EnemyStats();
        [SerializeField] EnemyStats ghost = new EnemyStats();
        [SerializeField] Enemy walkerPrefab;
        [SerializeField] Enemy spitterPrefab;
        [SerializeField] Enemy ghostPrefab;
        [SerializeField] Projectile acidPrefab;

        [Header("Weapon")]
        [SerializeField] WeaponStats weapon = new WeaponStats();
        [SerializeField] Projectile laserBoltPrefab;

        [Header("Cards and mirrors")]
        [SerializeField] CardSettings cards = new CardSettings();
        [SerializeField] Transform[] cardPrefabs = new Transform[5];
        [SerializeField] Transform mirrorPrefab;
        [SerializeField] Transform prismPrefab;

        [Header("Audio")]
        [SerializeField] List<SoundEntry> sounds = new List<SoundEntry>();

        public int DifficultyCount => difficulties.Length;
        public DifficultySettings Difficulty(int index) => difficulties[Mathf.Clamp(index, 0, difficulties.Length - 1)];

        public EnemyStats Stats(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Spitter: return spitter;
                case EnemyType.Ghost: return ghost;
                default: return walker;
            }
        }

        public Enemy Prefab(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Spitter: return spitterPrefab;
                case EnemyType.Ghost: return ghostPrefab;
                default: return walkerPrefab;
            }
        }

        public Projectile AcidPrefab => acidPrefab;
        public WeaponStats Weapon => weapon;
        public Projectile LaserPrefab => laserBoltPrefab;
        public CardSettings Cards => cards;
        public Transform[] CardPrefabs => cardPrefabs;
        public Transform MirrorPrefab => mirrorPrefab;
        public Transform PrismPrefab => prismPrefab;
        public IReadOnlyList<SoundEntry> Sounds => sounds;
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
        [SerializeField, Range(0f, 1f)] float ghostChance = 0.15f;
        [SerializeField, Min(0.1f)] float enemyDamageMultiplier = 1f;
        [SerializeField, Min(1f)] float playerMaxHealth = 100f;

        public string Name => displayName;
        public float RoundLength => roundLength;
        public int MaxEnemies => maxEnemiesAlive;
        public float GhostChance => ghostChance;
        public float DamageMultiplier => enemyDamageMultiplier;
        public float PlayerHealth => playerMaxHealth;

        public float SpawnDelay(float progress) => Mathf.Lerp(startSpawnInterval, minSpawnInterval, progress);
        public float SpitterChance(float progress) => Mathf.Lerp(spitterChanceStart, spitterChanceEnd, progress);
    }

    [Serializable]
    public class EnemyStats
    {
        [SerializeField, Min(1)] int hitsToKill = 3;
        [SerializeField, Min(0.05f)] float moveSpeed = 0.5f;
        [SerializeField, Min(0.1f)] float attackRange = 0.8f;
        [SerializeField, Min(0f)] float damage = 10f;
        [SerializeField, Min(0.1f)] float attackCooldown = 1.5f;
        [SerializeField, Min(0)] int scoreValue = 100;
        [SerializeField, Min(0.1f)] float projectileSpeed = 3f;

        public int Hits => hitsToKill;
        public float Speed => moveSpeed;
        public float Range => attackRange;
        public float Damage => damage;
        public float Cooldown => attackCooldown;
        public int Score => scoreValue;
        public float ShotSpeed => projectileSpeed;
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
        [SerializeField] float[] ricochetMultipliers = { 1f, 1.5f, 2f, 3f };

        public float Speed => boltSpeed;
        public float Lifetime => boltLifetime;
        public int MaxBounces => maxBounces;
        public float Radius => boltRadius;
        public float FireDelay => 1f / shotsPerSecond;
        public float Spread => spreadAngle;
        public float HeatPerShot => heatPerShot;
        public float CoolRate => coolPerSecond;
        public float LockTime => overheatLockTime;
        public float SplitAngle => prismSplitAngle;

        public float Multiplier(int bounces)
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
        [SerializeField, Min(0)] int setupMirrors = 3;
        [SerializeField, Min(1f)] float multiShotDuration = 12f;
        [SerializeField, Min(0.1f)] float freezeRadius = 2f;
        [SerializeField, Range(0f, 1f)] float freezeSpeedFactor = 0.3f;
        [SerializeField, Min(0.1f)] float freezeDuration = 4f;
        [SerializeField, Min(1f)] float medKitHeal = 30f;

        public float SpawnInterval => spawnInterval;
        public float FirstDelay => firstSpawnDelay;
        public float SpawnRadius => spawnRadius;
        public float PickupDistance => pickupDistance;
        public float CardLifetime => cardLifetime;
        public int MaxOnFloor => maxOnFloor;
        public float GadgetLifetime => gadgetLifetime;
        public int SetupMirrors => setupMirrors;
        public float MultiShotTime => multiShotDuration;
        public float FreezeRadius => freezeRadius;
        public float FreezeSpeed => freezeSpeedFactor;
        public float FreezeTime => freezeDuration;
        public float Heal => medKitHeal;
    }

    public enum SoundId
    {
        PlayerShoot, PlayerDeath, EnemySpawn, SpitterShoot, WalkerAttackHit,
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
        public bool spatial;
    }
}
