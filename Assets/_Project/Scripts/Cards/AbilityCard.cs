using System;
using Ricochet.Pooling;
using UnityEngine;

namespace Ricochet.Cards
{
    /// <summary>
    /// Abstract base for a collectable card lying on the floor (Abstraction).
    /// Each card type overrides Activate (Polymorphism): the spawner just calls card.Activate(context)
    /// without knowing which card it is.
    /// </summary>
    public abstract class AbilityCard : MonoBehaviour, IPoolable
    {
        [SerializeField] string title = "CARD";
        [SerializeField] Color color = Color.cyan;
        [SerializeField] Transform visual;

        Action<AbilityCard> _release;
        float _age;
        Vector3 _visualRest;

        public string Title => title;
        public Color Color => color;
        public float Age => _age;

        void Awake()
        {
            if (visual) _visualRest = visual.localPosition;
        }

        public void BindRelease(Action<AbilityCard> release) => _release = release;

        public void Release() => _release?.Invoke(this);

        public void OnSpawned()
        {
            _age = 0f;
            if (visual)
            {
                visual.localPosition = _visualRest;
                visual.localRotation = Quaternion.identity;
            }
        }

        public void OnDespawned() { }

        /// <summary>What the card does when collected.</summary>
        public abstract void Activate(PlayerContext context);

        void Update()
        {
            _age += Time.deltaTime;
            if (!visual) return;
            // Hover and spin so it reads as a pickup.
            visual.localPosition = _visualRest + Vector3.up * (Mathf.Sin(_age * 2.5f) * 0.03f);
            visual.localRotation = Quaternion.Euler(0f, _age * 90f, 0f);
        }
    }
}
