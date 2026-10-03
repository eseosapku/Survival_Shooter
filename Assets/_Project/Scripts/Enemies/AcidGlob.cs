using System;
using Ricochet.Core;
using Ricochet.Pooling;
using UnityEngine;

namespace Ricochet.Enemies
{
    /// <summary>
    /// Pooled Spitter projectile. Same SphereCast movement as the laser bolt, but it hits the player's hurtbox,
    /// and splats on walls/floor. Never hits enemies (its mask doesn't include them).
    /// </summary>
    public class AcidGlob : MonoBehaviour, IPoolable
    {
        [SerializeField] LayerMask hitMask;
        [SerializeField, Min(0.01f)] float radius = 0.06f;
        [SerializeField, Min(0.1f)] float lifetime = 4f;
        [SerializeField] Color splatColor = new Color(0.6f, 1f, 0.1f);
        [SerializeField] TrailRenderer trail;

        Action<AcidGlob> _release;
        Vector3 _velocity;
        float _damage;
        float _life;
        bool _active;

        public void BindRelease(Action<AcidGlob> release) => _release = release;

        public void OnSpawned()
        {
            _active = true;
            _life = lifetime;
            if (trail) trail.Clear();
        }

        public void OnDespawned()
        {
            _active = false;
            _velocity = Vector3.zero;
            if (trail) trail.Clear();
        }

        public void Launch(Vector3 direction, float speed, float damage)
        {
            _velocity = direction.normalized * speed;
            _damage = damage;
        }

        void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            float step = _velocity.magnitude * dt;

            if (step > 0f && Physics.SphereCast(pos, radius, _velocity.normalized, out var hit, step, hitMask, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<IDamageable>();
                target?.TakeDamage(new DamageInfo(_damage, hit.point, _velocity.normalized));
                VfxManager.Instance?.Burst(hit.point, splatColor, 10, 0.6f, 0.04f);
                _release?.Invoke(this);
                return;
            }

            transform.position = pos + _velocity * dt;
            _life -= dt;
            if (_life <= 0f) _release?.Invoke(this);
        }
    }
}
