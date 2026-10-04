using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Ricochet
{
    public enum EnemyType { Walker, Spitter, Ghost }

    public class Enemy : MonoBehaviour, IPoolable, IDamageable
    {
        enum Phase { Rising, Active, Dying }

        [SerializeField] EnemyType type;
        [SerializeField] Transform model;
        [SerializeField] Collider hitCollider;
        [SerializeField] Transform mouth;
        [SerializeField] Transform mouthGlow;
        [SerializeField] Color deathColor = new Color(0.5f, 1f, 0.4f);
        [SerializeField, Min(0f)] float hover;

        const float RiseTime = 0.6f;
        const float DeathTime = 0.45f;
        const float Knockback = 0.08f;
        const float FlashTime = 0.12f;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color FlashColor = new Color(4f, 4f, 4f, 1f);
        static readonly Color BlockedColor = new Color(0.7f, 0.4f, 1f);

        EnemyAI ai;
        Action<Enemy> onReturn;
        Phase phase;
        float phaseTime, cooldown, slow = 1f, slowTimer, walkCycle, lunge, flashTimer, blockedTimer;
        Vector3 push, modelStart;
        Quaternion modelRotation;
        Renderer[] renderers;
        Color[][] baseColors;
        MaterialPropertyBlock block;
        Color tint = Color.white;

        public EnemyType Type => type;
        public EnemyStats Stats { get; private set; }
        public EnemyContext World { get; private set; }
        public int Health { get; private set; }
        public bool IsAlive => phase != Phase.Dying && Health > 0;
        public bool IsMoving { get; set; }
        public Transform Mouth => mouth;

        void Awake()
        {
            ai = EnemyAI.Create(type, this);
            if (model)
            {
                modelStart = model.localPosition;
                modelRotation = model.localRotation;
            }
            CacheColors();
        }

        public void SetReturn(Action<Enemy> callback) => onReturn = callback;

        public void Spawn(EnemyStats stats, EnemyContext world)
        {
            Stats = stats;
            World = world;
            Health = stats.Hits;
            cooldown = stats.Cooldown * 0.5f;
            var p = transform.position;
            p.y = world.Arena.Height;
            transform.position = p;
            LookAtPlayer(1000f);
        }

        public void OnSpawned()
        {
            phase = Phase.Rising;
            phaseTime = slowTimer = lunge = flashTimer = blockedTimer = 0f;
            slow = 1f;
            push = Vector3.zero;
            tint = Color.white;
            IsMoving = false;
            if (hitCollider) hitCollider.enabled = true;
            if (model)
            {
                model.localPosition = modelStart;
                model.localRotation = modelRotation;
                model.localScale = new Vector3(0.6f, 0.01f, 0.6f);
            }
            ai.Reset();
            ApplyColors(0f);
        }

        public void OnDespawned() => World = null;

        void Update()
        {
            if (World == null) return;
            float dt = Time.deltaTime;
            phaseTime += dt;
            blockedTimer -= dt;

            switch (phase)
            {
                case Phase.Rising:
                    float t = Mathf.Clamp01(phaseTime / RiseTime);
                    float e = 1f - (1f - t) * (1f - t);
                    if (model) model.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, e), e, Mathf.Lerp(0.6f, 1f, e));
                    LookAtPlayer(dt);
                    if (t >= 1f)
                    {
                        phase = Phase.Active;
                        phaseTime = 0f;
                    }
                    break;

                case Phase.Active:
                    if (slowTimer > 0f && (slowTimer -= dt) <= 0f)
                    {
                        slow = 1f;
                        tint = Color.white;
                        ApplyColors(0f);
                    }
                    cooldown -= dt * slow;
                    IsMoving = false;
                    ai.Tick(dt);
                    ApplyPush(dt);
                    Animate(dt);
                    break;

                case Phase.Dying:
                    float d = Mathf.Clamp01(phaseTime / DeathTime);
                    if (model) model.localScale = new Vector3(1f + d * 0.3f, 1f - d, 1f + d * 0.3f);
                    if (d >= 1f) onReturn?.Invoke(this);
                    break;
            }

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                ApplyColors(Mathf.Clamp01(flashTimer / FlashTime));
            }
        }

        public float DistanceToPlayer => World.Player.FlatDistance(transform.position);
        public bool ReadyToAttack => cooldown <= 0f;
        public float Charge => 1f - Mathf.Clamp01(cooldown / Stats.Cooldown);
        public float Damage => Stats.Damage * World.DamageMultiplier;
        public void ResetCooldown() => cooldown = Stats.Cooldown;
        public void Lunge() => lunge = 1f;

        public void SetMouthSize(float size)
        {
            if (mouthGlow) mouthGlow.localScale = Vector3.one * size;
        }

        public void MoveToPlayer(float dt, float stopDistance)
        {
            Vector3 pos = transform.position;
            Vector3 flat = World.Arena.ToFloor(World.Player.Position) - pos;
            flat.y = 0f;
            float dist = flat.magnitude;
            if (dist <= stopDistance) return;
            pos += flat / dist * Mathf.Min(Stats.Speed * slow * dt, dist - stopDistance);
            pos.y = World.Arena.Height;
            transform.position = pos;
            IsMoving = true;
        }

        public void LookAtPlayer(float dt)
        {
            Vector3 flat = World.Player.Position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            var look = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-8f * dt));
        }

        public void TakeDamage(Hit hit)
        {
            if (!IsAlive) return;

            if (!ai.CanBeHurtBy(hit))
            {
                Vfx.Sparks(hit.Point, -hit.Direction, BlockedColor, 12);
                if (blockedTimer <= 0f)
                {
                    blockedTimer = 1f;
                    GameEvents.Blocked(transform.position + Vector3.up * 1.2f);
                }
                return;
            }

            Health -= Mathf.Max(1, Mathf.RoundToInt(hit.Damage));
            flashTimer = FlashTime;
            Vector3 dir = hit.Direction;
            dir.y = 0f;
            push += dir.normalized * Knockback;
            AudioManager.Instance?.PlayAt(SoundId.EnemyHit, hit.Point);
            if (Health <= 0) Die(hit);
        }

        void Die(Hit hit)
        {
            phase = Phase.Dying;
            phaseTime = 0f;
            if (hitCollider) hitCollider.enabled = false;
            Vector3 centre = transform.position + Vector3.up * 0.9f;
            Vfx.Burst(centre, deathColor, 22, 1.1f);
            AudioManager.Instance?.PlayAt(SoundId.EnemyDeath, centre);
            GameEvents.Killed(new KillInfo(type, Stats.Score, hit.Multiplier, centre));
        }

        public void Slow(float factor, float duration)
        {
            if (!IsAlive) return;
            slow = Mathf.Clamp01(factor);
            slowTimer = duration;
            tint = new Color(0.5f, 0.8f, 1.6f);
            ApplyColors(0f);
        }

        void ApplyPush(float dt)
        {
            if (push.sqrMagnitude < 0.000001f) return;
            Vector3 step = push * (1f - Mathf.Exp(-15f * dt));
            transform.position += step;
            push -= step;
        }

        void Animate(float dt)
        {
            if (!model) return;
            if (IsMoving) walkCycle += dt * 9f * slow;
            float bob = IsMoving ? Mathf.Abs(Mathf.Sin(walkCycle)) * 0.04f : 0f;
            float sway = IsMoving ? Mathf.Sin(walkCycle) * 4f : 0f;
            if (hover > 0f) bob = hover + Mathf.Sin(Time.time * 2.5f) * 0.06f;
            lunge = Mathf.MoveTowards(lunge, 0f, dt * 3f);
            float l = Mathf.Sin(lunge * Mathf.PI);
            model.localPosition = modelStart + new Vector3(0f, bob, l * 0.15f);
            model.localRotation = modelRotation * Quaternion.Euler(l * 18f, 0f, sway);
        }

        void CacheColors()
        {
            block = new MaterialPropertyBlock();
            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length][];
            for (int r = 0; r < renderers.Length; r++)
            {
                var mats = renderers[r].sharedMaterials;
                baseColors[r] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                    baseColors[r][m] = mats[m] && mats[m].HasProperty(ColorId) ? mats[m].GetColor(ColorId) : Color.white;
            }
        }

        void ApplyColors(float flash)
        {
            for (int r = 0; r < renderers.Length; r++)
                for (int m = 0; m < baseColors[r].Length; m++)
                {
                    Color c = baseColors[r][m] * tint;
                    c.a = baseColors[r][m].a;
                    renderers[r].GetPropertyBlock(block, m);
                    block.SetColor(ColorId, Color.Lerp(c, FlashColor, flash));
                    renderers[r].SetPropertyBlock(block, m);
                }
        }
    }

    public abstract class EnemyAI
    {
        protected readonly Enemy Enemy;
        protected EnemyAI(Enemy enemy) => Enemy = enemy;

        public static EnemyAI Create(EnemyType type, Enemy enemy)
        {
            switch (type)
            {
                case EnemyType.Spitter: return new SpitterAI(enemy);
                case EnemyType.Ghost: return new GhostAI(enemy);
                default: return new WalkerAI(enemy);
            }
        }

        public virtual void Reset() { }
        public virtual bool CanBeHurtBy(Hit hit) => true;
        public abstract void Tick(float dt);
        protected abstract void Attack();
    }

    public class WalkerAI : EnemyAI
    {
        const float WindUp = 0.3f;
        float windUp = -1f;

        public WalkerAI(Enemy enemy) : base(enemy) { }

        public override void Reset() => windUp = -1f;

        public override void Tick(float dt)
        {
            Enemy.LookAtPlayer(dt);
            float distance = Enemy.DistanceToPlayer;

            if (windUp >= 0f)
            {
                windUp -= dt;
                if (windUp < 0f && distance <= Enemy.Stats.Range * 1.25f) Strike();
                return;
            }

            Enemy.MoveToPlayer(dt, Enemy.Stats.Range * 0.75f);
            if (distance <= Enemy.Stats.Range && Enemy.ReadyToAttack) Attack();
        }

        protected override void Attack()
        {
            Enemy.ResetCooldown();
            Enemy.Lunge();
            windUp = WindUp;
        }

        void Strike()
        {
            var player = Enemy.World.Player;
            player.TakeDamage(new Hit(Enemy.Damage, player.Target, player.Position - Enemy.transform.position));
            AudioManager.Instance?.Play(SoundId.WalkerAttackHit);
        }
    }

    public class GhostAI : WalkerAI
    {
        public GhostAI(Enemy enemy) : base(enemy) { }

        public override bool CanBeHurtBy(Hit hit) => hit.FromMirror;
    }

    public class SpitterAI : EnemyAI
    {
        public SpitterAI(Enemy enemy) : base(enemy) { }

        public override void Tick(float dt)
        {
            Enemy.LookAtPlayer(dt);
            if (Enemy.DistanceToPlayer > Enemy.Stats.Range)
            {
                Enemy.MoveToPlayer(dt, Enemy.Stats.Range * 0.95f);
                return;
            }

            float c = Enemy.Charge;
            Enemy.SetMouthSize(0.7f + c * c * 0.6f);
            if (Enemy.ReadyToAttack) Attack();
        }

        protected override void Attack()
        {
            Enemy.ResetCooldown();
            Enemy.Lunge();
            Vector3 from = Enemy.Mouth ? Enemy.Mouth.position : Enemy.transform.position + Vector3.up * 1.1f;
            Vector3 dir = (Enemy.World.Player.Target - from).normalized;
            Enemy.World.Factory.SpawnAcid(from, dir, Enemy.Stats.ShotSpeed, Enemy.Damage);
            AudioManager.Instance?.PlayAt(SoundId.SpitterShoot, from);
        }
    }

    public class EnemyContext
    {
        public Player Player;
        public Arena Arena;
        public EnemyFactory Factory;
        public float DamageMultiplier = 1f;
    }

    public class EnemyFactory
    {
        static readonly Color SpawnColor = new Color(0.5f, 1f, 0.3f);
        static readonly Color GhostSpawnColor = new Color(0.7f, 0.4f, 1f);

        readonly GameConfig config;
        readonly Dictionary<EnemyType, ObjectPool<Enemy>> pools = new Dictionary<EnemyType, ObjectPool<Enemy>>();
        readonly ObjectPool<Projectile> acid;
        readonly EnemyContext world = new EnemyContext();

        public EnemyFactory(GameConfig config, Transform poolRoot)
        {
            this.config = config;
            world.Factory = this;

            foreach (EnemyType t in Enum.GetValues(typeof(EnemyType)))
            {
                ObjectPool<Enemy> pool = null;
                int size = t == EnemyType.Ghost ? 6 : 12;
                pool = new ObjectPool<Enemy>(config.Prefab(t), size, poolRoot, e => e.SetReturn(x => pool.Return(x)));
                pools[t] = pool;
            }

            ObjectPool<Projectile> acidPool = null;
            acidPool = new ObjectPool<Projectile>(config.AcidPrefab, 20, poolRoot, g => g.SetReturn(x => acidPool.Return(x)));
            acid = acidPool;
        }

        public int Alive
        {
            get
            {
                int n = 0;
                foreach (var pool in pools.Values)
                    foreach (var e in pool.Active)
                        if (e.IsAlive) n++;
                return n;
            }
        }

        public void Setup(Player player, Arena arena, float damageMultiplier)
        {
            world.Player = player;
            world.Arena = arena;
            world.DamageMultiplier = damageMultiplier;
        }

        public Enemy Create(EnemyType type, Vector3 position)
        {
            if (world.Arena == null || world.Player == null) return null;
            var enemy = pools[type].Get(position, Quaternion.identity);
            enemy.Spawn(config.Stats(type), world);
            Vfx.Ring(enemy.transform.position, 0.35f, type == EnemyType.Ghost ? GhostSpawnColor : SpawnColor);
            AudioManager.Instance?.PlayAt(SoundId.EnemySpawn, enemy.transform.position);
            return enemy;
        }

        public void SpawnAcid(Vector3 position, Vector3 dir, float speed, float damage)
        {
            acid.Get(position, Quaternion.LookRotation(dir)).FireAcid(dir, speed, damage);
        }

        public void ForEach(Action<Enemy> action)
        {
            foreach (var pool in pools.Values)
                for (int i = pool.Active.Count - 1; i >= 0; i--)
                    if (pool.Active[i].IsAlive) action(pool.Active[i]);
        }

        public void ReturnAll()
        {
            foreach (var pool in pools.Values) pool.ReturnAll();
            acid.ReturnAll();
        }
    }

    public class EnemySpawner
    {
        const float MinDistance = 2.5f, MaxDistance = 4f, FrontAngle = 75f, FrontChance = 0.7f, FirstDelay = 1f;
        const int Tries = 16;

        readonly EnemyFactory factory;
        readonly Func<bool> hasMirrors;
        DifficultySettings difficulty;
        Func<float> progress;
        Player player;
        Arena arena;
        bool running;
        float timer;

        public EnemySpawner(EnemyFactory factory, Func<bool> hasMirrors)
        {
            this.factory = factory;
            this.hasMirrors = hasMirrors;
        }

        public void Setup(DifficultySettings settings, Func<float> roundProgress, Player target, Arena area)
        {
            difficulty = settings;
            progress = roundProgress;
            player = target;
            arena = area;
            timer = FirstDelay;
        }

        public void Enable(bool on) => running = on && difficulty != null && arena != null;

        public void Tick(float dt)
        {
            if (!running || (timer -= dt) > 0f) return;
            float p = progress();
            timer = difficulty.SpawnDelay(p);
            if (factory.Alive >= difficulty.MaxEnemies) return;
            factory.Create(PickType(p), FindSpot());
        }

        EnemyType PickType(float p)
        {
            if (hasMirrors() && Random.value < difficulty.GhostChance) return EnemyType.Ghost;
            return Random.value < difficulty.SpitterChance(p) ? EnemyType.Spitter : EnemyType.Walker;
        }

        Vector3 FindSpot()
        {
            Vector3 feet = arena.ToFloor(player.Position);
            Vector3 fwd = player.Forward;
            float facing = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;

            for (int i = 0; i < Tries; i++)
            {
                float angle = Random.value < FrontChance ? facing + Random.Range(-FrontAngle, FrontAngle) : Random.Range(0f, 360f);
                Vector3 spot = feet + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(MinDistance, MaxDistance);
                if (arena.IsOnFloor(spot)) return spot;
            }

            Vector3 best = feet + Vector3.forward * MinDistance;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Tries; i++)
            {
                float angle = facing + Random.Range(-FrontAngle, FrontAngle);
                Vector3 spot = feet + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(MinDistance, MaxDistance);
                if (arena.InRange(spot)) return arena.ToFloor(spot);
                float d = Vector3.Distance(spot, arena.Center);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = spot;
                }
            }
            return arena.ToFloor(best);
        }
    }
}
