using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Ricochet
{
    // =========================================================================
    // ABILITY CARDS: abstract base + 5 subclasses (abstraction, inheritance, polymorphism)
    // =========================================================================

    /// <summary>Everything a card might change when it is picked up.</summary>
    public class PlayerContext
    {
        public Player Player;
        public EnemyFactory Enemies;
        public CardSystem Cards;
        public CardSettings Settings;
    }

    /// <summary>
    /// A collectable ability. The card system calls Activate without knowing which card it is (polymorphism).
    /// </summary>
    public abstract class AbilityCard
    {
        public abstract string Title { get; }
        public abstract Color Color { get; }
        public abstract void Activate(PlayerContext ctx);
    }

    /// <summary>Spread tier +1 (Single → Twin → Tri → Quad) for 12 s.</summary>
    public class MultiShotCard : AbilityCard
    {
        public override string Title => "MULTI-SHOT";
        public override Color Color => new Color(1f, 0.6f, 0.1f);
        public override void Activate(PlayerContext ctx) => ctx.Player.AddSpreadTier(1, ctx.Settings.MultiShotDuration);
    }

    /// <summary>+1 mirror charge (placed with the HUD button).</summary>
    public class MirrorCard : AbilityCard
    {
        public override string Title => "MIRROR +1";
        public override Color Color => new Color(0.3f, 0.9f, 1f);
        public override void Activate(PlayerContext ctx) => ctx.Cards.AddCharge(GadgetType.Mirror);
    }

    /// <summary>+1 prism charge (placed with the HUD button).</summary>
    public class PrismCard : AbilityCard
    {
        public override string Title => "PRISM +1";
        public override Color Color => new Color(1f, 0.3f, 0.9f);
        public override void Activate(PlayerContext ctx) => ctx.Cards.AddCharge(GadgetType.Prism);
    }

    /// <summary>Slows every enemy within 2 m of the player by 70% for 4 s.</summary>
    public class FreezeCard : AbilityCard
    {
        public override string Title => "FREEZE PULSE";
        public override Color Color => new Color(0.6f, 0.8f, 1f);

        public override void Activate(PlayerContext ctx)
        {
            var s = ctx.Settings;
            ctx.Enemies.ForEachAlive(e =>
            {
                if (ctx.Player.HorizontalDistanceTo(e.transform.position) <= s.FreezeRadius)
                    e.ApplySlow(s.FreezeSpeedFactor, s.FreezeDuration);
            });
            Vector3 p = ctx.Player.Position;
            Vfx.Ring(new Vector3(p.x, ctx.Cards.FloorHeight, p.z), s.FreezeRadius, Color, 40);
        }
    }

    /// <summary>Restores 30 HP.</summary>
    public class MedKitCard : AbilityCard
    {
        public override string Title => "MED KIT +30";
        public override Color Color => new Color(0.3f, 1f, 0.4f);
        public override void Activate(PlayerContext ctx) => ctx.Player.Heal(ctx.Settings.MedKitHeal);
    }

    public enum GadgetType { Mirror, Prism }

    // =========================================================================
    // CARD SYSTEM: drops cards, detects walking pickups, places/expires mirrors & prisms
    // =========================================================================

    /// <summary>
    /// Drops a random card near the beacon every ~20 s; the player collects it by physically walking over it
    /// (camera within 0.5 m horizontally). Holds the mirror/prism charges and places them on the real floor
    /// where the crosshair points. Card visuals, mirrors and prisms are all pooled.
    /// Plain C# class ticked by the PlayingState.
    /// </summary>
    public class CardSystem
    {
        const int PoolPerCard = 2;
        const int GadgetPoolSize = 4;
        const float MaxPlaceDistance = 4f;

        class FloorCard { public AbilityCard Card; public Transform Visual; public ObjectPool<Transform> Pool; public float Age; }
        class Gadget { public Transform Root; public ObjectPool<Transform> Pool; public float Remaining; }

        readonly CardSettings _settings;
        readonly Player _player;
        readonly PlayerContext _context;
        readonly AbilityCard[] _cards = { new MultiShotCard(), new MirrorCard(), new PrismCard(), new FreezeCard(), new MedKitCard() };
        readonly ObjectPool<Transform>[] _cardPools;
        readonly ObjectPool<Transform> _mirrors, _prisms;
        readonly List<FloorCard> _onFloor = new List<FloorCard>();
        readonly List<Gadget> _gadgets = new List<Gadget>();
        readonly int _floorMask;
        Arena _arena;
        bool _running;
        float _timer;

        public int MirrorCharges { get; private set; }
        public int PrismCharges { get; private set; }
        public float FloorHeight => _arena != null ? _arena.FloorHeight : 0f;

        /// <summary>(mirrorCharges, prismCharges)</summary>
        public event Action<int, int> ChargesChanged;
        public event Action<AbilityCard> CardCollected;

        public CardSystem(GameConfig config, Transform poolRoot, Player player, EnemyFactory enemies)
        {
            _settings = config.Cards;
            _player = player;
            _context = new PlayerContext { Player = player, Enemies = enemies, Cards = this, Settings = _settings };
            _floorMask = LayerMask.GetMask("ARFloor");

            _cardPools = new ObjectPool<Transform>[_cards.Length];
            for (int i = 0; i < _cards.Length; i++)
                _cardPools[i] = new ObjectPool<Transform>(config.CardPrefabs[i], PoolPerCard, poolRoot);
            _mirrors = new ObjectPool<Transform>(config.MirrorPrefab, GadgetPoolSize, poolRoot);
            _prisms = new ObjectPool<Transform>(config.PrismPrefab, GadgetPoolSize, poolRoot);
        }

        public void Configure(Arena arena)
        {
            _arena = arena;
            _timer = _settings.FirstSpawnDelay;
            MirrorCharges = PrismCharges = 0;
            ChargesChanged?.Invoke(0, 0);
        }

        public void SetRunning(bool running) => _running = running && _arena != null;

        public void Tick(float dt)
        {
            if (!_running) return;

            if ((_timer -= dt) <= 0f)
            {
                _timer = _settings.SpawnInterval;
                if (_onFloor.Count < _settings.MaxOnFloor) SpawnRandomCard();
            }

            for (int i = _onFloor.Count - 1; i >= 0; i--)
            {
                var c = _onFloor[i];
                c.Age += dt;
                AnimateCard(c);
                if (_player.HorizontalDistanceTo(c.Visual.position) <= _settings.PickupDistance) Collect(i);
                else if (c.Age >= _settings.CardLifetime) RemoveCard(i);
            }

            for (int i = _gadgets.Count - 1; i >= 0; i--)
            {
                var g = _gadgets[i];
                g.Remaining -= dt;
                var visual = g.Root.Find("Visual");
                if (visual) visual.localScale = Vector3.one * Mathf.Clamp(g.Remaining, 0.01f, 1f); // shrink away in the last second
                var spinner = g.Root.Find("Visual/Spinner");
                if (spinner) spinner.Rotate(0f, 45f * dt, 0f, Space.Self);
                if (g.Remaining <= 0f)
                {
                    g.Pool.Release(g.Root);
                    _gadgets.RemoveAt(i);
                }
            }
        }

        void SpawnRandomCard()
        {
            int index = Random.Range(0, _cards.Length);
            Vector3 pos = _arena.Center;
            for (int i = 0; i < 10; i++)
            {
                Vector2 r = Random.insideUnitCircle * _settings.SpawnRadius;
                Vector3 c = _arena.Center + new Vector3(r.x, 0f, r.y);
                if (_player.HorizontalDistanceTo(c) > _settings.PickupDistance * 1.5f && _arena.IsOnFloor(c)) { pos = c; break; }
            }
            pos = _arena.ProjectToFloor(pos);
            var visual = _cardPools[index].Get(pos, Quaternion.identity);
            _onFloor.Add(new FloorCard { Card = _cards[index], Visual = visual, Pool = _cardPools[index] });
            Vfx.Ring(pos, 0.2f, _cards[index].Color, 16);
        }

        static void AnimateCard(FloorCard c)
        {
            var card = c.Visual.Find("Visual");
            if (!card) return;
            card.localPosition = new Vector3(0f, 0.25f + Mathf.Sin(c.Age * 2.5f) * 0.03f, 0f);
            card.localRotation = Quaternion.Euler(0f, c.Age * 90f, 0f);
        }

        void Collect(int index)
        {
            var c = _onFloor[index];
            c.Card.Activate(_context); // polymorphic call
            Vfx.Burst(c.Visual.position + Vector3.up * 0.2f, c.Card.Color, 18, 0.9f, 0.05f);
            AudioManager.Instance?.Play(SoundId.CardPickup);
            CardCollected?.Invoke(c.Card);
            RemoveCard(index);
        }

        void RemoveCard(int index)
        {
            _onFloor[index].Pool.Release(_onFloor[index].Visual);
            _onFloor.RemoveAt(index);
        }

        // ---------- Gadgets (mirror / prism) ----------

        public void AddCharge(GadgetType type)
        {
            if (type == GadgetType.Mirror) MirrorCharges++;
            else PrismCharges++;
            ChargesChanged?.Invoke(MirrorCharges, PrismCharges);
        }

        /// <summary>HUD button: places a gadget on the floor under the crosshair, facing the camera.</summary>
        public bool TryPlace(GadgetType type)
        {
            if (_arena == null || (type == GadgetType.Mirror ? MirrorCharges : PrismCharges) <= 0) return false;

            Transform cam = _player.Camera.transform;
            Vector3 pos;
            if (Physics.Raycast(cam.position, cam.forward, out var hit, MaxPlaceDistance, _floorMask, QueryTriggerInteraction.Ignore))
                pos = hit.point;
            else if (cam.forward.y < -0.05f && (cam.position.y - FloorHeight) / -cam.forward.y <= MaxPlaceDistance)
                pos = cam.position + cam.forward * ((FloorHeight - cam.position.y) / cam.forward.y);
            else
            {
                Vector3 fwd = cam.forward;
                fwd.y = 0f;
                pos = cam.position + fwd.normalized * 1.2f;
            }
            pos.y = FloorHeight;

            Vector3 toCam = cam.position - pos;
            toCam.y = 0f;
            var rot = toCam.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCam.normalized) : Quaternion.identity;

            var pool = type == GadgetType.Mirror ? _mirrors : _prisms;
            var root = pool.Get(pos, rot);
            var visual = root.Find("Visual");
            if (visual) visual.localScale = Vector3.one;
            _gadgets.Add(new Gadget { Root = root, Pool = pool, Remaining = _settings.GadgetLifetime });

            if (type == GadgetType.Mirror) MirrorCharges--;
            else PrismCharges--;
            ChargesChanged?.Invoke(MirrorCharges, PrismCharges);
            AudioManager.Instance?.PlayAt(SoundId.MirrorPlace, pos);
            return true;
        }

        /// <summary>Wipes cards and gadgets (end of round).</summary>
        public void ReleaseAll()
        {
            _onFloor.Clear();
            _gadgets.Clear();
            foreach (var p in _cardPools) p.ReleaseAll();
            _mirrors.ReleaseAll();
            _prisms.ReleaseAll();
        }
    }
}
