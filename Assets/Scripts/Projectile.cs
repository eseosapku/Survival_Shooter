using System;
using UnityEngine;

namespace Ricochet
{
    public class Projectile : MonoBehaviour, IPoolable
    {
        public enum Team { Player, Enemy }

        [SerializeField] Team team = Team.Player;
        [SerializeField] LayerMask hitMask;
        [SerializeField] TrailRenderer trail;
        [SerializeField] Color impactColor = new Color(0.3f, 1f, 1f);
        [SerializeField] Color[] bounceColors =
        {
            new Color(0.3f, 1f, 1f),
            new Color(1f, 0.25f, 0.85f),
            new Color(1f, 0.8f, 0.2f),
            new Color(1f, 1f, 1f)
        };

        const int MaxStepsPerFrame = 5;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        Action<Projectile> onReturn;
        Action<Vector3, Vector3, int, bool> onSplit;
        WeaponStats weapon;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        Vector3 direction;
        float speed, radius, damage, life;
        int maxBounces, mask, mirrorMask, wallMask, prismMask;
        bool active;

        public int Bounces { get; private set; }
        public bool FromMirror { get; private set; }

        void Awake()
        {
            mirrorMask = LayerMask.GetMask("Mirror");
            wallMask = LayerMask.GetMask("ReflectiveWall");
            prismMask = LayerMask.GetMask("Prism");
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        public void SetReturn(Action<Projectile> callback) => onReturn = callback;

        public void OnSpawned()
        {
            Bounces = 0;
            FromMirror = false;
            mask = hitMask;
            active = true;
            if (trail)
            {
                trail.Clear();
                trail.emitting = true;
            }
        }

        public void OnDespawned()
        {
            active = false;
            onSplit = null;
            if (trail) trail.Clear();
        }

        public void FireLaser(Vector3 dir, WeaponStats stats, int bounces, bool fromMirror, bool canSplit, Action<Vector3, Vector3, int, bool> split)
        {
            weapon = stats;
            direction = dir.normalized;
            speed = stats.Speed;
            radius = stats.Radius;
            life = stats.Lifetime;
            maxBounces = stats.MaxBounces;
            damage = 1f;
            Bounces = bounces;
            FromMirror = fromMirror;
            onSplit = split;
            if (!canSplit) mask &= ~prismMask;
            SetColor(bounceColors[Mathf.Min(Bounces, bounceColors.Length - 1)]);
        }

        public void FireAcid(Vector3 dir, float shotSpeed, float shotDamage)
        {
            weapon = null;
            direction = dir.normalized;
            speed = shotSpeed;
            radius = 0.06f;
            life = 4f;
            maxBounces = 0;
            damage = shotDamage;
        }

        void SetColor(Color c)
        {
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, c);
                r.SetPropertyBlock(block);
            }
            if (trail)
            {
                trail.startColor = c;
                trail.endColor = new Color(c.r, c.g, c.b, 0f);
            }
        }

        void Update()
        {
            if (!active) return;
            float dt = Time.deltaTime;
            float distance = speed * dt;
            Vector3 pos = transform.position;

            for (int i = 0; i < MaxStepsPerFrame && distance > 0f; i++)
            {
                if (!Physics.SphereCast(pos, radius, direction, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
                {
                    pos += direction * distance;
                    break;
                }

                Vector3 centre = pos + direction * hit.distance;
                distance -= hit.distance;
                int layer = 1 << hit.collider.gameObject.layer;

                if (team == Team.Player && (layer & (wallMask | mirrorMask)) != 0)
                {
                    Vector3 normal = Vector3.Dot(hit.normal, direction) > 0f ? -hit.normal : hit.normal;
                    direction = Vector3.Reflect(direction, normal).normalized;
                    Bounces++;
                    if ((layer & mirrorMask) != 0) FromMirror = true;

                    Color c = bounceColors[Mathf.Min(Bounces, bounceColors.Length - 1)];
                    SetColor(c);
                    Vfx.Sparks(hit.point, normal, c, 10);
                    AudioManager.Instance?.PlayAt(SoundId.LaserBounce, hit.point);

                    if (Bounces > maxBounces)
                    {
                        Finish();
                        return;
                    }
                    pos = centre + normal * 0.002f;
                    continue;
                }

                if (team == Team.Player && (layer & prismMask) != 0)
                {
                    mask &= ~prismMask;
                    onSplit?.Invoke(centre, direction, Bounces, FromMirror);
                    Vfx.Sparks(hit.point, -direction, new Color(1f, 0.5f, 1f), 12);
                    pos = centre;
                    continue;
                }

                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    float multiplier = weapon != null ? weapon.Multiplier(Bounces) : 1f;
                    target.TakeDamage(new Hit(damage, hit.point, direction, multiplier, FromMirror));
                }

                Vfx.Sparks(hit.point, hit.normal, impactColor, 8, 0.8f);
                Finish();
                return;
            }

            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(direction));
            life -= dt;
            if (life <= 0f) Finish();
        }

        void Finish()
        {
            if (active) onReturn?.Invoke(this);
        }
    }
}
