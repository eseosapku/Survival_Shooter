using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ricochet
{
    /// <summary>
    /// Central coordinator (Singleton). Owns the state machine, the round's score/timer, the leaderboard,
    /// the enemy spawner/factory and the card system, and exposes simple commands that the UI calls.
    /// It never references UI classes: the UI observes its events instead (Observer).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public const int PointsPerSecond = 5;
        public const int SurvivalBonus = 500;

        public static GameManager Instance { get; private set; }

        [SerializeField] GameConfig config;
        [SerializeField] ARController ar;
        [SerializeField] Player player;
        [SerializeField] Transform poolRoot;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] ParticleSystem puffs;

        GameStateMachine _machine;
        MainMenuState _mainMenu;
        LeaderboardState _leaderboard;
        ScanningState _scanning;
        CountdownState _countdown;
        PlayingState _playing;
        PausedState _paused;
        GameOverState _gameOver;

        public GameConfig Config => config;
        public GameStateMachine StateMachine => _machine;
        public GameStateId CurrentState => _machine.Current.Id;
        public ARController AR => ar;
        public Player Player => player;
        public EnemyFactory Enemies { get; private set; }
        public EnemySpawner Spawner { get; private set; }
        public CardSystem Cards { get; private set; }
        public ScoreSystem Score { get; } = new ScoreSystem();
        public RoundTimer Timer { get; } = new RoundTimer();
        public LeaderboardService Leaderboard { get; private set; }

        public int DifficultyIndex { get; private set; }
        public DifficultySettings ActiveDifficulty => config.GetDifficulty(DifficultyIndex);
        public SessionResult LastResult { get; private set; }
        public bool LastWasNewBest { get; private set; }

        /// <summary>3, 2, 1, then 0 for "GO!".</summary>
        public event Action<int> CountdownTicked;
        public event Action<int> DifficultyChanged;
        /// <summary>(result, isNewBest) once a round is over and saved.</summary>
        public event Action<SessionResult, bool> RoundEnded;

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
            Physics.queriesHitBackfaces = true; // lasers must hit AR wall meshes from either side
            Vfx.Init(sparks, puffs);

            DifficultyIndex = Mathf.Clamp(GameSettings.Difficulty, 0, config.DifficultyCount - 1);
            Leaderboard = new LeaderboardService();
            Enemies = new EnemyFactory(config, poolRoot);
            Spawner = new EnemySpawner(Enemies);
            Cards = new CardSystem(config, poolRoot, player, Enemies);

            _machine = new GameStateMachine();
            _mainMenu = new MainMenuState(this);
            _leaderboard = new LeaderboardState(this);
            _scanning = new ScanningState(this);
            _countdown = new CountdownState(this);
            _playing = new PlayingState(this);
            _paused = new PausedState(this);
            _gameOver = new GameOverState(this);

            GameEvents.EnemyKilled += OnEnemyKilled;
        }

        void Start()
        {
            _machine.ChangeState(_mainMenu);
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
            _machine.Tick();
#if UNITY_EDITOR
            DebugKeys();
#endif
        }

        // ---------- Commands used by the UI ----------

        public void StartGame()
        {
            if (ar.HasArena) GoToCountdown();
            else _machine.ChangeState(_scanning);
        }

        public void OpenLeaderboard() => _machine.ChangeState(_leaderboard);
        public void BackToMenu() => _machine.ChangeState(_mainMenu);
        public void Restart() => StartGame();

        public void Pause()
        {
            if (_machine.Current == _playing && !_playing.IsDying) _machine.ChangeState(_paused);
        }

        public void Resume()
        {
            if (_machine.Current == _paused) _machine.ChangeState(_playing);
        }

        public void SelectDifficulty(int index)
        {
            DifficultyIndex = Mathf.Clamp(index, 0, config.DifficultyCount - 1);
            GameSettings.Difficulty = DifficultyIndex;
            DifficultyChanged?.Invoke(DifficultyIndex);
        }

        // ---------- Called by the states ----------

        public void GoToCountdown() => _machine.ChangeState(_countdown);
        public void GoToPlaying() => _machine.ChangeState(_playing);
        public void RaiseCountdown(int number) => CountdownTicked?.Invoke(number);

        /// <summary>Puts every system back to the start of a round using the selected difficulty.</summary>
        public void PrepareRound()
        {
            WipeBoard();
            var d = ActiveDifficulty;
            _playing.ResetRound();
            player.ResetForRound(d.PlayerMaxHealth);
            Score.Reset();
            Timer.Reset(d.RoundLength);
            Enemies.Configure(player, ar.Arena, d.EnemyDamageMultiplier);
            Spawner.Configure(d, () => Timer.Progress01, player, ar.Arena);
            Cards.Configure(ar.Arena);
        }

        /// <summary>Turns the systems that only run during play on or off.</summary>
        public void SetCombatActive(bool active)
        {
            player.SetCombatActive(active);
            Spawner.SetRunning(active);
            Cards.SetRunning(active);
        }

        /// <summary>Returns every enemy, projectile, card and gadget to its pool.</summary>
        public void WipeBoard()
        {
            Enemies.ReleaseAll();
            player.ReleaseAllBolts();
            Cards.ReleaseAll();
        }

        public void EndRound(bool survived)
        {
            if (survived) Score.AddPoints(SurvivalBonus);

            LastResult = new SessionResult
            {
                score = Score.Score,
                enemiesDefeated = Score.Kills,
                walkers = Score.Walkers,
                spitters = Score.Spitters,
                timeSurvived = Timer.Elapsed,
                difficulty = ActiveDifficulty.DisplayName,
                survived = survived,
                dateIso = DateTime.Now.ToString("o")
            };
            LastWasNewBest = Leaderboard.Add(LastResult);
            if (survived) AudioManager.Instance?.Play(SoundId.RoundWin);

            _machine.ChangeState(_gameOver);
            RoundEnded?.Invoke(LastResult, LastWasNewBest);
        }

        void OnEnemyKilled(EnemyKilledArgs kill)
        {
            if (_machine.Current == _playing) Score.AddKill(kill);
        }

