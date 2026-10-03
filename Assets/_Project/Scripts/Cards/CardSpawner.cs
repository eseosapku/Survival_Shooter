using System;
using System.Collections.Generic;
using Ricochet.AR;
using Ricochet.Audio;
using Ricochet.Core;
using Ricochet.Player;
using Ricochet.Pooling;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Ricochet.Cards
{
    /// <summary>
    /// Drops a random ability card on the floor near the beacon every ~20 s. The player collects it by
    /// physically walking over it (camera within 0.5 m horizontally). Cards expire if ignored.
    /// </summary>
    public class CardSpawner : MonoBehaviour
    {
        [SerializeField] AbilityCard[] cardPrefabs;
        [SerializeField] Transform poolRoot;
        [SerializeField, Min(1)] int poolPerType = 2;
        [SerializeField, Min(1f)] float interval = 20f;
        [SerializeField, Min(0f)] float firstDelay = 10f;
        [SerializeField, Min(0.2f)] float spawnRadius = 1.5f;
        [SerializeField, Min(0.1f)] float pickupDistance = 0.5f;
        [SerializeField, Min(1f)] float cardLifetime = 18f;
        [SerializeField, Min(1)] int maxCardsOnFloor = 2;

        readonly List<ObjectPool<AbilityCard>> _pools = new List<ObjectPool<AbilityCard>>();
        readonly List<AbilityCard> _onFloor = new List<AbilityCard>();
        ArenaContext _arena;
        PlayerController _player;
        PlayerContext _context;
        bool _running;
        float _timer;

        /// <summary>Raised when the player picks a card up (HUD shows its name).</summary>
        public event Action<AbilityCard> CardCollected;

        void Awake()
        {
            foreach (var prefab in cardPrefabs)
            {
                ObjectPool<AbilityCard> pool = null;
                pool = new ObjectPool<AbilityCard>(prefab, poolPerType, poolRoot, c => c.BindRelease(x => pool.Release(x)));
                _pools.Add(pool);
            }
        }

        public void Configure(ArenaContext arena, PlayerController player, PlayerContext context)
        {
            _arena = arena;
            _player = player;
            _context = context;
            _timer = firstDelay;
        }

        public void SetRunning(bool running) => _running = running && _arena != null;

        void Update()
        {
            if (!_running) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = interval;
                if (_onFloor.Count < maxCardsOnFloor) SpawnRandomCard();
            }

            for (int i = _onFloor.Count - 1; i >= 0; i--)
            {
                var card = _onFloor[i];
                if (_player.HorizontalDistanceTo(card.transform.position) <= pickupDistance)
                {
                    Collect(card);
                }
                else if (card.Age >= cardLifetime)
                {
                    _onFloor.RemoveAt(i);
                    card.Release();
                }
            }
        }

        void SpawnRandomCard()
        {
            if (_pools.Count == 0) return;
            var pool = _pools[Random.Range(0, _pools.Count)];

            Vector3 pos = _arena.Center;
            for (int i = 0; i < 10; i++)
            {
                Vector2 r = Random.insideUnitCircle * spawnRadius;
                Vector3 c = _arena.Center + new Vector3(r.x, 0f, r.y);
                // Not right under the player's feet, and on the floor.
                if (_player.HorizontalDistanceTo(c) > pickupDistance * 1.5f && _arena.IsOnFloor(c))
                {
                    pos = c;
                    break;
                }
            }
            pos = _arena.ProjectToFloor(pos);
            var card = pool.Get(pos, Quaternion.identity);
            _onFloor.Add(card);
            VfxManager.Instance?.Ring(pos, 0.2f, card.Color, 16);
        }

        void Collect(AbilityCard card)
        {
            _onFloor.Remove(card);
            card.Activate(_context);
            VfxManager.Instance?.Burst(card.transform.position + Vector3.up * 0.2f, card.Color, 18, 0.9f, 0.05f);
            AudioManager.Instance?.Play(SoundId.CardPickup);
            CardCollected?.Invoke(card);
            card.Release();
        }

        public void ReleaseAll()
        {
            _onFloor.Clear();
            foreach (var pool in _pools) pool.ReleaseAll();
        }
    }
}
