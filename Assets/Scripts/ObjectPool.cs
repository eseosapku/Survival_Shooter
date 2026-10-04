using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ricochet
{
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    public interface IPoolInfo
    {
        string Name { get; }
        int Total { get; }
        int InUse { get; }
        int Grown { get; }
    }

    public class ObjectPool<T> : IPoolInfo where T : Component
    {
        readonly T prefab;
        readonly Transform parent;
        readonly Action<T> setup;
        readonly Stack<T> free = new Stack<T>();
        readonly List<T> used = new List<T>();

        public string Name { get; }
        public int Total { get; private set; }
        public int InUse => used.Count;
        public int Grown { get; private set; }
        public IReadOnlyList<T> Active => used;

        public ObjectPool(T prefab, int size, Transform parent, Action<T> setup = null)
        {
            this.prefab = prefab;
            this.parent = parent;
            this.setup = setup;
            Name = prefab.name;
            for (int i = 0; i < size; i++)
                free.Push(Create());
            Pools.Add(this);
        }

        T Create()
        {
            T item = UnityEngine.Object.Instantiate(prefab, parent);
            item.name = prefab.name + " " + Total;
            item.gameObject.SetActive(false);
            Total++;
            setup?.Invoke(item);
            return item;
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T item;
            if (free.Count > 0)
            {
                item = free.Pop();
            }
            else
            {
                Grown++;
                Debug.LogWarning($"Pool '{Name}' ran out and had to grow.");
                item = Create();
            }

            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            used.Add(item);
            (item as IPoolable)?.OnSpawned();
            return item;
        }

        public void Return(T item)
        {
            if (item == null || !used.Remove(item)) return;
            (item as IPoolable)?.OnDespawned();
            item.gameObject.SetActive(false);
            if (parent != null && item.transform.parent != parent)
                item.transform.SetParent(parent, false);
            free.Push(item);
        }

        public void ReturnAll()
        {
            for (int i = used.Count - 1; i >= 0; i--)
                Return(used[i]);
        }
    }

    public static class Pools
    {
        static readonly List<IPoolInfo> all = new List<IPoolInfo>();

        public static void Add(IPoolInfo pool) => all.Add(pool);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear() => all.Clear();

        public static string Report()
        {
            var sb = new StringBuilder("POOLS  (in use / total, grown)\n");
            foreach (var p in all)
                sb.AppendLine($"{p.Name}: {p.InUse}/{p.Total}, grown {p.Grown}");
            return sb.ToString();
        }
    }
}
