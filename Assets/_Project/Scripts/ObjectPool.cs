using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ricochet
{
    /// <summary>
    /// Optional contract for pooled objects. OnSpawned must reset ALL runtime state,
    /// so a reused object behaves exactly like a brand-new one.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>Non-generic view of a pool so pools of different types can be listed together.</summary>
    public interface IPoolStats
    {
        string Name { get; }
        int CountAll { get; }
        int CountActive { get; }
        int GrowCount { get; }
    }

    /// <summary>
    /// Generic, pre-warmed object pool (Object Pool pattern).
    /// Every instance is created up front (in Awake), so gameplay never calls Instantiate/Destroy.
    /// If the pool runs dry it grows by one and logs a warning, so an undersized pool is easy to spot.
    /// </summary>
    public class ObjectPool<T> : IPoolStats where T : Component
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
        public IReadOnlyList<T> Active => _active;

        /// <param name="onCreate">One-time setup per instance, e.g. giving it a way to return itself.</param>
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
                Debug.LogWarning($"[ObjectPool] '{Name}' was empty and had to grow. Increase its pre-warm count.");
                item = CreateInstance();
            }

            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            _active.Add(item);
            (item as IPoolable)?.OnSpawned();
            return item;
        }

        /// <summary>Returns an object to the pool. A second call for the same object is ignored.</summary>
        public void Release(T item)
        {
            if (item == null || !_active.Remove(item)) return;
            (item as IPoolable)?.OnDespawned();
            item.gameObject.SetActive(false);
            if (_parent != null && item.transform.parent != _parent)
                item.transform.SetParent(_parent, false);
            _inactive.Push(item);
        }

        public void ReleaseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Release(_active[i]);
        }
    }

    /// <summary>Lists every pool so the pause screen can prove nothing is instantiated during play ("grown 0").</summary>
    public static class PoolRegistry
    {
        static readonly List<IPoolStats> s_Pools = new List<IPoolStats>();

        public static void Register(IPoolStats pool) => s_Pools.Add(pool);

        // Static state survives play sessions when domain reload is disabled, so clear it on startup.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear() => s_Pools.Clear();

        public static string BuildReport()
        {
            var sb = new StringBuilder("POOLS  (active / total, grown)\n");
            foreach (var p in s_Pools)
                sb.AppendLine($"{p.Name}: {p.CountActive}/{p.CountAll}, grown {p.GrowCount}");
            return sb.ToString();
        }
    }
}
