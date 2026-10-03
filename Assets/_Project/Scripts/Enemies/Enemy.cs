using System;
using Ricochet.Audio;
using Ricochet.Core;
using Ricochet.Data;
using Ricochet.Pooling;
using UnityEngine;

namespace Ricochet.Enemies
{
    /// <summary>
    /// Abstract base for every enemy (Abstraction + Inheritance).
    /// Handles everything enemies share: health, rising out of the floor, moving on the floor towards the
    /// player, facing the player, hit feedback, slow effects, death and returning to the pool.
    /// Derived classes only decide WHAT to do each frame (TickBehaviour) and HOW to attack (Attack): Polymorphism.
    ///
    /// Prefab layout: scripts + collider on the root, visuals under a child "Model" (easy to swap for a real model).
    /// </summary>
    [RequireComponent(typeof(HitFlash))]
    public abstract class Enemy : MonoBehaviour, IPoolable, IDamageable
    {
        enum LifeState { Rising, Active, Dying }

        [SerializeField] protected EnemyStats stats;
        [SerializeField] protected Transform model;
        [SerializeField] Collider hitCollider;
        [SerializeField] Color deathColor = new Color(0.4f, 1f, 0.4f);
        [SerializeField, Min(0.05f)] float riseDuration = 0.6f;
        [SerializeField, Min(0.05f)] float deathDuration = 0.45f;
        [SerializeField, Min(0f)] float knockbackDistance = 0.08f;
        [SerializeField, Min(0f)] float turnSpeed = 8f;

        HitFlash _flash;
        Animator _animator;
        Action<Enemy> _release;
        LifeState _state;
        float _stateTimer;
        float _cooldown;
        float _slowFactor = 1f;
        float _slowTimer;
        Vector3 _knockback;
        float _walkPhase;
        float _lunge;
        Vector3 _modelRestPos;
        Quaternion _modelRestRot;

        protected EnemyContext Ctx { get; private set; }
        protected bool IsMoving { get; set; }

        public abstract EnemyType Type { get; }
        public EnemyStats Stats => stats;
        public int Health { get; private set; }
        public bool IsAlive => _state != LifeState.Dying && Health > 0;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int DieHash = Animator.StringToHash("Die");

        protected virtual void Awake()
        {
            _flash = GetComponent<HitFlash>();
            _animator = GetComponentInChildren<Animator>();
            if (model)
            {
                _modelRestPos = model.localPosition;
                _modelRestRot = model.localRotation;
            }
        }

        /// <summary>Given once by the factory: how to give this enemy back to its pool.</summary>
        public void BindRelease(Action<Enemy> release) => _release = release;

        /// <summary>Called by the factory right after taking the enemy from the pool.</summary>
        public void Activate(EnemyContext context)
        {
            Ctx = context;
            SnapToFloor();
        }

        // ---------- IPoolable: full reset on reuse ----------

        public virtual void OnSpawned()
        {
            Health = stats.HitsToKill;
            _state = LifeState.Rising;
            _stateTimer = 0f;
            _cooldown = stats.AttackCooldown * 0.5f;
            _slowFactor = 1f;
            _slowTimer = 0f;
            _knockback = Vector3.zero;
            _lunge = 0f;
            IsMoving = false;
            if (hitCollider) hitCollider.enabled = true;
            if (model)
            {
                model.localPosition = _modelRestPos;
                model.localRotation = _modelRestRot;
                model.localScale = new Vector3(0.6f, 0.01f, 0.6f);
            }
            _flash.ResetVisuals();
        }

        public virtual void OnDespawned()
        {
            Ctx = null;
        }

        // ---------- Loop ----------

        void Update()
        {
            if (Ctx == null) return;
            float dt = Time.deltaTime;
            _stateTimer += dt;

            switch (_state)
            {
                case LifeState.Rising:
                    // Grows up out of the floor (pivot is at the feet, so nothing renders below the floor).
                    float t = Mathf.Clamp01(_stateTimer / riseDuration);
                    float e = 1f - (1f - t) * (1f - t);
                    if (model) model.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, e), e, Mathf.Lerp(0.6f, 1f, e));
                    FacePlayer(dt);
                    if (t >= 1f) { _state = LifeState.Active; _stateTimer = 0f; }
                    break;

                case LifeState.Active:
                    UpdateSlow(dt);
                    _cooldown -= dt * _slowFactor;
                    IsMoving = false;
                    TickBehaviour(dt);
                    ApplyKnockback(dt);
                    break;

                case LifeState.Dying:
                    float d = Mathf.Clamp01(_stateTimer / deathDuration);
                    if (model) model.localScale = new Vector3(1f + d * 0.3f, 1f - d, 1f + d * 0.3f);
                    if (d >= 1f) _release?.Invoke(this);
                    break;
            }

