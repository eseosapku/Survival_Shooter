using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Ricochet
{
    public enum EnemyType { Walker, Spitter }

    /// <summary>
    /// The enemy component (on P_Walker and P_Spitter). It does everything all enemies share:
    /// health, rising out of the floor, walking on the floor toward the player, facing the player,
    /// hit flash + knockback, slow effect, death, returning to the pool.
    /// WHAT the enemy does each frame and HOW it attacks is delegated to an EnemyAI subclass
    /// (WalkerAI = melee, SpitterAI = ranged): inheritance + polymorphism.
    /// Visuals live under the child "Model", so a real 3D model can be swapped in without touching code.
    /// </summary>
    public class Enemy : MonoBehaviour, IPoolable, IDamageable
    {
        enum LifeState { Rising, Active, Dying }

        [SerializeField] EnemyType type;
        [SerializeField] Transform model;
        [SerializeField] Collider hitCollider;
        [Tooltip("Spitter only: where acid comes from, and the glowing mouth that swells before a shot.")]
        [SerializeField] Transform mouth;
        [SerializeField] Transform mouthGlow;
        [SerializeField] Color deathColor = new Color(0.5f, 1f, 0.4f);

        const float RiseDuration = 0.6f;
        const float DeathDuration = 0.45f;
        const float KnockbackDistance = 0.08f;
        const float FlashDuration = 0.12f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color FlashColor = new Color(4f, 4f, 4f, 1f);

        EnemyAI _ai;
        Action<Enemy> _release;
        LifeState _state;
        float _stateTimer, _cooldown, _slowFactor = 1f, _slowTimer, _walkPhase, _lunge, _flashTimer;
        Vector3 _knockback, _modelRestPos;
        Quaternion _modelRestRot;
        Renderer[] _renderers;
        Color[][] _baseColors;
        MaterialPropertyBlock _block;
        Color _tint = Color.white;

        public EnemyType Type => type;
        public EnemyStats Stats { get; private set; }
        public EnemyContext Ctx { get; private set; }
        public int Health { get; private set; }
        public bool IsAlive => _state != LifeState.Dying && Health > 0;
        public bool IsMoving { get; set; }
        public Transform Mouth => mouth;

        void Awake()
        {
            _ai = EnemyAI.Create(type, this);
            if (model)
            {
                _modelRestPos = model.localPosition;
                _modelRestRot = model.localRotation;
            }
            CacheRenderers();
        }

        public void BindRelease(Action<Enemy> release) => _release = release;

        /// <summary>Called by the factory right after taking the enemy from the pool.</summary>
        public void Activate(EnemyStats stats, EnemyContext context)
        {
            Stats = stats;
            Ctx = context;
            Health = stats.HitsToKill;
            _cooldown = stats.AttackCooldown * 0.5f;
            var p = transform.position;
            p.y = context.Arena.FloorHeight;
            transform.position = p;
            FacePlayer(1000f);
        }

        // ---------- IPoolable: full reset on reuse ----------

        public void OnSpawned()
        {
            _state = LifeState.Rising;
            _stateTimer = _slowTimer = _lunge = _flashTimer = 0f;
            _slowFactor = 1f;
            _knockback = Vector3.zero;
            _tint = Color.white;
            IsMoving = false;
            if (hitCollider) hitCollider.enabled = true;
            if (model)
            {
                model.localPosition = _modelRestPos;
                model.localRotation = _modelRestRot;
                model.localScale = new Vector3(0.6f, 0.01f, 0.6f);
            }
            _ai.Reset();
            ApplyColors(0f);
        }

        public void OnDespawned() => Ctx = null;

        // ---------- Loop ----------

        void Update()
        {
            if (Ctx == null) return;
            float dt = Time.deltaTime;
            _stateTimer += dt;

            switch (_state)
            {
                case LifeState.Rising:
                    // Grows up out of the real floor (pivot at the feet, so nothing renders below the floor).
                    float t = Mathf.Clamp01(_stateTimer / RiseDuration);
                    float e = 1f - (1f - t) * (1f - t);
                    if (model) model.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, e), e, Mathf.Lerp(0.6f, 1f, e));
                    FacePlayer(dt);
                    if (t >= 1f) { _state = LifeState.Active; _stateTimer = 0f; }
                    break;

                case LifeState.Active:
                    if (_slowTimer > 0f && (_slowTimer -= dt) <= 0f) { _slowFactor = 1f; _tint = Color.white; ApplyColors(0f); }
                    _cooldown -= dt * _slowFactor;
                    IsMoving = false;
                    _ai.Tick(dt); // polymorphic: Walker or Spitter behaviour
                    ApplyKnockback(dt);
                    AnimateModel(dt);
                    break;

                case LifeState.Dying:
                    float d = Mathf.Clamp01(_stateTimer / DeathDuration);
                    if (model) model.localScale = new Vector3(1f + d * 0.3f, 1f - d, 1f + d * 0.3f);
                    if (d >= 1f) _release?.Invoke(this);
                    break;
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                ApplyColors(Mathf.Clamp01(_flashTimer / FlashDuration));
            }
        }

        // ---------- Helpers used by EnemyAI subclasses ----------

        public float DistanceToPlayer => Ctx.Player.HorizontalDistanceTo(transform.position);
        public bool CooldownReady => _cooldown <= 0f;
        public float AttackCharge01 => 1f - Mathf.Clamp01(_cooldown / Stats.AttackCooldown);
        public float ScaledDamage => Stats.Damage * Ctx.DamageMultiplier;
        public void ResetCooldown() => _cooldown = Stats.AttackCooldown;
        public void PlayAttackAnimation() => _lunge = 1f;
        public void SetMouthScale(float s) { if (mouthGlow) mouthGlow.localScale = Vector3.one * s; }

        /// <summary>Walks along the floor toward the player's floor position, stopping at stopDistance.</summary>
        public void MoveTowardPlayer(float dt, float stopDistance)
        {
            Vector3 pos = transform.position;
            Vector3 flat = Ctx.Arena.ProjectToFloor(Ctx.Player.Position) - pos;
            flat.y = 0f;
            float dist = flat.magnitude;
            if (dist <= stopDistance) return;
            pos += flat / dist * Mathf.Min(Stats.MoveSpeed * _slowFactor * dt, dist - stopDistance);
            pos.y = Ctx.Arena.FloorHeight;
            transform.position = pos;
            IsMoving = true;
        }

        public void FacePlayer(float dt)
        {
            Vector3 flat = Ctx.Player.Position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            var look = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-8f * dt));
        }

        // ---------- Damage ----------

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;
            Health -= Mathf.Max(1, Mathf.RoundToInt(info.Amount));
            _flashTimer = FlashDuration;
            Vector3 push = info.Direction;
            push.y = 0f;
            _knockback += push.normalized * KnockbackDistance;
            AudioManager.Instance?.PlayAt(SoundId.EnemyHit, info.Point);
            if (Health <= 0) Die(info);
        }

        void Die(DamageInfo killingBlow)
        {
            _state = LifeState.Dying;
            _stateTimer = 0f;
            if (hitCollider) hitCollider.enabled = false;
            Vector3 centre = transform.position + Vector3.up * 0.9f;
            Vfx.Burst(centre, deathColor, 22, 1.1f);
            AudioManager.Instance?.PlayAt(SoundId.EnemyDeath, centre);
            GameEvents.RaiseEnemyKilled(new EnemyKilledArgs(type, Stats.ScoreValue, killingBlow.ScoreMultiplier, centre));
        }

        /// <summary>Freeze Pulse card: slows movement and attacks, tints the enemy blue.</summary>
        public void ApplySlow(float speedFactor, float duration)
        {
            if (!IsAlive) return;
            _slowFactor = Mathf.Clamp01(speedFactor);
            _slowTimer = duration;
            _tint = new Color(0.5f, 0.8f, 1.6f);
            ApplyColors(0f);
        }

        void ApplyKnockback(float dt)
        {
            if (_knockback.sqrMagnitude < 0.000001f) return;
            Vector3 step = _knockback * (1f - Mathf.Exp(-15f * dt));
            transform.position += step;
            _knockback -= step;
        }

        /// <summary>Procedural walk bob and attack lunge for the placeholder models.</summary>
        void AnimateModel(float dt)
        {
            if (!model) return;
            if (IsMoving) _walkPhase += dt * 9f * _slowFactor;
            float bob = IsMoving ? Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.04f : 0f;
            float sway = IsMoving ? Mathf.Sin(_walkPhase) * 4f : 0f;
            _lunge = Mathf.MoveTowards(_lunge, 0f, dt * 3f);
            float l = Mathf.Sin(_lunge * Mathf.PI);
            model.localPosition = _modelRestPos + new Vector3(0f, bob, l * 0.15f);
            model.localRotation = _modelRestRot * Quaternion.Euler(l * 18f, 0f, sway);
        }

        // ---------- Hit flash (MaterialPropertyBlock: no material copies, works with real models too) ----------

        void CacheRenderers()
        {
            _block = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColors = new Color[_renderers.Length][];
            for (int r = 0; r < _renderers.Length; r++)
            {
                var mats = _renderers[r].sharedMaterials;
                _baseColors[r] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                    _baseColors[r][m] = mats[m] && mats[m].HasProperty(BaseColorId) ? mats[m].GetColor(BaseColorId) : Color.white;
            }
        }

        void ApplyColors(float flash01)
        {
            for (int r = 0; r < _renderers.Length; r++)
                for (int m = 0; m < _baseColors[r].Length; m++)
                {
                    Color c = _baseColors[r][m] * _tint;
                    c.a = _baseColors[r][m].a;
                    _renderers[r].GetPropertyBlock(_block, m);
                    _block.SetColor(BaseColorId, Color.Lerp(c, FlashColor, flash01));
                    _renderers[r].SetPropertyBlock(_block, m);
                }
        }
    }

    // =========================================================================
    // ENEMY BEHAVIOUR: abstract base + one subclass per enemy type (polymorphism)
    // =========================================================================

    /// <summary>Abstract behaviour every enemy type must provide: what to do each frame, and how to attack.</summary>
    public abstract class EnemyAI
    {
        protected readonly Enemy Enemy;
        protected EnemyAI(Enemy enemy) => Enemy = enemy;

        /// <summary>Simple factory method: picks the behaviour class for an enemy type.</summary>
        public static EnemyAI Create(EnemyType type, Enemy enemy) =>
            type == EnemyType.Walker ? new WalkerAI(enemy) : (EnemyAI)new SpitterAI(enemy);

        public virtual void Reset() { }
        public abstract void Tick(float dt);
        protected abstract void Attack();
    }

    /// <summary>Melee: walks straight at the player; in claw range (0.8 m) it winds up and swipes.</summary>
    public class WalkerAI : EnemyAI
    {
        const float WindUp = 0.3f;
        float _windUp = -1f;

        public WalkerAI(Enemy enemy) : base(enemy) { }

        public override void Reset() => _windUp = -1f;

        public override void Tick(float dt)
        {
            Enemy.FacePlayer(dt);
            float distance = Enemy.DistanceToPlayer;

            if (_windUp >= 0f)
            {
                _windUp -= dt;
                // Damage only lands if the player is still close when the claw connects.
                if (_windUp < 0f && distance <= Enemy.Stats.AttackRange * 1.25f) LandHit();
                return;
            }

            Enemy.MoveTowardPlayer(dt, Enemy.Stats.AttackRange * 0.75f);
            if (distance <= Enemy.Stats.AttackRange && Enemy.CooldownReady) Attack();
        }

        protected override void Attack()
        {
            Enemy.ResetCooldown();
            Enemy.PlayAttackAnimation();
            _windUp = WindUp;
        }

        void LandHit()
        {
            var player = Enemy.Ctx.Player;
            player.TakeDamage(new DamageInfo(Enemy.ScaledDamage, player.TargetPoint, player.Position - Enemy.transform.position));
            AudioManager.Instance?.Play(SoundId.WalkerAttackHit); // required: melee damaging the player
        }
    }

    /// <summary>Ranged: walks until 3 m from the player, then stops and spits pooled acid globs at the player's head.</summary>
    public class SpitterAI : EnemyAI
    {
        public SpitterAI(Enemy enemy) : base(enemy) { }

        public override void Tick(float dt)
        {
            Enemy.FacePlayer(dt);
            if (Enemy.DistanceToPlayer > Enemy.Stats.AttackRange)
            {
                Enemy.MoveTowardPlayer(dt, Enemy.Stats.AttackRange * 0.95f);
                return;
            }

            // Mouth swells as the next shot gets close: a readable "about to fire" warning.
            float c = Enemy.AttackCharge01;
            Enemy.SetMouthScale(0.7f + c * c * 0.6f);
            if (Enemy.CooldownReady) Attack();
        }

        protected override void Attack()
        {
            Enemy.ResetCooldown();
            Enemy.PlayAttackAnimation();
            Vector3 origin = Enemy.Mouth ? Enemy.Mouth.position : Enemy.transform.position + Vector3.up * 1.1f;
            Vector3 dir = (Enemy.Ctx.Player.TargetPoint - origin).normalized;
            Enemy.Ctx.Factory.SpawnAcid(origin, dir, Enemy.Stats.ProjectileSpeed, Enemy.ScaledDamage);
            AudioManager.Instance?.PlayAt(SoundId.SpitterShoot, origin);
        }
    }

    /// <summary>What an enemy needs to know about the world, shared by all enemies of the round.</summary>
    public class EnemyContext
    {
        public Player Player;
        public Arena Arena;
        public EnemyFactory Factory;
        public float DamageMultiplier = 1f;
    }

    // =========================================================================
    // FACTORY + SPAWNER
    // =========================================================================

    /// <summary>
    /// Factory pattern: callers say "give me a Spitter here" and never see prefabs or pools.
    /// Every enemy and acid glob comes from a pre-warmed ObjectPool, so wiping the board = returning to pools.
    /// </summary>
    public class EnemyFactory
    {
        const int EnemiesPerType = 12;
        const int AcidPoolSize = 20;
        static readonly Color SpawnRingColor = new Color(0.5f, 1f, 0.3f);

        readonly GameConfig _config;
        readonly Dictionary<EnemyType, ObjectPool<Enemy>> _pools = new Dictionary<EnemyType, ObjectPool<Enemy>>();
        readonly ObjectPool<Projectile> _acid;
        readonly EnemyContext _context = new EnemyContext();

        public EnemyFactory(GameConfig config, Transform poolRoot)
        {
            _config = config;
            _context.Factory = this;
            foreach (EnemyType t in Enum.GetValues(typeof(EnemyType)))
            {
                ObjectPool<Enemy> pool = null;
                pool = new ObjectPool<Enemy>(config.PrefabFor(t), EnemiesPerType, poolRoot, e => e.BindRelease(x => pool.Release(x)));
                _pools[t] = pool;
            }
            ObjectPool<Projectile> acid = null;
            acid = new ObjectPool<Projectile>(config.AcidPrefab, AcidPoolSize, poolRoot, g => g.BindRelease(x => acid.Release(x)));
            _acid = acid;
        }

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

        public void Configure(Player player, Arena arena, float damageMultiplier)
        {
            _context.Player = player;
            _context.Arena = arena;
            _context.DamageMultiplier = damageMultiplier;
        }

        /// <summary>The factory method.</summary>
        public Enemy Create(EnemyType type, Vector3 position)
        {
            if (_context.Arena == null || _context.Player == null) return null;
            var enemy = _pools[type].Get(position, Quaternion.identity);
            enemy.Activate(_config.StatsFor(type), _context);
            Vfx.Ring(enemy.transform.position, 0.35f, SpawnRingColor);
            AudioManager.Instance?.PlayAt(SoundId.EnemySpawn, enemy.transform.position);
            return enemy;
        }

        public void SpawnAcid(Vector3 position, Vector3 direction, float speed, float damage)
        {
            _acid.Get(position, Quaternion.LookRotation(direction)).LaunchAcid(direction, speed, damage);
        }

        public void ForEachAlive(Action<Enemy> action)
        {
            foreach (var pool in _pools.Values)
                for (int i = pool.Active.Count - 1; i >= 0; i--)
                    if (pool.Active[i].IsAlive) action(pool.Active[i]);
        }

        public void ReleaseAll()
        {
            foreach (var pool in _pools.Values) pool.ReleaseAll();
            _acid.ReleaseAll();
        }
    }

    /// <summary>
    /// Decides WHEN and WHERE enemies appear (the factory decides HOW). Over the round the spawn interval
    /// shrinks and the Spitter chance grows. Spawn points are 2.5-4 m from the player, on the placed floor
    /// (fallback: within a radius of the beacon), mostly in front of the player so they see them rise.
    /// </summary>
    public class EnemySpawner
    {
        const float MinDistance = 2.5f, MaxDistance = 4f, FrontHalfAngle = 75f, FrontChance = 0.7f, FirstDelay = 1f;
        const int Attempts = 16;

        readonly EnemyFactory _factory;
        DifficultySettings _difficulty;
        Func<float> _progress;
        Player _player;
        Arena _arena;
        bool _running;
        float _timer;

        public EnemySpawner(EnemyFactory factory) => _factory = factory;

        public void Configure(DifficultySettings difficulty, Func<float> progress01, Player player, Arena arena)
        {
            _difficulty = difficulty;
            _progress = progress01;
            _player = player;
            _arena = arena;
            _timer = FirstDelay;
        }

        public void SetRunning(bool running) => _running = running && _difficulty != null && _arena != null;

        public void Tick(float dt)
        {
            if (!_running || (_timer -= dt) > 0f) return;
            float p = _progress();
            _timer = _difficulty.SpawnIntervalAt(p);
            if (_factory.AliveCount >= _difficulty.MaxEnemiesAlive) return;
            var type = Random.value < _difficulty.SpitterChanceAt(p) ? EnemyType.Spitter : EnemyType.Walker;
            _factory.Create(type, FindSpawnPoint());
        }

        Vector3 FindSpawnPoint()
        {
            Vector3 playerFloor = _arena.ProjectToFloor(_player.Position);
            Vector3 fwd = _player.Forward;
            float facing = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;

            for (int i = 0; i < Attempts; i++)
            {
                float angle = Random.value < FrontChance ? facing + Random.Range(-FrontHalfAngle, FrontHalfAngle) : Random.Range(0f, 360f);
                Vector3 c = playerFloor + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(MinDistance, MaxDistance);
                if (_arena.IsOnFloor(c)) return c;
            }

            // Fallback: within the beacon radius but still 2.5-4 m away, so nothing spawns in the player's face.
            Vector3 best = playerFloor + Vector3.forward * MinDistance;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Attempts; i++)
            {
                float angle = facing + Random.Range(-FrontHalfAngle, FrontHalfAngle);
                Vector3 c = playerFloor + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(MinDistance, MaxDistance);
                if (_arena.IsWithinFallback(c)) return _arena.ProjectToFloor(c);
                float d = Vector3.Distance(c, _arena.Center);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return _arena.ProjectToFloor(best);
        }
    }
}
