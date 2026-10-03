using System;
using System.Collections.Generic;
using Ricochet.Audio;
using Ricochet.Data;
using Ricochet.Pooling;
using UnityEngine;

namespace Ricochet.Weapons
{
    /// <summary>
    /// The player's gun. Holds the trigger state (from the HUD fire button), fires pooled LaserBolts
    /// from the muzzle towards whatever is under the crosshair, manages heat and spread tier.
    /// </summary>
    public class LaserBlaster : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] WeaponStats stats;
        [SerializeField] Camera aimCamera;
        [SerializeField] Transform muzzle;
        [SerializeField] Transform gunModel;
        [SerializeField] GameObject muzzleFlash;
        [SerializeField] LaserBolt boltPrefab;
        [SerializeField] Transform poolRoot;

        [Header("Pool & aim")]
        [SerializeField, Min(1)] int boltPoolSize = 40;
        [SerializeField] LayerMask aimMask;
        [SerializeField, Min(1f)] float maxAimDistance = 15f;
        [SerializeField] float prismSplitAngle = 25f;

        ObjectPool<LaserBolt> _pool;
        readonly List<Vector3> _directions = new List<Vector3>(4);
        bool _armed;
        bool _triggerHeld;
        float _cooldown;
        int _bonusTiers;
        float _bonusTimer;
        float _flashTimer;
        float _recoil;
        Vector3 _gunRest;

        public HeatSystem Heat { get; private set; }
        public int SpreadTier => Mathf.Clamp(stats.BaseSpreadTier + _bonusTiers, 1, 4);
        public float SpreadBonusRemaining => _bonusTimer;
        public IPoolStats PoolStats => _pool;

        public event Action<int> SpreadTierChanged;
        public event Action Fired;

        void Awake()
        {
            Heat = new HeatSystem(() => stats.CoolPerSecond, () => stats.OverheatLockTime);
            Heat.OverheatChanged += hot => { if (hot) AudioManager.Instance?.Play(SoundId.Overheat); };

            // Pre-warm: every bolt that will ever be used is created here, before play starts.
            _pool = new ObjectPool<LaserBolt>(boltPrefab, boltPoolSize, poolRoot);
            if (gunModel) _gunRest = gunModel.localPosition;
            if (muzzleFlash) muzzleFlash.SetActive(false);
        }

        // ---------- Commands ----------

        /// <summary>Hold-to-fire input from the HUD button.</summary>
        public void SetTriggerHeld(bool held) => _triggerHeld = held;

        /// <summary>The weapon only fires while the round is being played.</summary>
        public void SetArmed(bool armed)
        {
            _armed = armed;
            if (!armed) _triggerHeld = false;
        }

        public void ResetWeapon()
        {
            _cooldown = 0f;
            _triggerHeld = false;
            _bonusTiers = 0;
            _bonusTimer = 0f;
            Heat.Reset();
            SpreadTierChanged?.Invoke(SpreadTier);
        }

        /// <summary>Multi-Shot card: +tiers for a duration (picking another one refreshes the timer).</summary>
        public void AddSpreadTier(int tiers, float duration)
        {
            _bonusTiers = Mathf.Clamp(_bonusTiers + tiers, 0, 4 - stats.BaseSpreadTier);
            _bonusTimer = duration;
            SpreadTierChanged?.Invoke(SpreadTier);
        }

        public void ReleaseAllBolts() => _pool.ReleaseAll();

        public void ReleaseBolt(LaserBolt bolt) => _pool.Release(bolt);

        /// <summary>Called by a bolt passing through a prism: spawns two extra bolts at +/- the split angle.</summary>
        public void SpawnSplitBolts(Vector3 position, Vector3 direction, int bounces)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 dir = Quaternion.AngleAxis(prismSplitAngle * sign, Vector3.up) * direction;
                var bolt = _pool.Get(position, Quaternion.LookRotation(dir));
                bolt.Launch(this, stats, dir, bounces, canSplit: false);
            }
        }

        // ---------- Loop ----------

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
            if (_armed && wantsFire && _cooldown <= 0f && !Heat.IsOverheated)
                Fire();

            AnimateGun(dt);
        }

        void Fire()
        {
            _cooldown = stats.FireInterval;

            // Aim at whatever is under the crosshair, so bolts from the off-centre muzzle converge on it.
            Transform cam = aimCamera.transform;
            Vector3 target = Physics.Raycast(cam.position, cam.forward, out var hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : cam.position + cam.forward * maxAimDistance;
            Vector3 aim = (target - muzzle.position).normalized;

            SpreadPattern.GetDirections(aim, cam.up, SpreadTier, stats.SpreadAngle, _directions);
            foreach (var dir in _directions)
            {
                var bolt = _pool.Get(muzzle.position, Quaternion.LookRotation(dir));
                bolt.Launch(this, stats, dir);
            }

            Heat.AddHeat(stats.HeatPerShot);
            AudioManager.Instance?.Play(SoundId.PlayerShoot);
            _flashTimer = 0.05f;
            _recoil = 1f;
            Fired?.Invoke();
        }

        void AnimateGun(float dt)
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
        }
    }
}
