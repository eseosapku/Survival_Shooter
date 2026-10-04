using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ricochet
{
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public const int PointsPerSecond = 5;
        public const int WinBonus = 500;

        public static GameManager Instance { get; private set; }

        [SerializeField] GameConfig config;
        [SerializeField] ARController ar;
        [SerializeField] Player player;
        [SerializeField] Transform poolRoot;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] ParticleSystem puffs;

        StateMachine machine;
        MenuState menu;
        InstructionsState instructions;
        LeaderboardState leaderboard;
        ScanState scan;
        SetupState setup;
        CountdownState countdown;
        PlayState play;
        PauseState pause;
        GameOverState gameOver;

        public GameConfig Config => config;
        public StateMachine States => machine;
        public GameStateId State => machine.Current.Id;
        public ARController AR => ar;
        public Player Player => player;
        public EnemyFactory Enemies { get; private set; }
        public EnemySpawner Spawner { get; private set; }
        public CardSystem Cards { get; private set; }
        public Score Score { get; } = new Score();
        public RoundTimer Timer { get; } = new RoundTimer();
        public Leaderboard Leaderboard { get; private set; }

        public int DifficultyIndex { get; private set; }
        public DifficultySettings Difficulty => config.Difficulty(DifficultyIndex);
        public RunResult LastRun { get; private set; }
        public bool NewBest { get; private set; }

        public event Action<int> CountdownTick;
        public event Action<int> DifficultyChanged;
        public event Action<RunResult, bool> RoundEnded;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Physics.queriesHitBackfaces = true;
            Vfx.Init(sparks, puffs);

            DifficultyIndex = Mathf.Clamp(Settings.Difficulty, 0, config.DifficultyCount - 1);
            Leaderboard = new Leaderboard();
            Enemies = new EnemyFactory(config, poolRoot);
            Cards = new CardSystem(config, poolRoot, player, Enemies);
            Spawner = new EnemySpawner(Enemies, () => Cards.HasMirrors);

            machine = new StateMachine();
            menu = new MenuState(this);
            instructions = new InstructionsState(this);
            leaderboard = new LeaderboardState(this);
            scan = new ScanState(this);
            setup = new SetupState(this);
            countdown = new CountdownState(this);
            play = new PlayState(this);
            pause = new PauseState(this);
            gameOver = new GameOverState(this);

            GameEvents.EnemyKilled += OnEnemyKilled;
        }

        void Start()
        {
            machine.Change(menu);
            AudioManager.Instance?.PlayMusic();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            Time.timeScale = 1f;
            Instance = null;
        }

        void Update()
        {
            machine.Tick();
#if UNITY_EDITOR
            DebugKeys();
#endif
        }

        public void StartGame()
        {
            if (ar.HasArena) machine.Change(setup);
            else machine.Change(scan);
        }

        public void ShowInstructions() => machine.Change(instructions);
        public void ShowLeaderboard() => machine.Change(leaderboard);
        public void ShowMenu() => machine.Change(menu);
        public void Restart() => StartGame();
        public void ShowSetup() => machine.Change(setup);
        public void StartCountdown() => machine.Change(countdown);
        public void StartRound() => machine.Change(play);

        public void Pause()
        {
            if (machine.Current == play && !play.Dying) machine.Change(pause);
        }

        public void Resume()
        {
            if (machine.Current == pause) machine.Change(play);
        }

        public void SetDifficulty(int index)
        {
            DifficultyIndex = Mathf.Clamp(index, 0, config.DifficultyCount - 1);
            Settings.Difficulty = DifficultyIndex;
            DifficultyChanged?.Invoke(DifficultyIndex);
        }

        public void ShowCountdown(int number) => CountdownTick?.Invoke(number);

        public void ResetRound()
        {
            ClearBoard();
            var d = Difficulty;
            play.Reset();
            player.Respawn(d.PlayerHealth);
            Score.Reset();
            Timer.Reset(d.RoundLength);
            Enemies.Setup(player, ar.Arena, d.DamageMultiplier);
            Spawner.Setup(d, () => Timer.Progress, player, ar.Arena);
            Cards.Setup(ar.Arena);
        }

        public void EnableCombat(bool on)
        {
            player.EnableCombat(on);
            Spawner.Enable(on);
            Cards.Enable(on);
        }

        public void ClearBoard()
        {
            Enemies.ReturnAll();
            player.ClearBolts();
            Cards.ClearRound();
        }

        public void EndRound(bool survived)
        {
            if (survived) Score.Add(WinBonus);

            LastRun = new RunResult
            {
                score = Score.Points,
                enemiesDefeated = Score.Kills,
                walkers = Score.Walkers,
                spitters = Score.Spitters,
                ghosts = Score.Ghosts,
                timeSurvived = Timer.Elapsed,
                difficulty = Difficulty.Name,
                survived = survived,
                dateIso = DateTime.Now.ToString("o")
            };
            NewBest = Leaderboard.Add(LastRun);
            if (survived) AudioManager.Instance?.Play(SoundId.RoundWin);

            machine.Change(gameOver);
            RoundEnded?.Invoke(LastRun, NewBest);
        }

        void OnEnemyKilled(KillInfo kill)
        {
            if (machine.Current == play) Score.AddKill(kill);
        }

