using System;
using System.Collections.Generic;
using Ricochet.AR;
using Ricochet.Audio;
using Ricochet.Core;
using Ricochet.Player;
using Ricochet.Pooling;
using UnityEngine;

namespace Ricochet.Enemies
{
    /// <summary>
    /// Factory pattern: callers say "give me a Spitter here" and don't know which prefab or pool is used.
    /// Behind the scenes every enemy and every acid glob comes from a pre-warmed ObjectPool,
    /// so wiping the board at game end = returning everything to its pool.
    /// </summary>
    public class EnemyFactory : MonoBehaviour
    {
        [SerializeField] WalkerEnemy walkerPrefab;
        [SerializeField] SpitterEnemy spitterPrefab;
        [SerializeField] AcidGlob acidPrefab;
        [SerializeField] Transform poolRoot;
        [SerializeField, Min(1)] int enemiesPerType = 12;
        [SerializeField, Min(1)] int acidPoolSize = 20;
        [SerializeField] Color spawnRingColor = new Color(0.5f, 1f, 0.3f);

        readonly Dictionary<EnemyType, ObjectPool<Enemy>> _pools = new Dictionary<EnemyType, ObjectPool<Enemy>>();
        ObjectPool<AcidGlob> _acid;
        readonly EnemyContext _context = new EnemyContext();

        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (var pool in _pools.Values)
                    foreach (var e in pool.Active)
                        if (e.IsAlive) n++;
                return n;
            }
        }

        void Awake()
        {
            _pools[EnemyType.Walker] = CreateEnemyPool(walkerPrefab);
            _pools[EnemyType.Spitter] = CreateEnemyPool(spitterPrefab);

            ObjectPool<AcidGlob> acid = null;
            acid = new ObjectPool<AcidGlob>(acidPrefab, acidPoolSize, poolRoot, g => g.BindRelease(x => acid.Release(x)));
            _acid = acid;
            _context.Factory = this;
        }

        ObjectPool<Enemy> CreateEnemyPool(Enemy prefab)
        {
            ObjectPool<Enemy> pool = null;
            // The lambda runs later (on release), by which time 'pool' is assigned.
            pool = new ObjectPool<Enemy>(prefab, enemiesPerType, poolRoot, e => e.BindRelease(x => pool.Release(x)));
            return pool;
        }

        /// <summary>Sets what newly created enemies need to know for this round.</summary>
        public void Configure(PlayerController player, ArenaContext arena, float damageMultiplier)
        {
            _context.Player = player;
            _context.PlayerTarget = player ? player.GetComponent<IDamageable>() : null;
            _context.Arena = arena;
            _context.DamageMultiplier = damageMultiplier;
        }

        /// <summary>The factory method.</summary>
        public Enemy Create(EnemyType type, Vector3 position)
        {
            if (_context.Arena == null || _context.Player == null) return null;

            var enemy = _pools[type].Get(position, Quaternion.identity);
            enemy.Activate(_context);

            VfxManager.Instance?.Ring(enemy.transform.position, 0.35f, spawnRingColor);
            AudioManager.Instance?.PlayAt(SoundId.EnemySpawn, enemy.transform.position);
            GameEvents.RaiseEnemySpawned(enemy.transform.position);
            return enemy;
        }

        public AcidGlob SpawnAcid(Vector3 position, Vector3 direction, float speed, float damage)
        {
            var glob = _acid.Get(position, Quaternion.LookRotation(direction));
            glob.Launch(direction, speed, damage);
            return glob;
        }

        public void ForEachAlive(Action<Enemy> action)
        {
            foreach (var pool in _pools.Values)
                for (int i = pool.Active.Count - 1; i >= 0; i--)
                    if (pool.Active[i].IsAlive) action(pool.Active[i]);
        }

        /// <summary>Wipes every enemy and acid glob (end of round).</summary>
        public void ReleaseAll()
        {
            foreach (var pool in _pools.Values) pool.ReleaseAll();
            _acid.ReleaseAll();
        }
    }
}
