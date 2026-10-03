using System;
using Ricochet.AR;
using Ricochet.Data;
using Ricochet.Player;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Ricochet.Enemies
{
    /// <summary>
    /// Decides WHEN and WHERE enemies appear; the factory decides HOW.
    /// Over the round the spawn interval shrinks and the Spitter chance grows (both from DifficultySettings).
    /// Spawn points are 2.5-4 m from the player and must be on the placed floor plane
    /// (fallback: inside a radius around the beacon).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] EnemyFactory factory;
        [SerializeField, Min(0.5f)] float minDistance = 2.5f;
        [SerializeField, Min(0.5f)] float maxDistance = 4f;
        [SerializeField, Range(0f, 180f)] float preferredHalfAngle = 75f;
        [SerializeField, Range(0f, 1f)] float preferInFrontChance = 0.7f;
        [SerializeField, Min(1)] int attempts = 16;
        [SerializeField, Min(0f)] float firstSpawnDelay = 1f;

        DifficultySettings _difficulty;
        Func<float> _progress;
        PlayerController _player;
        ArenaContext _arena;
        bool _running;
        float _timer;

        public void Configure(DifficultySettings difficulty, Func<float> progress01, PlayerController player, ArenaContext arena)
        {
            _difficulty = difficulty;
            _progress = progress01;
            _player = player;
            _arena = arena;
            _timer = firstSpawnDelay;
        }

        public void SetRunning(bool running) => _running = running && _difficulty != null && _arena != null;

        void Update()
        {
            if (!_running) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            float p = _progress();
            _timer = _difficulty.SpawnIntervalAt(p);

            if (factory.AliveCount >= _difficulty.MaxEnemiesAlive) return;

            var type = Random.value < _difficulty.SpitterChanceAt(p) ? EnemyType.Spitter : EnemyType.Walker;
            factory.Create(type, FindSpawnPoint());
        }

        Vector3 FindSpawnPoint()
        {
            Vector3 playerFloor = _arena.ProjectToFloor(_player.Position);
            Vector3 forward = _player.Forward;
            forward.y = 0f;
            float facing = forward.sqrMagnitude > 0.001f ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg : 0f;

            for (int i = 0; i < attempts; i++)
            {
                // Mostly in front of the player so they can see enemies rise, sometimes anywhere.
                float angle = Random.value < preferInFrontChance
                    ? facing + Random.Range(-preferredHalfAngle, preferredHalfAngle)
                    : Random.Range(0f, 360f);
                float dist = Random.Range(minDistance, maxDistance);
                Vector3 candidate = playerFloor + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
                if (_arena.IsOnFloor(candidate)) return candidate;
            }

            // Fallback (floor plane too small / not detected): a point within the radius around the beacon
            // that still respects the minimum distance from the player, so nothing spawns in the player's face.
            Vector3 best = Vector3.zero;
            float bestDist = -1f;
            for (int i = 0; i < attempts; i++)
            {
                float angle = facing + Random.Range(-preferredHalfAngle, preferredHalfAngle);
                Vector3 c = playerFloor + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(minDistance, maxDistance);
                if (_arena.IsWithinFallback(c)) return _arena.ProjectToFloor(c);

                float d = Vector3.Distance(c, _arena.Center);
                if (bestDist < 0f || d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            // Last resort: the candidate closest to the beacon (still 2.5-4 m from the player).
            return _arena.ProjectToFloor(best);
        }
    }
}