            AnimateModel(dt);
        }

        /// <summary>What this enemy type does every frame while active.</summary>
        protected abstract void TickBehaviour(float dt);

        /// <summary>This enemy type's attack.</summary>
        protected abstract void Attack();

        // ---------- Helpers for derived classes ----------

        protected float DistanceToPlayer => Ctx.Player.HorizontalDistanceTo(transform.position);
        protected bool CooldownReady => _cooldown <= 0f;
        /// <summary>0 right after attacking, 1 when the next attack is ready.</summary>
        protected float AttackCharge01 => 1f - Mathf.Clamp01(_cooldown / stats.AttackCooldown);
        protected void ResetCooldown() => _cooldown = stats.AttackCooldown;
        protected float ScaledDamage => stats.Damage * Ctx.DamageMultiplier;

        /// <summary>Walks along the floor towards the player, stopping at stopDistance.</summary>
        protected void MoveTowardPlayer(float dt, float stopDistance)
        {
            Vector3 target = Ctx.Arena.ProjectToFloor(Ctx.Player.Position);
            Vector3 pos = transform.position;
            Vector3 flat = target - pos;
            flat.y = 0f;
            float dist = flat.magnitude;
            if (dist <= stopDistance) return;

            float step = Mathf.Min(stats.MoveSpeed * _slowFactor * dt, dist - stopDistance);
            pos += flat / dist * step;
            pos.y = Ctx.Arena.FloorHeight;
            transform.position = pos;
            IsMoving = true;
        }

        protected void FacePlayer(float dt)
        {
            Vector3 flat = Ctx.Player.Position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            var look = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSpeed * dt));
        }

        protected void PlayAttackAnimation()
        {
            _lunge = 1f;
            if (_animator) _animator.SetTrigger(AttackHash);
        }

        void SnapToFloor()
        {
            var p = transform.position;
            p.y = Ctx.Arena.FloorHeight;
            transform.position = p;
            Vector3 flat = Ctx.Player.Position - p;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        // ---------- Damage ----------

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            Health -= Mathf.Max(1, Mathf.RoundToInt(info.Amount));
            _flash.Flash();
            Vector3 push = info.Direction;
            push.y = 0f;
            _knockback += push.normalized * knockbackDistance;
            if (_animator) _animator.SetTrigger(HitHash);
            AudioManager.Instance?.PlayAt(SoundId.EnemyHit, info.Point);

            if (Health <= 0) Die(info);
        }

        void Die(DamageInfo killingBlow)
        {
            _state = LifeState.Dying;
            _stateTimer = 0f;
            if (hitCollider) hitCollider.enabled = false;
            if (_animator) _animator.SetTrigger(DieHash);

            Vector3 centre = transform.position + Vector3.up * 0.9f;
            VfxManager.Instance?.Burst(centre, deathColor, 22, 1.1f);
            AudioManager.Instance?.PlayAt(SoundId.EnemyDeath, centre);
            GameEvents.RaiseEnemyKilled(new EnemyKilledArgs(Type, stats.ScoreValue, killingBlow.ScoreMultiplier, killingBlow.Bounces, centre));
        }

        /// <summary>Freeze Pulse card: slows movement and attacks.</summary>
        public void ApplySlow(float speedFactor, float duration)
        {
            if (!IsAlive) return;
            _slowFactor = Mathf.Clamp01(speedFactor);
            _slowTimer = duration;
            _flash.SetTint(new Color(0.5f, 0.8f, 1.6f));
        }

        void UpdateSlow(float dt)
        {
            if (_slowTimer <= 0f) return;
            _slowTimer -= dt;
            if (_slowTimer <= 0f)
            {
                _slowFactor = 1f;
                _flash.SetTint(Color.white);
            }
        }

        void ApplyKnockback(float dt)
        {
            if (_knockback.sqrMagnitude < 0.000001f) return;
            Vector3 step = Vector3.Lerp(Vector3.zero, _knockback, 1f - Mathf.Exp(-15f * dt));
            transform.position += step;
            _knockback -= step;
        }

        /// <summary>
        /// Simple procedural animation for placeholder models (bob while walking, lunge when attacking).
        /// If a real model has an Animator, the "Speed" parameter is driven instead.
        /// </summary>
        void AnimateModel(float dt)
        {
            if (!model) return;
            if (_animator)
            {
                _animator.SetFloat(SpeedHash, IsMoving ? stats.MoveSpeed * _slowFactor : 0f);
                return;
            }
            if (_state != LifeState.Active) return;

            if (IsMoving) _walkPhase += dt * 9f * _slowFactor;
            float bob = IsMoving ? Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.04f : 0f;
            float sway = IsMoving ? Mathf.Sin(_walkPhase) * 4f : 0f;
            _lunge = Mathf.MoveTowards(_lunge, 0f, dt * 3f);
            float lungeCurve = Mathf.Sin(_lunge * Mathf.PI);

            model.localPosition = _modelRestPos + new Vector3(0f, bob, lungeCurve * 0.15f);
            model.localRotation = _modelRestRot * Quaternion.Euler(lungeCurve * 18f, 0f, sway);
        }
    }
}
