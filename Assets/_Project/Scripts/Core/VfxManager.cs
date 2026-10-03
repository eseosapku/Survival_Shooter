using UnityEngine;

namespace Ricochet.Core
{
    /// <summary>
    /// Particle effects without spawning objects: two world-space particle systems that emit bursts
    /// at any position on request (hit sparks, bounce sparks, death bursts, spawn rings, acid splats).
    /// ParticleSystem.Emit recycles particles internally, so this is effectively pooled too.
    /// </summary>
    public class VfxManager : MonoBehaviour
    {
        public static VfxManager Instance { get; private set; }

        [SerializeField] ParticleSystem sparks;
        [SerializeField] ParticleSystem puffs;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Fast, small sparks flying away from a surface.</summary>
        public void Sparks(Vector3 position, Vector3 normal, Color color, int count = 10, float speed = 1.5f)
        {
            if (!sparks) return;
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = (normal + Random.insideUnitSphere * 0.9f).normalized;
                p.position = position;
                p.velocity = dir * speed * Random.Range(0.4f, 1f);
                p.startSize = Random.Range(0.012f, 0.025f);
                p.startLifetime = Random.Range(0.15f, 0.35f);
                sparks.Emit(p, 1);
            }
        }

        /// <summary>Bigger, slower puffs (death, spawn, acid splat, card pickup).</summary>
        public void Burst(Vector3 position, Color color, int count = 16, float speed = 0.8f, float size = 0.06f)
        {
            if (!puffs) return;
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                p.position = position + Random.insideUnitSphere * 0.05f;
                p.velocity = Random.insideUnitSphere * speed + Vector3.up * speed * 0.5f;
                p.startSize = size * Random.Range(0.6f, 1.3f);
                p.startLifetime = Random.Range(0.3f, 0.6f);
                puffs.Emit(p, 1);
            }
        }

        /// <summary>A flat ring of particles on the floor (enemy spawn, freeze pulse).</summary>
        public void Ring(Vector3 center, float radius, Color color, int count = 24)
        {
            if (!puffs) return;
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.position = center + offset * radius + Vector3.up * 0.02f;
                p.velocity = offset * 0.3f + Vector3.up * 0.4f;
                p.startSize = 0.05f;
                p.startLifetime = 0.5f;
                puffs.Emit(p, 1);
            }
        }
    }
}
