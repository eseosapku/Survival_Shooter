using System;
using Ricochet.Enemies;
using UnityEngine;

namespace Ricochet.Core
{
    /// <summary>Data sent when an enemy dies.</summary>
    public readonly struct EnemyKilledArgs
    {
        public readonly EnemyType Type;
        public readonly int BaseScore;
        public readonly float Multiplier;
        public readonly int Bounces;
        public readonly Vector3 Position;

        public EnemyKilledArgs(EnemyType type, int baseScore, float multiplier, int bounces, Vector3 position)
        {
            Type = type;
            BaseScore = baseScore;
            Multiplier = multiplier;
            Bounces = bounces;
            Position = position;
        }
    }

    /// <summary>
    /// Game-wide events (Observer pattern) for things many unrelated systems care about.
    /// Publishers don't know who listens: enemies just announce "I died", and the score system,
    /// UI and audio react independently.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<EnemyKilledArgs> EnemyKilled;
        public static event Action<Vector3> EnemySpawned;

        public static void RaiseEnemyKilled(EnemyKilledArgs args) => EnemyKilled?.Invoke(args);
        public static void RaiseEnemySpawned(Vector3 position) => EnemySpawned?.Invoke(position);

        // Static events survive play sessions when domain reload is disabled, so clear them on startup.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            EnemyKilled = null;
            EnemySpawned = null;
        }
    }
}
