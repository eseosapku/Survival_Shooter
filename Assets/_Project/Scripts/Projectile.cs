using System;
using UnityEngine;

namespace Ricochet
{
    /// <summary>
    /// One pooled projectile class used for both teams:
    /// - Player laser bolt: reflects off ReflectiveWall/Mirror (Vector3.Reflect, max 3 bounces), splits once in a Prism,
    ///   deals exactly 1 damage to an Enemy carrying the ricochet score multiplier.
    /// - Spitter acid glob: hits the player's hurtbox (or splats on walls/floor).
    /// Movement is a SphereCast from the current position to next frame's position, so it never tunnels through
    /// thin AR walls and needs no Rigidbody. OnSpawned resets all state for reuse.
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        public enum Team { Player, Enemy }

        [SerializeField] Team team = Team.Player;
        [SerializeField] LayerMask hitMask;
        [SerializeField] TrailRenderer trail;
        [SerializeField] Color impactColor = new Color(0.3f, 1f, 1f);

        const int MaxCastsPerFrame = 5;

        Action<Projectile> _release;
        Action<Vector3, Vector3, int> _onSplit;
        WeaponStats _weapon;
        Vector3 _direction;
        float _speed, _radius, _damage, _life;
        int _maxBounces, _mask, _reflectMask, _prismMask;
        bool _active;

        public int Bounces { get; private set; }
        public bool CanSplit { get; private set; }

        void Awake()
        {
            _reflectMask = LayerMask.GetMask("ReflectiveWall", "Mirror");
            _prismMask = LayerMask.GetMask("Prism");
        }

        public void BindRelease(Action<Projectile> release) => _release = release;

        public void OnSpawned()
        {
            Bounces = 0;
            CanSplit = false;
            _mask = hitMask;
            _active = true;
            if (trail)
            {
                trail.Clear();
                trail.emitting = true;
            }
        }

        public void OnDespawned()
        {
            _active = false;
            _onSplit = null;
            if (trail) trail.Clear();
        }

        /// <summary>Player bolt. Split bolts inherit the bounce count and can't split again.</summary>
        public void LaunchLaser(Vector3 direction, WeaponStats weapon, int bounces, bool canSplit, Action<Vector3, Vector3, int> onSplit)
        {
            _weapon = weapon;
            _direction = direction.normalized;
            _speed = weapon.BoltSpeed;
            _radius = weapon.BoltRadius;
            _life = weapon.BoltLifetime;
            _maxBounces = weapon.MaxBounces;
            _damage = 1f;
            Bounces = bounces;
            CanSplit = canSplit;
            _onSplit = onSplit;
            if (!canSplit) _mask &= ~_prismMask;
        }

        /// <summary>Enemy acid glob.</summary>
        public void LaunchAcid(Vector3 direction, float speed, float damage)
        {
            _weapon = null;
            _direction = direction.normalized;
            _speed = speed;
            _radius = 0.06f;
            _life = 4f;
            _maxBounces = 0;
            _damage = damage;
        }

        void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            float remaining = _speed * dt;
            Vector3 pos = transform.position;

            for (int i = 0; i < MaxCastsPerFrame && remaining > 0f; i++)
            {
                if (!Physics.SphereCast(pos, _radius, _direction, out RaycastHit hit, remaining, _mask, QueryTriggerInteraction.Ignore))
                {
                    pos += _direction * remaining;
                    break;
                }

                Vector3 centreAtHit = pos + _direction * hit.distance;
                remaining -= hit.distance;
                int layerBit = 1 << hit.collider.gameObject.layer;

                // Laser hits a wall or mirror: bounce.
                if (team == Team.Player && (layerBit & _reflectMask) != 0)
                {
                    Vector3 normal = Vector3.Dot(hit.normal, _direction) > 0f ? -hit.normal : hit.normal;
                    _direction = Vector3.Reflect(_direction, normal).normalized;
                    Bounces++;
                    Vfx.Sparks(hit.point, normal, impactColor, 8);
                    AudioManager.Instance?.PlayAt(SoundId.LaserBounce, hit.point);
                    if (Bounces > _maxBounces)
                    {
                        Despawn();
                        return;
                    }
                    pos = centreAtHit + normal * 0.002f;
                    continue;
                }

                // Laser passes through a prism: split once.
                if (team == Team.Player && (layerBit & _prismMask) != 0)
                {
                    CanSplit = false;
                    _mask &= ~_prismMask;
                    _onSplit?.Invoke(centreAtHit, _direction, Bounces);
                    Vfx.Sparks(hit.point, -_direction, new Color(1f, 0.5f, 1f), 12);
                    pos = centreAtHit;
                    continue;
                }

                // Anything damageable (enemy for lasers, player hurtbox for acid).
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    float multiplier = _weapon != null ? _weapon.MultiplierForBounces(Bounces) : 1f;
                    target.TakeDamage(new DamageInfo(_damage, hit.point, _direction, multiplier));
                }

                Vfx.Sparks(hit.point, hit.normal, impactColor, 8, 0.8f);
                Despawn();
                return;
            }

            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(_direction));
            _life -= dt;
            if (_life <= 0f) Despawn();
        }

        void Despawn()
        {
            if (_active) _release?.Invoke(this);
        }
    }
}
