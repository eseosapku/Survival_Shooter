using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ricochet.Pooling
{
    /// <summary>
    /// Generic, pre-warmed object pool (Object Pool pattern).
    /// All instances are created up front, so gameplay never calls Instantiate/Destroy.
    /// If the pool runs dry it grows by one and logs a warning, so undersized pools are visible.
    /// </summary>
    public class ObjectPool<T> : IPoolStats where T : Component, IPoolable
    {
        readonly T _prefab;
        readonly Transform _parent;
        readonly Action<T> _onCreate;
        readonly Stack<T> _inactive = new Stack<T>();
        readonly List<T> _active = new List<T>();

        public string Name { get; }
        public int CountAll { get; private set; }
        public int CountActive => _active.Count;
        public int GrowCount { get; private set; }

        /// <summary>Read-only view of the objects currently in use (used to wipe the board).</summary>
        public IReadOnlyList<T> Active => _active;

        /// <param name="prefab">Template to clone.</param>
        /// <param name="prewarm">How many instances to create immediately.</param>
        /// <param name="parent">Scene parent for pooled instances (keeps the Hierarchy tidy).</param>
        /// <param name="onCreate">Optional one-time setup per instance (e.g. give it a reference back to this pool).</param>
        public ObjectPool(T prefab, int prewarm, Transform parent, Action<T> onCreate = null)
        {
            _prefab = prefab;
            _parent = parent;
            _onCreate = onCreate;
            Name = prefab.name;

            for (int i = 0; i < prewarm; i++)
                _inactive.Push(CreateInstance());

            PoolRegistry.Register(this);
        }

        T CreateInstance()
        {
            T item = UnityEngine.Object.Instantiate(_prefab, _parent);
            item.name = $"{_prefab.name}_{CountAll:00}";
            item.gameObject.SetActive(false);
            CountAll++;
            _onCreate?.Invoke(item);
            return item;
        }

        /// <summary>Takes an object out of the pool, activates it and calls OnSpawned.</summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            T item;
            if (_inactive.Count > 0)
            {
                item = _inactive.Pop();
            }
            else
            {
                GrowCount++;
                Debug.LogWarning($"[ObjectPool] '{Name}' was empty and had to grow (size now {CountAll + 1}). Increase its pre-warm count.");
                item = CreateInstance();
            }

            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            _active.Add(item);
            item.OnSpawned();
            return item;
        }

        /// <summary>Returns an object to the pool. Safe to call twice (second call is ignored).</summary>
        public void Release(T item)
        {
            if (item == null || !_active.Remove(item))
                return;

            item.OnDespawned();
            item.gameObject.SetActive(false);
            if (_parent != null && item.transform.parent != _parent)
                item.transform.SetParent(_parent, false);
            _inactive.Push(item);
        }

        /// <summary>Returns every active object (used when a round ends).</summary>
        public void ReleaseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Release(_active[i]);
        }
    }
}
