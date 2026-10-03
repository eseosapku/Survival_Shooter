using System;
using Ricochet.Enemies;
using UnityEngine;

namespace Ricochet.Core
{
    /// <summary>
    /// Keeps score and kill counts for the current round.
    /// Kill score = enemy base score x ricochet multiplier of the killing bolt.
    /// </summary>
    public class ScoreSystem
    {
        public int Score { get; private set; }
        public int Kills => Walkers + Spitters;
        public int Walkers { get; private set; }
        public int Spitters { get; private set; }

        public event Action<int> ScoreChanged;
        public event Action<int> KillsChanged;

        /// <summary>(points, multiplier, world position) for floating "+150 x2 RICOCHET!" text.</summary>
        public event Action<int, float, Vector3> PointsAwarded;

        public void Reset()
        {
            Score = 0;
            Walkers = 0;
            Spitters = 0;
            ScoreChanged?.Invoke(Score);
            KillsChanged?.Invoke(Kills);
        }

        public void AddKill(EnemyKilledArgs kill)
        {
            if (kill.Type == EnemyType.Walker) Walkers++;
            else Spitters++;

            int points = Mathf.RoundToInt(kill.BaseScore * kill.Multiplier);
            AddPoints(points);
            KillsChanged?.Invoke(Kills);
            PointsAwarded?.Invoke(points, kill.Multiplier, kill.Position);
        }

        public void AddPoints(int points)
        {
            if (points == 0) return;
            Score += points;
            ScoreChanged?.Invoke(Score);
        }
    }
}
