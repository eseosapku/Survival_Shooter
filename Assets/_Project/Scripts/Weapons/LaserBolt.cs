using Ricochet.Audio;
using Ricochet.Core;
using Ricochet.Data;
using Ricochet.Pooling;
using UnityEngine;

namespace Ricochet.Weapons
{
    /// <summary>
    /// A pooled laser bolt. Moves itself with a SphereCast from its current position to where it will be
    /// next frame, so it can never tunnel through thin AR walls and needs no Rigidbody.
    /// - ReflectiveWall / Mirror: reflects (Vector3.Reflect), bounce count +1 (max 3).
    /// - Prism: passes through and splits into 3 (each bolt may only split once).
    /// - Enemy: deals exactly 1 damage, carrying the ricochet multiplier for scoring.
    /// - Floor / timeout: disappears.
    /// </summary>
    public class LaserBolt : MonoBehaviour, IPoolable
    {
        [SerializeField] TrailRenderer trail;
        [SerializeField] LayerMask hitMask;
        [SerializeField] Color sparkColor = new Color(0.3f, 1f, 1f);

        const float Damage = 1f;
        const int MaxCastsPerFrame = 5;

        LaserBlaster _owner;
        WeaponStats _stats;
        Vector3 _direction;
        float _life;
        int _mask;
        int _reflectMask;
        int _prismMask;
        int _enemyLayer;
        bool _active;

        public int Bounces { get; private set; }
        public bool CanSplit { get; private set; }

        void Awake()
        {
            _reflectMask = LayerMask.GetMask("ReflectiveWall", "Mirror");
            _prismMask = LayerMask.GetMask("Prism");
            _enemyLayer = LayerMask.NameToLayer("Enemy");
        }

        public void OnSpawned()
        {
            Bounces = 0;
            CanSplit = true;
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
            if (trail) trail.Clear();
        }

        /// <summary>Starts the bolt. Split bolts inherit the bounce count and can't split again.</summary>
        public void Launch(LaserBlaster owner, WeaponStats stats, Vector3 direction, int bounces = 0, bool canSplit = true)
        {
            _owner = owner;
            _stats = stats;
            _direction = direction.normalized;
            _life = stats.BoltLifetime;
            Bounces = bounces;
            CanSplit = canSplit;
            if (!canSplit) _mask &= ~_prismMask;
            transform.rotation = Quaternion.LookRotation(_direction);
        }

        void Update()
        {
            if (!_active || _stats == null) return;

            float dt = Time.deltaTime;
            float remaining = _stats.BoltSpeed * dt;
            Vector3 pos = transform.position;
            float radius = _stats.BoltRadius;

            for (int i = 0; i < MaxCastsPerFrame && remaining > 0f; i++)
            {
                if (!Physics.SphereCast(pos, radius, _direction, out RaycastHit hit, remaining, _mask, QueryTriggerInteraction.Ignore))
                {
                    pos += _direction * remaining;
                    break;
                }

                Vector3 centreAtHit = pos + _direction * hit.distance;
                remaining -= hit.distance;
                int layerBit = 1 << hit.collider.gameObject.layer;

                if (hit.collider.gameObject.layer == _enemyLayer)
                {
                    HitEnemy(hit);
                    return;
                }

                if ((layerBit & _reflectMask) != 0)
                {
                    Vector3 normal = hit.normal;
                    if (Vector3.Dot(normal, _direction) > 0f) normal = -normal; // hit from behind
                    _direction = Vector3.Reflect(_direction, normal).normalized;
                    Bounces++;
                    VfxManager.Instance?.Sparks(hit.point, normal, sparkColor, 8);
                    AudioManager.Instance?.PlayAt(SoundId.LaserBounce, hit.point);

                    if (Bounces > _stats.MaxBounces)
                    {
                        Despawn();
                        return;
                    }
                    pos = centreAtHit + normal * 0.002f;
                    continue;
                }

                if ((layerBit & _prismMask) != 0)
                {
                    // Pass through and split. This bolt keeps going straight; two more fan out at +/-25 degrees.
                    CanSplit = false;
                    _mask &= ~_prismMask;
                    _owner.SpawnSplitBolts(centreAtHit, _direction, Bounces);
                    VfxManager.Instance?.Sparks(hit.point, -_direction, new Color(1f, 0.5f, 1f), 12);
                    pos = centreAtHit;
                    continue;
                }

                // Floor or anything else: absorb.
                VfxManager.Instance?.Sparks(hit.point, hit.normal, sparkColor, 5, 0.8f);
                Despawn();
                return;
            }

            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(_direction));

            _life -= dt;
            if (_life <= 0f) Despawn();
        }

        void HitEnemy(RaycastHit hit)
        {
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                float multiplier = _stats.MultiplierForBounces(Bounces);
                target.TakeDamage(new DamageInfo(Damage, hit.point, _direction, multiplier, Bounces));
            }
            VfxManager.Instance?.Sparks(hit.point, -_direction, sparkColor, 10);
            Despawn();
        }

        void Despawn()
        {
            if (!_active) return;
            _owner.ReleaseBolt(this);
        }
    }
}
