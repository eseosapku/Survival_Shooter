using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ricochet
{
    /// <summary>
    /// The AR camera IS the player. This component sits on the camera and handles:
    /// - HEALTH (IDamageable): enemies and acid hurt it through the same interface as enemies use.
    /// - DAMAGE FEEDBACK: gun-viewmodel shake (never the AR camera), vibration, hurt/death sounds.
    /// - LASER BLASTER: hold-to-fire pooled bolts from the muzzle toward the crosshair, heat and spread tiers.
    /// The hurtbox is a child sphere (r = 0.25 m, layer PlayerHurtbox) with a kinematic Rigidbody.
    /// </summary>
    public class Player : MonoBehaviour, IDamageable
    {
        [Header("References")]
        [SerializeField] GameConfig config;
        [SerializeField] Camera playerCamera;
        [SerializeField] SphereCollider hurtbox;
        [SerializeField] Transform viewmodel;
        [SerializeField] Transform gunModel;
        [SerializeField] Transform muzzle;
        [SerializeField] GameObject muzzleFlash;
        [SerializeField] Transform boltPoolRoot;

        [Header("Weapon")]
        [SerializeField, Min(1)] int boltPoolSize = 40;
        [SerializeField] LayerMask aimMask;
        [SerializeField, Min(1f)] float maxAimDistance = 15f;

        [Header("Feedback")]
        [SerializeField, Min(0f)] float shakeStrength = 0.02f;
        [SerializeField, Min(0.01f)] float shakeDuration = 0.25f;

        ObjectPool<Projectile> _bolts;
        readonly List<Vector3> _directions = new List<Vector3>(4);
        WeaponStats _weapon;
        bool _invulnerable = true;
        bool _armed;
        bool _triggerHeld;
        float _cooldown;
        int _bonusTiers;
        float _bonusTimer;
        float _flashTimer;
        float _recoil;
        float _shakeTimer;
        Vector3 _gunRest;
        Vector3 _viewmodelRest;

        // ---------- Public state ----------

        public Camera Camera => playerCamera;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        /// <summary>The point enemies aim at (centre of the hurtbox).</summary>
        public Vector3 TargetPoint => hurtbox ? hurtbox.transform.TransformPoint(hurtbox.center) : transform.position;

        public float MaxHealth { get; private set; } = 100f;
        public float Health { get; private set; } = 100f;
        public float Health01 => MaxHealth > 0f ? Health / MaxHealth : 0f;
        public bool IsAlive => Health > 0f;

        public HeatSystem Heat { get; private set; }
        public int SpreadTier => Mathf.Clamp(1 + _bonusTiers, 1, 4);
        public float SpreadBonusRemaining => _bonusTimer;

        // ---------- Events (Observer) ----------

        public event Action<float, float> HealthChanged; // (current, max)
        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public event Action<int> SpreadTierChanged;

        void Awake()
        {
            _weapon = config.Weapon;
            Heat = new HeatSystem(_weapon.CoolPerSecond, _weapon.OverheatLockTime);
            Heat.OverheatChanged += hot => { if (hot) AudioManager.Instance?.Play(SoundId.Overheat); };

            // Pre-warm: every bolt that will ever be used is created now, before play starts.
            ObjectPool<Projectile> pool = null;
            pool = new ObjectPool<Projectile>(config.LaserBoltPrefab, boltPoolSize, boltPoolRoot, b => b.BindRelease(x => pool.Release(x)));
            _bolts = pool;

            if (gunModel) _gunRest = gunModel.localPosition;
            if (viewmodel) _viewmodelRest = viewmodel.localPosition;
            if (muzzleFlash) muzzleFlash.SetActive(false);
        }

        public float HorizontalDistanceTo(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        // =====================================================================
        // HEALTH
        // =====================================================================

        public void ResetForRound(float maxHealth)
        {
            MaxHealth = Health = maxHealth;
            HealthChanged?.Invoke(Health, MaxHealth);
            _cooldown = 0f;
            _triggerHeld = false;
            _bonusTiers = 0;
            _bonusTimer = 0f;
            Heat.Reset();
            SpreadTierChanged?.Invoke(SpreadTier);
        }

        /// <summary>Damage and firing only happen while the round is being played.</summary>
        public void SetCombatActive(bool active)
        {
            _invulnerable = !active;
            _armed = active;
            if (!active) _triggerHeld = false;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_invulnerable || !IsAlive || info.Amount <= 0f) return;

            Health = Mathf.Max(0f, Health - info.Amount);
            HealthChanged?.Invoke(Health, MaxHealth);
            Damaged?.Invoke(info);

            _shakeTimer = shakeDuration;
            AudioManager.Instance?.Play(SoundId.PlayerHurt);
#if UNITY_ANDROID || UNITY_IOS
            if (GameSettings.Vibration) Handheld.Vibrate();
#endif
            if (Health <= 0f)
            {
                AudioManager.Instance?.Play(SoundId.PlayerDeath);
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            HealthChanged?.Invoke(Health, MaxHealth);
        }

        // =====================================================================
        // WEAPON
        // =====================================================================

        /// <summary>Hold-to-fire input from the HUD fire button.</summary>
        public void SetTriggerHeld(bool held) => _triggerHeld = held;

        /// <summary>Multi-Shot card: +tiers for a duration (picking another refreshes the timer).</summary>
        public void AddSpreadTier(int tiers, float duration)
        {
            _bonusTiers = Mathf.Clamp(_bonusTiers + tiers, 0, 3);
            _bonusTimer = duration;
            SpreadTierChanged?.Invoke(SpreadTier);
        }

        public void ReleaseAllBolts() => _bolts.ReleaseAll();

        void Update()
        {
            float dt = Time.deltaTime;
            Heat.Tick(dt);
            _cooldown -= dt;

            if (_bonusTimer > 0f)
            {
                _bonusTimer -= dt;
                if (_bonusTimer <= 0f)
                {
                    _bonusTiers = 0;
                    SpreadTierChanged?.Invoke(SpreadTier);
                }
            }

            bool wantsFire = _triggerHeld;
#if UNITY_EDITOR
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.isPressed) wantsFire = true;
#endif
            if (_armed && wantsFire && _cooldown <= 0f && !Heat.IsOverheated) Fire();

            AnimateViewmodel(dt);
        }

        void Fire()
        {
            _cooldown = _weapon.FireInterval;

            // Aim at whatever is under the crosshair, so bolts from the off-centre muzzle converge on it.
            Transform cam = playerCamera.transform;
            Vector3 target = Physics.Raycast(cam.position, cam.forward, out var hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : cam.position + cam.forward * maxAimDistance;
            Vector3 aim = (target - muzzle.position).normalized;

            // Spread tiers: Single, Twin, Tri, Quad fanned ~8 degrees apart.
            _directions.Clear();
            float start = -_weapon.SpreadAngle * (SpreadTier - 1) * 0.5f;
            for (int i = 0; i < SpreadTier; i++)
                _directions.Add(Quaternion.AngleAxis(start + _weapon.SpreadAngle * i, cam.up) * aim);

            foreach (var dir in _directions) LaunchBolt(muzzle.position, dir, 0, true);

            Heat.AddHeat(_weapon.HeatPerShot);
            AudioManager.Instance?.Play(SoundId.PlayerShoot);
            _flashTimer = 0.05f;
            _recoil = 1f;
        }

        void LaunchBolt(Vector3 position, Vector3 dir, int bounces, bool canSplit)
        {
            var bolt = _bolts.Get(position, Quaternion.LookRotation(dir));
            bolt.LaunchLaser(dir, _weapon, bounces, canSplit, SplitBolt);
        }

        /// <summary>Called by a bolt passing through a prism: two extra bolts at +/- the split angle.</summary>
        void SplitBolt(Vector3 position, Vector3 direction, int bounces)
        {
            for (int sign = -1; sign <= 1; sign += 2)
                LaunchBolt(position, Quaternion.AngleAxis(_weapon.PrismSplitAngle * sign, Vector3.up) * direction, bounces, false);
        }

        void AnimateViewmodel(float dt)
        {
            if (muzzleFlash)
            {
                _flashTimer -= dt;
                bool show = _flashTimer > 0f;
                if (muzzleFlash.activeSelf != show) muzzleFlash.SetActive(show);
            }
            if (gunModel)
            {
                _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 10f);
                gunModel.localPosition = _gunRest + new Vector3(0f, 0.004f, -0.02f) * _recoil;
            }
            if (viewmodel)
            {
                // Damage shake on the gun only; uses unscaled time so it still plays during the death slow-mo.
                if (_shakeTimer > 0f)
                {
                    _shakeTimer -= Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(_shakeTimer / shakeDuration) * shakeStrength;
                    viewmodel.localPosition = _viewmodelRest + (Vector3)(UnityEngine.Random.insideUnitCircle * k);
                }
                else viewmodel.localPosition = _viewmodelRest;
            }
        }
    }

    /// <summary>
    /// Heat instead of ammo: each shot adds heat, heat cools over time, 100% locks firing for a moment.
    /// Plain C# class (no scene presence) ticked by the Player.
    /// </summary>
    public class HeatSystem
    {
        readonly float _coolPerSecond;
        readonly float _lockTime;
        float _lockTimer;

        public float Heat01 { get; private set; }
        public bool IsOverheated => _lockTimer > 0f;

        public event Action<float> HeatChanged;
        public event Action<bool> OverheatChanged;

        public HeatSystem(float coolPerSecond, float lockTime)
        {
            _coolPerSecond = coolPerSecond;
            _lockTime = Mathf.Max(0.01f, lockTime);
        }

        public void Reset()
        {
            bool was = IsOverheated;
            Heat01 = 0f;
            _lockTimer = 0f;
            HeatChanged?.Invoke(0f);
            if (was) OverheatChanged?.Invoke(false);
        }

        public void AddHeat(float amount)
        {
            if (IsOverheated) return;
            Heat01 = Mathf.Clamp01(Heat01 + amount);
            HeatChanged?.Invoke(Heat01);
            if (Heat01 < 1f) return;
            _lockTimer = _lockTime;
            OverheatChanged?.Invoke(true);
        }

        public void Tick(float dt)
        {
            if (Heat01 <= 0f && !IsOverheated) return;
            if (IsOverheated)
            {
                _lockTimer -= dt;
                Heat01 = Mathf.Clamp01(_lockTimer / _lockTime); // bar drains while locked
                if (_lockTimer <= 0f)
                {
                    _lockTimer = 0f;
                    Heat01 = 0f;
                    OverheatChanged?.Invoke(false);
                }
            }
            else Heat01 = Mathf.Max(0f, Heat01 - _coolPerSecond * dt);
            HeatChanged?.Invoke(Heat01);
        }
    }
}