#if UNITY_EDITOR
        void DebugKeys()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) ShowMenu();
            if (kb.f2Key.wasPressedThisFrame) StartGame();
            if (kb.f3Key.wasPressedThisFrame && machine.Current == setup) StartCountdown();
            if (kb.f4Key.wasPressedThisFrame && machine.Current == play) EndRound(true);
            if (kb.f5Key.wasPressedThisFrame && machine.Current == play)
                player.TakeDamage(new Hit(9999f, player.Position, Vector3.forward));
            if (kb.pKey.wasPressedThisFrame)
            {
                if (machine.Current == pause) Resume();
                else Pause();
            }
        }
#endif
    }

    public enum GameStateId { MainMenu, Instructions, Leaderboard, Scanning, Setup, Countdown, Playing, Paused, GameOver }

    public interface IGameState
    {
        GameStateId Id { get; }
        void Enter();
        void Tick();
        void Exit();
    }

    public class StateMachine
    {
        public IGameState Current { get; private set; }
        public event Action<IGameState, IGameState> Changed;

        public void Change(IGameState next)
        {
            if (next == null || next == Current) return;
            var previous = Current;
            previous?.Exit();
            Current = next;
            Current.Enter();
            Changed?.Invoke(previous, Current);
        }

        public void Tick() => Current?.Tick();
    }

    public abstract class GameState : IGameState
    {
        protected readonly GameManager Game;
        protected GameState(GameManager game) => Game = game;
        public abstract GameStateId Id { get; }
        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    public class MenuState : GameState
    {
        public MenuState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.MainMenu;

        public override void Enter()
        {
            Time.timeScale = 1f;
            Game.AR.EnablePlacement(false);
            Game.EnableCombat(false);
            Game.ClearBoard();
        }
    }

    public class InstructionsState : GameState
    {
        public InstructionsState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Instructions;
    }

    public class LeaderboardState : GameState
    {
        public LeaderboardState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Leaderboard;
    }

    public class ScanState : GameState
    {
        public ScanState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Scanning;

        public override void Enter()
        {
            Game.AR.ArenaPlaced += OnPlaced;
            Game.AR.EnablePlacement(true);
        }

        public override void Exit()
        {
            Game.AR.ArenaPlaced -= OnPlaced;
            Game.AR.EnablePlacement(false);
        }

        void OnPlaced(Arena arena) => Game.ShowSetup();
    }

    public class SetupState : GameState
    {
        public SetupState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Setup;

        public override void Enter()
        {
            Time.timeScale = 1f;
            Game.EnableCombat(false);
            Game.ClearBoard();
            Game.Cards.Setup(Game.AR.Arena);
            Game.AR.Tapped += OnTap;
        }

        public override void Exit() => Game.AR.Tapped -= OnTap;

        void OnTap(Vector2 screenPoint) => Game.Cards.PlaceSetupMirror(screenPoint);
    }

    public class CountdownState : GameState
    {
        float timeLeft;
        int shown;

        public CountdownState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Countdown;

        public override void Enter()
        {
            Time.timeScale = 1f;
            Game.ResetRound();
            timeLeft = 3f;
            shown = -1;
        }

        public override void Tick()
        {
            timeLeft -= Time.deltaTime;
            int number = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            if (number != shown)
            {
                shown = number;
                Game.ShowCountdown(number);
                AudioManager.Instance?.Play(number > 0 ? SoundId.CountdownBeep : SoundId.CountdownGo);
            }
            if (timeLeft <= -0.4f) Game.StartRound();
        }
    }

    public class PlayState : GameState
    {
        const float SlowMotion = 0.35f;
        const float DeathDelay = 0.9f;

        bool dying;
        float deathTimer;
        int secondsScored;

        public PlayState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Playing;
        public bool Dying => dying;

        public void Reset()
        {
            dying = false;
            secondsScored = 0;
        }

        public override void Enter()
        {
            Time.timeScale = dying ? SlowMotion : 1f;
            Game.Player.Died += OnDied;
            if (!dying) Game.EnableCombat(true);
        }

        public override void Exit()
        {
            Game.Player.Died -= OnDied;
            Game.EnableCombat(false);
        }

        public override void Tick()
        {
            if (dying)
            {
                deathTimer -= Time.unscaledDeltaTime;
                if (deathTimer <= 0f) Game.EndRound(false);
                return;
            }

            float dt = Time.deltaTime;
            Game.Timer.Tick(dt);
            Game.Spawner.Tick(dt);
            Game.Cards.Tick(dt);

            int seconds = Mathf.FloorToInt(Game.Timer.Elapsed);
            if (seconds > secondsScored)
            {
                Game.Score.Add((seconds - secondsScored) * GameManager.PointsPerSecond);
                secondsScored = seconds;
            }

            if (Game.Timer.Done) Game.EndRound(true);
        }

        void OnDied()
        {
            if (dying) return;
            dying = true;
            deathTimer = DeathDelay;
            Game.EnableCombat(false);
            Time.timeScale = SlowMotion;
        }
    }

    public class PauseState : GameState
    {
        public PauseState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Paused;
        public override void Enter() => Time.timeScale = 0f;
        public override void Exit() => Time.timeScale = 1f;
    }

    public class GameOverState : GameState
    {
        public GameOverState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.GameOver;

        public override void Enter()
        {
            Time.timeScale = 1f;
            Game.EnableCombat(false);
            Game.ClearBoard();
        }
    }

    public class RoundTimer
    {
        int lastShown = -1;

        public float Length { get; private set; }
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, Length - Elapsed);
        public float Progress => Length > 0f ? Mathf.Clamp01(Elapsed / Length) : 0f;
        public bool Done => Elapsed >= Length;

        public event Action<float> Changed;

        public void Reset(float length)
        {
            Length = length;
            Elapsed = 0f;
            lastShown = -1;
            Notify();
        }

        public void Tick(float dt)
        {
            if (Done) return;
            Elapsed = Mathf.Min(Length, Elapsed + dt);
            Notify();
        }

        void Notify()
        {
            int whole = Mathf.CeilToInt(Remaining);
            if (whole == lastShown) return;
            lastShown = whole;
            Changed?.Invoke(whole);
        }
    }

    public class Score
    {
        public int Points { get; private set; }
        public int Walkers { get; private set; }
        public int Spitters { get; private set; }
        public int Ghosts { get; private set; }
        public int Kills => Walkers + Spitters + Ghosts;

        public event Action<int> Changed;
        public event Action<int, float, Vector3> Gained;

        public void Reset()
        {
            Points = Walkers = Spitters = Ghosts = 0;
            Changed?.Invoke(Points);
        }

        public void AddKill(KillInfo kill)
        {
            if (kill.Type == EnemyType.Walker) Walkers++;
            else if (kill.Type == EnemyType.Spitter) Spitters++;
            else Ghosts++;

            int points = Mathf.RoundToInt(kill.BaseScore * kill.Multiplier);
            Add(points);
            Gained?.Invoke(points, kill.Multiplier, kill.Position);
        }

        public void Add(int points)
        {
            if (points == 0) return;
            Points += points;
            Changed?.Invoke(Points);
        }
    }

    public readonly struct KillInfo
    {
        public readonly EnemyType Type;
        public readonly int BaseScore;
        public readonly float Multiplier;
        public readonly Vector3 Position;

        public KillInfo(EnemyType type, int baseScore, float multiplier, Vector3 position)
        {
            Type = type;
            BaseScore = baseScore;
            Multiplier = multiplier;
            Position = position;
        }
    }

    public static class GameEvents
    {
        public static event Action<KillInfo> EnemyKilled;
        public static event Action<Vector3> HitBlocked;

        public static void Killed(KillInfo kill) => EnemyKilled?.Invoke(kill);
        public static void Blocked(Vector3 position) => HitBlocked?.Invoke(position);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            EnemyKilled = null;
            HitBlocked = null;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(Hit hit);
    }

    public struct Hit
    {
        public float Damage;
        public Vector3 Point;
        public Vector3 Direction;
        public float Multiplier;
        public bool FromMirror;

        public Hit(float damage, Vector3 point, Vector3 direction, float multiplier = 1f, bool fromMirror = false)
        {
            Damage = damage;
            Point = point;
            Direction = direction;
            Multiplier = multiplier;
            FromMirror = fromMirror;
        }
    }

    [Serializable]
    public class RunResult
    {
        public int score;
        public int enemiesDefeated;
        public int walkers;
        public int spitters;
        public int ghosts;
        public float timeSurvived;
        public string difficulty;
        public bool survived;
        public string dateIso;
    }

    [Serializable]
    public class RunList
    {
        public List<RunResult> sessions = new List<RunResult>();
    }

    public class Leaderboard
    {
        public const int MaxRuns = 5;
        const string BestKey = "Ricochet.BestScore";

        readonly string path = Path.Combine(Application.persistentDataPath, "leaderboard.json");
        RunList data = new RunList();

        public event Action Changed;
        public IReadOnlyList<RunResult> Runs => data.sessions;
        public int Best => PlayerPrefs.GetInt(BestKey, 0);

        public Leaderboard() => Load();

        void Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonUtility.FromJson<RunList>(File.ReadAllText(path));
                    if (loaded != null && loaded.sessions != null) data = loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Leaderboard file could not be read: " + e.Message);
                data = new RunList();
            }
            Trim();
        }

        public bool Add(RunResult run)
        {
            data.sessions.Insert(0, run);
            Trim();
            bool best = run.score > Best;
            if (best)
            {
                PlayerPrefs.SetInt(BestKey, run.score);
                PlayerPrefs.Save();
            }
            Save();
            Changed?.Invoke();
            return best;
        }

        public void Clear()
        {
            data.sessions.Clear();
            Save();
            Changed?.Invoke();
        }

        void Trim()
        {
            if (data.sessions.Count > MaxRuns)
                data.sessions.RemoveRange(MaxRuns, data.sessions.Count - MaxRuns);
        }

        void Save()
        {
            try { File.WriteAllText(path, JsonUtility.ToJson(data, true)); }
            catch (Exception e) { Debug.LogWarning("Leaderboard could not be saved: " + e.Message); }
        }
    }

    public static class Settings
    {
        public static int Difficulty
        {
            get => PlayerPrefs.GetInt("Ricochet.Difficulty", 1);
            set { PlayerPrefs.SetInt("Ricochet.Difficulty", value); PlayerPrefs.Save(); }
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("Ricochet.Vibration", 1) == 1;
            set { PlayerPrefs.SetInt("Ricochet.Vibration", value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }

    public static class Vfx
    {
        static ParticleSystem sparks;
        static ParticleSystem puffs;

        public static void Init(ParticleSystem sparkSystem, ParticleSystem puffSystem)
        {
            sparks = sparkSystem;
            puffs = puffSystem;
        }

        public static void Sparks(Vector3 position, Vector3 normal, Color color, int count = 10, float speed = 1.5f)
        {
            if (!sparks) return;
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                p.position = position;
                p.velocity = (normal + UnityEngine.Random.insideUnitSphere * 0.9f).normalized * speed * UnityEngine.Random.Range(0.4f, 1f);
                p.startSize = UnityEngine.Random.Range(0.012f, 0.025f);
                p.startLifetime = UnityEngine.Random.Range(0.15f, 0.35f);
                sparks.Emit(p, 1);
            }
        }

        public static void Burst(Vector3 position, Color color, int count = 16, float speed = 0.8f, float size = 0.06f)
        {
            if (!puffs) return;
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                p.position = position + UnityEngine.Random.insideUnitSphere * 0.05f;
                p.velocity = UnityEngine.Random.insideUnitSphere * speed + Vector3.up * speed * 0.5f;
                p.startSize = size * UnityEngine.Random.Range(0.6f, 1.3f);
                p.startLifetime = UnityEngine.Random.Range(0.3f, 0.6f);
                puffs.Emit(p, 1);
            }
        }

        public static void Ring(Vector3 center, float radius, Color color, int count = 24)
        {
            if (!puffs) return;
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.position = center + dir * radius + Vector3.up * 0.02f;
                p.velocity = dir * 0.3f + Vector3.up * 0.4f;
                p.startSize = 0.05f;
                p.startLifetime = 0.5f;
                puffs.Emit(p, 1);
            }
        }
    }

    public static class TouchUI
    {
        static readonly List<RaycastResult> results = new List<RaycastResult>();
        static PointerEventData pointer;
        static EventSystem owner;

        public static bool IsOverUI(Vector2 screenPoint)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (pointer == null || owner != es)
            {
                pointer = new PointerEventData(es);
                owner = es;
            }
            pointer.position = screenPoint;
            results.Clear();
            es.RaycastAll(pointer, results);
            return results.Count > 0;
        }
    }
}
