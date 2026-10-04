using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Ricochet
{
    public class PlayerContext
    {
        public Player Player;
        public EnemyFactory Enemies;
        public CardSystem Cards;
        public CardSettings Settings;
    }

    public abstract class AbilityCard
    {
        public abstract string Title { get; }
        public abstract Color Color { get; }
        public abstract void Activate(PlayerContext ctx);
    }

    public class MultiShotCard : AbilityCard
    {
        public override string Title => "MULTI-SHOT";
        public override Color Color => new Color(1f, 0.6f, 0.1f);
        public override void Activate(PlayerContext ctx) => ctx.Player.AddSpread(1, ctx.Settings.MultiShotTime);
    }

    public class MirrorCard : AbilityCard
    {
        public override string Title => "MIRROR +1";
        public override Color Color => new Color(0.3f, 0.9f, 1f);
        public override void Activate(PlayerContext ctx) => ctx.Cards.AddCharge(Gadget.Mirror);
    }

    public class PrismCard : AbilityCard
    {
        public override string Title => "PRISM +1";
        public override Color Color => new Color(1f, 0.3f, 0.9f);
        public override void Activate(PlayerContext ctx) => ctx.Cards.AddCharge(Gadget.Prism);
    }

    public class FreezeCard : AbilityCard
    {
        public override string Title => "FREEZE PULSE";
        public override Color Color => new Color(0.6f, 0.8f, 1f);

        public override void Activate(PlayerContext ctx)
        {
            var s = ctx.Settings;
            ctx.Enemies.ForEach(e =>
            {
                if (ctx.Player.FlatDistance(e.transform.position) <= s.FreezeRadius)
                    e.Slow(s.FreezeSpeed, s.FreezeTime);
            });
            Vector3 p = ctx.Player.Position;
            Vfx.Ring(new Vector3(p.x, ctx.Cards.FloorHeight, p.z), s.FreezeRadius, Color, 40);
        }
    }

    public class MedKitCard : AbilityCard
    {
        public override string Title => "MED KIT +30";
        public override Color Color => new Color(0.3f, 1f, 0.4f);
        public override void Activate(PlayerContext ctx) => ctx.Player.Heal(ctx.Settings.Heal);
    }

    public enum Gadget { Mirror, Prism }

    public class CardSystem
    {
        const float MaxReach = 6f;
        const float MirrorHalfHeight = 0.32f;

        class FloorCard
        {
            public AbilityCard Card;
            public Transform Visual;
            public ObjectPool<Transform> Pool;
            public float Age;
        }

        class Placed
        {
            public Transform Root;
            public ObjectPool<Transform> Pool;
            public Gadget Kind;
            public float TimeLeft;
        }

        readonly CardSettings settings;
        readonly Player player;
        readonly PlayerContext context;
        readonly AbilityCard[] cards = { new MultiShotCard(), new MirrorCard(), new PrismCard(), new FreezeCard(), new MedKitCard() };
        readonly ObjectPool<Transform>[] cardPools;
        readonly ObjectPool<Transform> mirrors, prisms;
        readonly List<FloorCard> onFloor = new List<FloorCard>();
        readonly List<Placed> placed = new List<Placed>();
        readonly List<Transform> setupMirrors = new List<Transform>();
        readonly int floorMask, wallMask;
        Arena arena;
        bool running;
        float timer;

        public int MirrorCharges { get; private set; }
        public int PrismCharges { get; private set; }
        public int SetupMirrorCount => setupMirrors.Count;
        public int SetupMirrorLimit => settings.SetupMirrors;
        public float FloorHeight => arena != null ? arena.Height : 0f;

        public bool HasMirrors
        {
            get
            {
                if (setupMirrors.Count > 0) return true;
                foreach (var p in placed)
                    if (p.Kind == Gadget.Mirror) return true;
                return false;
            }
        }

        public event Action<int, int> ChargesChanged;
        public event Action<int, int> SetupMirrorsChanged;
        public event Action<AbilityCard> Collected;

        public CardSystem(GameConfig config, Transform poolRoot, Player player, EnemyFactory enemies)
        {
            settings = config.Cards;
            this.player = player;
            context = new PlayerContext { Player = player, Enemies = enemies, Cards = this, Settings = settings };
            floorMask = LayerMask.GetMask("ARFloor");
            wallMask = LayerMask.GetMask("ReflectiveWall");

            cardPools = new ObjectPool<Transform>[cards.Length];
            for (int i = 0; i < cards.Length; i++)
                cardPools[i] = new ObjectPool<Transform>(config.CardPrefabs[i], 2, poolRoot);
            mirrors = new ObjectPool<Transform>(config.MirrorPrefab, 8, poolRoot);
            prisms = new ObjectPool<Transform>(config.PrismPrefab, 4, poolRoot);
        }

        public void Setup(Arena area)
        {
            arena = area;
            timer = settings.FirstDelay;
            MirrorCharges = PrismCharges = 0;
            ChargesChanged?.Invoke(0, 0);
            SetupMirrorsChanged?.Invoke(setupMirrors.Count, settings.SetupMirrors);
        }

        public void Enable(bool on) => running = on && arena != null;

        public void Tick(float dt)
        {
            if (!running) return;

            if ((timer -= dt) <= 0f)
            {
                timer = settings.SpawnInterval;
                if (onFloor.Count < settings.MaxOnFloor) DropCard();
            }

            for (int i = onFloor.Count - 1; i >= 0; i--)
            {
                var c = onFloor[i];
                c.Age += dt;
                Spin(c);
                if (player.FlatDistance(c.Visual.position) <= settings.PickupDistance) Collect(i);
                else if (c.Age >= settings.CardLifetime) RemoveCard(i);
            }

            for (int i = placed.Count - 1; i >= 0; i--)
            {
                var g = placed[i];
                g.TimeLeft -= dt;
                var visual = g.Root.Find("Visual");
                if (visual) visual.localScale = Vector3.one * Mathf.Clamp(g.TimeLeft, 0.01f, 1f);
                var spinner = g.Root.Find("Visual/Spinner");
                if (spinner) spinner.Rotate(0f, 45f * dt, 0f, Space.Self);
                if (g.TimeLeft > 0f) continue;
                g.Pool.Return(g.Root);
                placed.RemoveAt(i);
            }
        }

        void DropCard()
        {
            int index = Random.Range(0, cards.Length);
            Vector3 pos = arena.Center;
            for (int i = 0; i < 10; i++)
            {
                Vector2 r = Random.insideUnitCircle * settings.SpawnRadius;
                Vector3 spot = arena.Center + new Vector3(r.x, 0f, r.y);
                if (player.FlatDistance(spot) > settings.PickupDistance * 1.5f && arena.IsOnFloor(spot))
                {
                    pos = spot;
                    break;
                }
            }
            pos = arena.ToFloor(pos);
            var visual = cardPools[index].Get(pos, Quaternion.identity);
            onFloor.Add(new FloorCard { Card = cards[index], Visual = visual, Pool = cardPools[index] });
            Vfx.Ring(pos, 0.2f, cards[index].Color, 16);
        }

        static void Spin(FloorCard c)
        {
            var card = c.Visual.Find("Visual");
            if (!card) return;
            card.localPosition = new Vector3(0f, 0.25f + Mathf.Sin(c.Age * 2.5f) * 0.03f, 0f);
            card.localRotation = Quaternion.Euler(0f, c.Age * 90f, 0f);
        }

        void Collect(int index)
        {
            var c = onFloor[index];
            c.Card.Activate(context);
            Vfx.Burst(c.Visual.position + Vector3.up * 0.2f, c.Card.Color, 18, 0.9f, 0.05f);
            AudioManager.Instance?.Play(SoundId.CardPickup);
            Collected?.Invoke(c.Card);
            RemoveCard(index);
        }

        void RemoveCard(int index)
        {
            onFloor[index].Pool.Return(onFloor[index].Visual);
            onFloor.RemoveAt(index);
        }

        public void AddCharge(Gadget kind)
        {
            if (kind == Gadget.Mirror) MirrorCharges++;
            else PrismCharges++;
            ChargesChanged?.Invoke(MirrorCharges, PrismCharges);
        }

        public bool PlaceGadget(Gadget kind)
        {
            if (arena == null || (kind == Gadget.Mirror ? MirrorCharges : PrismCharges) <= 0) return false;
            Transform cam = player.Camera.transform;
            if (!FindSpot(new Ray(cam.position, cam.forward), kind == Gadget.Mirror, out var pos, out var rot))
            {
                Vector3 fwd = cam.forward;
                fwd.y = 0f;
                pos = arena.ToFloor(cam.position + fwd.normalized * 1.2f);
                rot = FaceCamera(pos);
            }

            var pool = kind == Gadget.Mirror ? mirrors : prisms;
            var root = pool.Get(pos, rot);
            ResetVisual(root);
            placed.Add(new Placed { Root = root, Pool = pool, Kind = kind, TimeLeft = settings.GadgetLifetime });

            if (kind == Gadget.Mirror) MirrorCharges--;
            else PrismCharges--;
            ChargesChanged?.Invoke(MirrorCharges, PrismCharges);
            AudioManager.Instance?.PlayAt(SoundId.MirrorPlace, pos);
            return true;
        }

        public bool PlaceSetupMirror(Vector2 screenPoint)
        {
            if (arena == null || setupMirrors.Count >= settings.SetupMirrors) return false;
            var ray = player.Camera.ScreenPointToRay(screenPoint);
            if (!FindSpot(ray, true, out var pos, out var rot)) return false;

            var mirror = mirrors.Get(pos, rot);
            ResetVisual(mirror);
            setupMirrors.Add(mirror);
            Vfx.Sparks(pos + rot * Vector3.up * MirrorHalfHeight, rot * Vector3.forward, new Color(0.3f, 0.9f, 1f), 14);
            AudioManager.Instance?.PlayAt(SoundId.MirrorPlace, pos);
            SetupMirrorsChanged?.Invoke(setupMirrors.Count, settings.SetupMirrors);
            return true;
        }

        public void ClearSetupMirrors()
        {
            foreach (var m in setupMirrors) mirrors.Return(m);
            setupMirrors.Clear();
            SetupMirrorsChanged?.Invoke(0, settings.SetupMirrors);
        }

        bool FindSpot(Ray ray, bool allowWalls, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;
            int mask = allowWalls ? floorMask | wallMask : floorMask;

            if (Physics.Raycast(ray, out var hit, MaxReach, mask, QueryTriggerInteraction.Ignore))
            {
                bool wall = (wallMask & (1 << hit.collider.gameObject.layer)) != 0;
                if (wall)
                {
                    Vector3 normal = hit.normal;
                    if (Vector3.Dot(normal, ray.direction) > 0f) normal = -normal;
                    normal.y = 0f;
                    if (normal.sqrMagnitude < 0.01f) return false;
                    normal.Normalize();
                    pos = hit.point + normal * 0.02f - Vector3.up * MirrorHalfHeight;
                    rot = Quaternion.LookRotation(normal);
                    return true;
                }
                pos = arena.ToFloor(hit.point);
                rot = FaceCamera(pos);
                return true;
            }

            if (ray.direction.y < -0.05f)
            {
                float t = (FloorHeight - ray.origin.y) / ray.direction.y;
                if (t > 0f && t <= MaxReach)
                {
                    pos = arena.ToFloor(ray.origin + ray.direction * t);
                    rot = FaceCamera(pos);
                    return true;
                }
            }
            return false;
        }

        Quaternion FaceCamera(Vector3 pos)
        {
            Vector3 toCam = player.Position - pos;
            toCam.y = 0f;
            return toCam.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCam.normalized) : Quaternion.identity;
        }

        static void ResetVisual(Transform root)
        {
            var visual = root.Find("Visual");
            if (visual) visual.localScale = Vector3.one;
        }

        public void ClearRound()
        {
            foreach (var c in onFloor) c.Pool.Return(c.Visual);
            onFloor.Clear();
            foreach (var g in placed) g.Pool.Return(g.Root);
            placed.Clear();
        }
    }
}