#if UNITY_EDITOR
        // Debug keys for testing the state machine in the editor.
        void DebugKeys()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) BackToMenu();
            if (kb.f2Key.wasPressedThisFrame) StartGame();
            if (kb.f4Key.wasPressedThisFrame && _machine.Current == _playing) EndRound(true);
            if (kb.f5Key.wasPressedThisFrame && _machine.Current == _playing)
                player.TakeDamage(new DamageInfo(9999f, player.Position, Vector3.forward));
            if (kb.pKey.wasPressedThisFrame)
            {
                if (_machine.Current == _paused) Resume();
                else Pause();
            }
        }
#endif
    }

    // =========================================================================
    // STATE PATTERN
    // =========================================================================

    public enum GameStateId { MainMenu, Leaderboard, Scanning, Countdown, Playing, Paused, GameOver }

    /// <summary>One phase of the game. Each state switches on what it needs in Enter and off in Exit.</summary>
    public interface IGameState
    {
        GameStateId Id { get; }
        void Enter();
        void Tick();
        void Exit();
    }

    /// <summary>Runs exactly one state at a time and tells listeners (the UI) when it changes.</summary>
    public class GameStateMachine
    {
        public IGameState Current { get; private set; }

        /// <summary>(previous, current)</summary>
        public event Action<IGameState, IGameState> StateChanged;

        public void ChangeState(IGameState next)
        {
            if (next == null || next == Current) return;
            var previous = Current;
            previous?.Exit();
            Current = next;
            Current.Enter();
            StateChanged?.Invoke(previous, Current);
        }

        public void Tick() => Current?.Tick();
    }

    /// <summary>Shared base: gives every state the GameManager and empty defaults.</summary>
    public abstract class GameStateBase : IGameState
    {
        protected readonly GameManager Game;
        protected GameStateBase(GameManager game) => Game = game;
        public abstract GameStateId Id { get; }
        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    /// <summary>Title screen. The arena (if placed) stays in the room, but nothing runs.</summary>
    public class MainMenuState : GameStateBase
    {
        public MainMenuState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.MainMenu;

        public override void Enter()
        {
            Time.timeScale = 1f;
            Game.AR.SetPlacementEnabled(false);
            Game.SetCombatActive(false);
            Game.WipeBoard();
        }
    }

    public class LeaderboardState : GameStateBase
    {
        public LeaderboardState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Leaderboard;
    }

    /// <summary>Scan the floor and tap to place the beacon. The only state where placement is on.</summary>
    public class ScanningState : GameStateBase
    {
        public ScanningState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Scanning;

        public override void Enter()
        {
            Game.AR.ArenaPlaced += OnArenaPlaced;
            Game.AR.SetPlacementEnabled(true);
        }

        public override void Exit()
        {
            Game.AR.ArenaPlaced -= OnArenaPlaced;
            Game.AR.SetPlacementEnabled(false);
        }

        void OnArenaPlaced(Arena arena) => Game.GoToCountdown();
    }

    /// <summary>Resets the round, then counts 3-2-1-GO.</summary>
    public class CountdownState : GameStateBase
    {
        float _remaining;
        int _shown;

        public CountdownState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Countdown;

        public override void Enter()
        {
            Time.timeScale = 1f;
            Game.PrepareRound();
            _remaining = 3f;
            _shown = -1;
        }

        public override void Tick()
        {
            _remaining -= Time.deltaTime;
            int number = Mathf.Max(0, Mathf.CeilToInt(_remaining));
            if (number != _shown)
            {
                _shown = number;
                Game.RaiseCountdown(number);
                AudioManager.Instance?.Play(number > 0 ? SoundId.CountdownBeep : SoundId.CountdownGo);
            }
            if (_remaining <= -0.4f) Game.GoToPlaying(); // hold "GO!" briefly
        }
    }

    /// <summary>
    /// The round. Ends on timer = 0 (win) or health = 0 (lose). On death there is a short slow-motion beat
    /// (UI uses unscaled time) before the End screen.
    /// </summary>
    public class PlayingState : GameStateBase
    {
        const float DeathSlowMo = 0.35f;
        const float DeathDelay = 0.9f;

        bool _dying;
        float _deathTimer;
        int _secondsScored;

        public PlayingState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Playing;
        public bool IsDying => _dying;

        public void ResetRound()
        {
            _dying = false;
            _secondsScored = 0;
        }

        public override void Enter()
        {
            Time.timeScale = _dying ? DeathSlowMo : 1f;
            Game.Player.Died += OnPlayerDied;
            if (!_dying) Game.SetCombatActive(true);
        }

        public override void Exit()
        {
            Game.Player.Died -= OnPlayerDied;
            Game.SetCombatActive(false);
        }

        public override void Tick()
        {
            if (_dying)
            {
                _deathTimer -= Time.unscaledDeltaTime;
                if (_deathTimer <= 0f) Game.EndRound(false);
                return;
            }

            float dt = Time.deltaTime;
            Game.Timer.Tick(dt);
            Game.Spawner.Tick(dt);
            Game.Cards.Tick(dt);

            int seconds = Mathf.FloorToInt(Game.Timer.Elapsed);
            if (seconds > _secondsScored)
            {
                Game.Score.AddPoints((seconds - _secondsScored) * GameManager.PointsPerSecond);
                _secondsScored = seconds;
            }

            if (Game.Timer.IsFinished) Game.EndRound(true);
        }

        void OnPlayerDied()
        {
            if (_dying) return;
            _dying = true;
            _deathTimer = DeathDelay;
            Game.SetCombatActive(false);
            Time.timeScale = DeathSlowMo;
        }
    }

    /// <summary>Time.timeScale = 0 freezes gameplay; the AR camera keeps tracking because it doesn't use game time.</summary>
    public class PausedState : GameStateBase
    {
        public PausedState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Paused;
        public override void Enter() => Time.timeScale = 0f;
        public override void Exit() => Time.timeScale = 1f;
    }

    /// <summary>Round over: everything goes back to its pool. The result was already saved in EndRound.</summary>
    public class GameOverState : GameStateBase
    {
        public GameOverState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.GameOver;

        public override void Enter()
        {
            Time.timeScale = 1f;
            Game.SetCombatActive(false);
            Game.WipeBoard();
        }
    }

    // =========================================================================
    // ROUND: timer, score, game-wide events
    // =========================================================================

    /// <summary>Counts the round down. Raises TimeChanged once per displayed second.</summary>
    public class RoundTimer
    {
        int _lastWhole = -1;

        public float Duration { get; private set; }
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        public float Progress01 => Duration > 0f ? Mathf.Clamp01(Elapsed / Duration) : 0f;
        public bool IsFinished => Elapsed >= Duration;

        public event Action<float> TimeChanged;

        public void Reset(float duration)
        {
            Duration = duration;
            Elapsed = 0f;
            _lastWhole = -1;
            Notify();
        }

        public void Tick(float dt)
        {
            if (IsFinished) return;
            Elapsed = Mathf.Min(Duration, Elapsed + dt);
            Notify();
        }

        void Notify()
        {
            int whole = Mathf.CeilToInt(Remaining);
            if (whole == _lastWhole) return;
            _lastWhole = whole;
            TimeChanged?.Invoke(whole);
        }
    }

    /// <summary>Score and kill counts. Kill score = enemy base score x ricochet multiplier of the killing bolt.</summary>
    public class ScoreSystem
    {
        public int Score { get; private set; }
        public int Walkers { get; private set; }
        public int Spitters { get; private set; }
        public int Kills => Walkers + Spitters;

        public event Action<int> ScoreChanged;
        /// <summary>(points, multiplier, world position) for the floating "+150 x2 RICOCHET!" text.</summary>
        public event Action<int, float, Vector3> PointsAwarded;

        public void Reset()
        {
            Score = Walkers = Spitters = 0;
            ScoreChanged?.Invoke(Score);
        }

        public void AddKill(EnemyKilledArgs kill)
        {
            if (kill.Type == EnemyType.Walker) Walkers++;
            else Spitters++;
            int points = Mathf.RoundToInt(kill.BaseScore * kill.Multiplier);
            AddPoints(points);
            PointsAwarded?.Invoke(points, kill.Multiplier, kill.Position);
        }

        public void AddPoints(int points)
        {
            if (points == 0) return;
            Score += points;
            ScoreChanged?.Invoke(Score);
        }
    }

    public readonly struct EnemyKilledArgs
    {
        public readonly EnemyType Type;
        public readonly int BaseScore;
        public readonly float Multiplier;
        public readonly Vector3 Position;

        public EnemyKilledArgs(EnemyType type, int baseScore, float multiplier, Vector3 position)
        {
            Type = type;
            BaseScore = baseScore;
            Multiplier = multiplier;
            Position = position;
        }
    }

    /// <summary>Game-wide events (Observer). Enemies announce "I died"; score, UI and audio react independently.</summary>
    public static class GameEvents
    {
        public static event Action<EnemyKilledArgs> EnemyKilled;
        public static void RaiseEnemyKilled(EnemyKilledArgs args) => EnemyKilled?.Invoke(args);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => EnemyKilled = null;
    }

    // =========================================================================
    // DAMAGE CONTRACT
    // =========================================================================

    /// <summary>Anything that can be hurt (the player and every enemy). Callers don't need to know which.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }

    /// <summary>One hit. ScoreMultiplier carries the laser's ricochet bonus to the enemy.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public float ScoreMultiplier;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, float scoreMultiplier = 1f)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            ScoreMultiplier = scoreMultiplier;
        }
    }

    // =========================================================================
    // SAVE DATA: leaderboard + settings
    // =========================================================================

    [Serializable]
    public class SessionResult
    {
        public int score;
        public int enemiesDefeated;
        public int walkers;
        public int spitters;
        public float timeSurvived;
        public string difficulty;
        public bool survived;
        public string dateIso;
    }

    /// <summary>JsonUtility can't serialize a bare list, so it is wrapped.</summary>
    [Serializable]
    public class SessionResultList
    {
        public List<SessionResult> sessions = new List<SessionResult>();
    }

    /// <summary>
    /// Saves the LATEST 5 sessions (newest first) as JSON in persistentDataPath so they survive closing the app.
    /// A missing or corrupt file never crashes the game. The best score is kept separately in PlayerPrefs.
    /// </summary>
    public class LeaderboardService
    {
        public const int MaxEntries = 5;
        const string BestKey = "Ricochet.BestScore";

        readonly string _path = Path.Combine(Application.persistentDataPath, "leaderboard.json");
        SessionResultList _data = new SessionResultList();

        public event Action Changed;
        public IReadOnlyList<SessionResult> Sessions => _data.sessions;
        public int BestScore => PlayerPrefs.GetInt(BestKey, 0);

        public LeaderboardService() => Load();

        void Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var loaded = JsonUtility.FromJson<SessionResultList>(File.ReadAllText(_path));
                    if (loaded != null && loaded.sessions != null) _data = loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] Could not read {_path}, starting empty. {e.Message}");
                _data = new SessionResultList();
            }
            Trim();
        }

        /// <summary>Inserts at the front, keeps the latest 5. Returns true for a new best score.</summary>
        public bool Add(SessionResult result)
        {
            _data.sessions.Insert(0, result);
            Trim();
            bool newBest = result.score > BestScore;
            if (newBest)
            {
                PlayerPrefs.SetInt(BestKey, result.score);
                PlayerPrefs.Save();
            }
            Save();
            Changed?.Invoke();
            return newBest;
        }

        public void Clear()
        {
            _data.sessions.Clear();
            Save();
            Changed?.Invoke();
        }

        void Trim()
        {
            if (_data.sessions.Count > MaxEntries)
                _data.sessions.RemoveRange(MaxEntries, _data.sessions.Count - MaxEntries);
        }

        void Save()
        {
            try { File.WriteAllText(_path, JsonUtility.ToJson(_data, true)); }
            catch (Exception e) { Debug.LogWarning($"[Leaderboard] Could not save: {e.Message}"); }
        }
    }

    /// <summary>Small preferences stored in PlayerPrefs.</summary>
    public static class GameSettings
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

    // =========================================================================
    // HELPERS
    // =========================================================================

    /// <summary>
    /// Particle effects without spawning objects: two world-space particle systems emit bursts anywhere on request.
    /// ParticleSystem.Emit recycles particles internally, so this is effectively pooled too.
    /// </summary>
    public static class Vfx
    {
        static ParticleSystem s_Sparks;
        static ParticleSystem s_Puffs;

        public static void Init(ParticleSystem sparks, ParticleSystem puffs)
        {
            s_Sparks = sparks;
            s_Puffs = puffs;
        }

        public static void Sparks(Vector3 position, Vector3 normal, Color color, int count = 10, float speed = 1.5f)
        {
            if (!s_Sparks) return;
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                p.position = position;
                p.velocity = (normal + UnityEngine.Random.insideUnitSphere * 0.9f).normalized * speed * UnityEngine.Random.Range(0.4f, 1f);
                p.startSize = UnityEngine.Random.Range(0.012f, 0.025f);
                p.startLifetime = UnityEngine.Random.Range(0.15f, 0.35f);
                s_Sparks.Emit(p, 1);
            }
        }

        public static void Burst(Vector3 position, Color color, int count = 16, float speed = 0.8f, float size = 0.06f)
        {
            if (!s_Puffs) return;
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                p.position = position + UnityEngine.Random.insideUnitSphere * 0.05f;
                p.velocity = UnityEngine.Random.insideUnitSphere * speed + Vector3.up * speed * 0.5f;
                p.startSize = size * UnityEngine.Random.Range(0.6f, 1.3f);
                p.startLifetime = UnityEngine.Random.Range(0.3f, 0.6f);
                s_Puffs.Emit(p, 1);
            }
        }

        /// <summary>A flat ring on the floor (enemy spawn, freeze pulse, card drop).</summary>
        public static void Ring(Vector3 center, float radius, Color color, int count = 24)
        {
            if (!s_Puffs) return;
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.position = center + dir * radius + Vector3.up * 0.02f;
                p.velocity = dir * 0.3f + Vector3.up * 0.4f;
                p.startSize = 0.05f;
                p.startLifetime = 0.5f;
                s_Puffs.Emit(p, 1);
            }
        }
    }

    /// <summary>"Is this screen position on a UI element?" so a button press never also places or fires.</summary>
    public static class UIHitTest
    {
        static readonly List<RaycastResult> s_Results = new List<RaycastResult>();
        static PointerEventData s_Pointer;
        static EventSystem s_Owner;

        public static bool IsOverUI(Vector2 screenPosition)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (s_Pointer == null || s_Owner != es)
            {
                s_Pointer = new PointerEventData(es);
                s_Owner = es;
            }
            s_Pointer.position = screenPosition;
            s_Results.Clear();
            es.RaycastAll(s_Pointer, s_Results);
            return s_Results.Count > 0;
        }
    }
}
