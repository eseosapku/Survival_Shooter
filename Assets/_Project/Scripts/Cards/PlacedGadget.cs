using System;
using Ricochet.Pooling;
using UnityEngine;

namespace Ricochet.Cards
{
    /// <summary>
    /// Base for objects the player places on the floor (Mirror, Prism). Pooled, expires after a lifetime
    /// and shrinks away during its last second. The collider on the gadget's layer is what lasers interact with.
    /// </summary>
    public abstract class PlacedGadget : MonoBehaviour, IPoolable
    {
        [SerializeField, Min(1f)] float lifetime = 30f;
        [SerializeField] Transform visual;

        Action<PlacedGadget> _release;
        float _remaining;

        public float Remaining => _remaining;

        public void BindRelease(Action<PlacedGadget> release) => _release = release;

        public virtual void OnSpawned()
        {
            _remaining = lifetime;
            if (visual) visual.localScale = Vector3.one;
        }

        public virtual void OnDespawned() { }

        protected virtual void Update()
        {
            _remaining -= Time.deltaTime;
            if (visual && _remaining < 1f)
                visual.localScale = Vector3.one * Mathf.Max(0.01f, _remaining);
            if (_remaining <= 0f) _release?.Invoke(this);
        }
    }
}
