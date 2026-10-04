using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ricochet
{
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

        ObjectPool<Projectile> bolts;
        readonly List<Vector3> directions = new List<Vector3>(4);
        WeaponStats weapon;
        bool invulnerable = true;
        bool armed;
        bool firing;
        float cooldown;
        int extraSpread;
        float spreadTimer;
        float flashTimer;
        float recoil;
        float shakeTimer;
        Vector3 gunStart;
        Vector3 viewmodelStart;

        public Camera Camera => playerCamera;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 Target => hurtbox ? hurtbox.transform.TransformPoint(hurtbox.center) : transform.position;

        public float MaxHealth { get; private set; } = 100f;
        public float Health { get; private set; } = 100f;
        public float HealthPercent => MaxHealth > 0f ? Health / MaxHealth : 0f;
        public bool IsAlive => Health > 0f;

        public Heat Heat { get; private set; }
        public int SpreadLevel => Mathf.Clamp(1 + extraSpread, 1, 4);
        public float SpreadTimeLeft => spreadTimer;

        public event Action<float, float> HealthChanged;
        public event Action<Hit> Damaged;
        public event Action Died;
        public event Action<int> SpreadChanged;

        void Awake()
        {
            weapon = config.Weapon;
            Heat = new Heat(weapon.CoolRate, weapon.LockTime);
            Heat.OverheatChanged += hot => { if (hot) AudioManager.Instance?.Play(SoundId.Overheat); };

            ObjectPool<Projectile> pool = null;
            pool = new ObjectPool<Projectile>(config.LaserPrefab, boltPoolSize, boltPoolRoot, b => b.SetReturn(x => pool.Return(x)));
            bolts = pool;

            if (gunModel) gunStart = gunModel.localPosition;
            if (viewmodel) viewmodelStart = viewmodel.localPosition;
            if (muzzleFlash) muzzleFlash.SetActive(false);
        }

        public float FlatDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        public void Respawn(float maxHealth)
        {
            MaxHealth = Health = maxHealth;
            HealthChanged?.Invoke(Health, MaxHealth);
            cooldown = 0f;
            firing = false;
            extraSpread = 0;
            spreadTimer = 0f;
            Heat.Reset();
            SpreadChanged?.Invoke(SpreadLevel);
        }

        public void EnableCombat(bool on)
        {
            invulnerable = !on;
            armed = on;
            if (!on) firing = false;
        }

        public void TakeDamage(Hit hit)
        {
            if (invulnerable || !IsAlive || hit.Damage <= 0f) return;

            Health = Mathf.Max(0f, Health - hit.Damage);
            HealthChanged?.Invoke(Health, MaxHealth);
            Damaged?.Invoke(hit);

            shakeTimer = shakeDuration;
            AudioManager.Instance?.Play(SoundId.PlayerHurt);
#if UNITY_ANDROID || UNITY_IOS
            if (Settings.Vibration) Handheld.Vibrate();
#endif
            if (Health > 0f) return;
            AudioManager.Instance?.Play(SoundId.PlayerDeath);
            Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            HealthChanged?.Invoke(Health, MaxHealth);
        }

        public void SetFiring(bool on) => firing = on;

        public void AddSpread(int levels, float duration)
        {
            extraSpread = Mathf.Clamp(extraSpread + levels, 0, 3);
            spreadTimer = duration;
            SpreadChanged?.Invoke(SpreadLevel);
        }

        public void ClearBolts() => bolts.ReturnAll();

        void Update()
        {
            float dt = Time.deltaTime;
            Heat.Tick(dt);
            cooldown -= dt;

            if (spreadTimer > 0f)
            {
                spreadTimer -= dt;
                if (spreadTimer <= 0f)
                {
                    extraSpread = 0;
                    SpreadChanged?.Invoke(SpreadLevel);
                }
            }

            bool wantsToFire = firing;
#if UNITY_EDITOR
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.isPressed) wantsToFire = true;
#endif
            if (armed && wantsToFire && cooldown <= 0f && !Heat.Overheated) Shoot();

            AnimateGun(dt);
        }

        void Shoot()
        {
            cooldown = weapon.FireDelay;

            Transform cam = playerCamera.transform;
            Vector3 target = Physics.Raycast(cam.position, cam.forward, out var hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : cam.position + cam.forward * maxAimDistance;
            Vector3 aim = (target - muzzle.position).normalized;

            directions.Clear();
            float start = -weapon.Spread * (SpreadLevel - 1) * 0.5f;
            for (int i = 0; i < SpreadLevel; i++)
                directions.Add(Quaternion.AngleAxis(start + weapon.Spread * i, cam.up) * aim);

            foreach (var dir in directions) SpawnBolt(muzzle.position, dir, 0, false, true);

            Heat.Add(weapon.HeatPerShot);
            AudioManager.Instance?.Play(SoundId.PlayerShoot);
            flashTimer = 0.05f;
            recoil = 1f;
        }

        void SpawnBolt(Vector3 position, Vector3 dir, int bounces, bool fromMirror, bool canSplit)
        {
            var bolt = bolts.Get(position, Quaternion.LookRotation(dir));
            bolt.FireLaser(dir, weapon, bounces, fromMirror, canSplit, Split);
        }

        void Split(Vector3 position, Vector3 dir, int bounces, bool fromMirror)
        {
            for (int side = -1; side <= 1; side += 2)
                SpawnBolt(position, Quaternion.AngleAxis(weapon.SplitAngle * side, Vector3.up) * dir, bounces, fromMirror, false);
        }

        void AnimateGun(float dt)
        {
            if (muzzleFlash)
            {
                flashTimer -= dt;
                bool show = flashTimer > 0f;
                if (muzzleFlash.activeSelf != show) muzzleFlash.SetActive(show);
            }

            if (gunModel)
            {
                recoil = Mathf.MoveTowards(recoil, 0f, dt * 10f);
                gunModel.localPosition = gunStart + new Vector3(0f, 0.004f, -0.02f) * recoil;
            }

            if (!viewmodel) return;
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(shakeTimer / shakeDuration) * shakeStrength;
                viewmodel.localPosition = viewmodelStart + (Vector3)(UnityEngine.Random.insideUnitCircle * k);
            }
            else
            {
                viewmodel.localPosition = viewmodelStart;
            }
        }
    }

    public class Heat
    {
        readonly float coolRate;
        readonly float lockTime;
        float lockTimer;

        public float Value { get; private set; }
        public bool Overheated => lockTimer > 0f;

        public event Action<float> Changed;
        public event Action<bool> OverheatChanged;

        public Heat(float coolRate, float lockTime)
        {
            this.coolRate = coolRate;
            this.lockTime = Mathf.Max(0.01f, lockTime);
        }

        public void Reset()
        {
            bool was = Overheated;
            Value = 0f;
            lockTimer = 0f;
            Changed?.Invoke(0f);
            if (was) OverheatChanged?.Invoke(false);
        }

        public void Add(float amount)
        {
            if (Overheated) return;
            Value = Mathf.Clamp01(Value + amount);
            Changed?.Invoke(Value);
            if (Value < 1f) return;
            lockTimer = lockTime;
            OverheatChanged?.Invoke(true);
        }

        public void Tick(float dt)
        {
            if (Value <= 0f && !Overheated) return;
            if (Overheated)
            {
                lockTimer -= dt;
                Value = Mathf.Clamp01(lockTimer / lockTime);
                if (lockTimer <= 0f)
                {
                    lockTimer = 0f;
                    Value = 0f;
                    OverheatChanged?.Invoke(false);
                }
            }
            else
            {
                Value = Mathf.Max(0f, Value - coolRate * dt);
            }
            Changed?.Invoke(Value);
        }
    }
}
