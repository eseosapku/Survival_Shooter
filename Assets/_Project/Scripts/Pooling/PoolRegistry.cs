using System.Collections.Generic;
using System.Text;

namespace Ricochet.Pooling
{
    /// <summary>Non-generic view of a pool, so pools of different types can be listed together.</summary>
    public interface IPoolStats
    {
        string Name { get; }
        int CountAll { get; }
        int CountActive { get; }
        int GrowCount { get; }
    }

    /// <summary>
    /// Keeps track of every pool so their stats can be shown on screen.
    /// This is how we prove that no projectiles are instantiated during play (GrowCount stays 0).
    /// </summary>
    public static class PoolRegistry
    {
        static readonly List<IPoolStats> s_Pools = new List<IPoolStats>();

        public static IReadOnlyList<IPoolStats> Pools => s_Pools;

        public static void Register(IPoolStats pool) => s_Pools.Add(pool);

        // Static state survives play sessions when domain reload is disabled, so reset it explicitly.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => s_Pools.Clear();

        public static string BuildReport()
        {
            var sb = new StringBuilder("POOLS (name: active/total, grown)\n");
            foreach (var p in s_Pools)
                sb.AppendLine($"{p.Name}: {p.CountActive}/{p.CountAll}, grown {p.GrowCount}");
            return sb.ToString();
        }
    }
}
