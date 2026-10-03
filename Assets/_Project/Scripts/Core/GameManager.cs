using System;
using Ricochet.AR;
using Ricochet.Audio;
using Ricochet.Cards;
using Ricochet.Data;
using Ricochet.Enemies;
using Ricochet.Player;
using Ricochet.Weapons;
using UnityEngine;

namespace Ricochet.Core
{
    /// <summary>
    /// Central coordinator (Singleton). Owns the state machine, the round's score/timer and the leaderboard,
    /// and exposes simple commands (StartGame, Pause, Restart...) that the UI calls.
    /// It never references UI classes: the UI observes its events instead.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public const int PointsPerSecond = 5;
        public const int SurvivalBonus = 500;

        public static GameManager Instance { get; private set; }

        [Header("Scene systems")]
        [SerializeField] ARPlacementController placement;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] LaserBlaster blaster;
        [SerializeField] EnemyFactory enemyFactory;
        [SerializeField] EnemySpawner enemySpawner;
        [SerializeField] CardSpawner cardSpawner;
        [SerializeField] AbilityPlacer abilityPlacer;

        [Header("Config")]
        [SerializeField] DifficultyStore difficulties = new DifficultyStore();

        GameStateMachine _machine;
        MainMenuState _mainMenu;
        LeaderboardState _leaderboard;
        ScanningState _scanning;
        CountdownState _countdown;
        PlayingState _playing;
        PausedState _paused;
        GameOverState _gameOver;

        public GameStateMachine StateMachine => _machine;
        public GameStateId CurrentState => _machine.Current.Id;
        public ARPlacementController Placement => placement;
        public PlayerController Player => player;
        public PlayerHealth PlayerHealth => playerHealth;
        public LaserBlaster Blaster => blaster;
        public AbilityPlacer AbilityPlacer => abilityPlacer;
        public CardSpawner Cards => cardSpawner;
        public EnemyFactory Enemies => enemyFactory;
        public DifficultyStore Difficulties => difficulties;
        public DifficultySettings ActiveDifficulty { get; private set; }

        public ScoreSystem Score { get; } = new ScoreSystem();
        public RoundTimer Timer { get; } = new RoundTimer();
        public LeaderboardService Leaderboard { get; private set; }

        public SessionResult LastResult { get; private set; }
        public bool LastWasNewBest { get; private set; }

        /// <summary>3, 2, 1, then 0 for "GO!".</summary>
        public event Action<int> CountdownTicked;
        /// <summary>Raised once a round is over and saved: (result, isNewBest).</summary>
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
            // Lasers must be able to hit AR wall meshes from either side.
            Physics.queriesHitBackfaces = true;

            difficulties.Load();
            ActiveDifficulty = difficulties.Selected;
            Leaderboard = new LeaderboardService();

            _machine = new GameStateMachine();
            _mainMenu = new MainMenuState(this);
            _leaderboard = new LeaderboardState(this);
            _scanning = new ScanningState(this);
            _countdown = new CountdownState(this);
            _playing = new PlayingState(this);
            _paused = new PausedState(this);
            _gameOver = new GameOverState(this);

            GameEvents.EnemyKilled += OnEnemyKilled;
            placement.ArenaPlaced += OnArenaPlaced;
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
            if (placement) placement.ArenaPlaced -= OnArenaPlaced;
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
            if (placement.HasArena) GoToCountdown();
            else _machine.ChangeState(_scanning);
        }

        public void OpenLeaderboard() => _machine.ChangeState(_leaderboard);
        public void BackToMenu() => _machine.ChangeState(_mainMenu);
        public void Restart() => StartGame();

        public void Pause()
        {
            if (_machine.Current == _playing && !_playing.IsDying)
                _machine.ChangeState(_paused);
        }

        public void Resume()
        {
            if (_machine.Current == _paused)
                _machine.ChangeState(_playing);
        }

        public void SelectDifficulty(int index)
        {
            difficulties.Select(index);
            ActiveDifficulty = difficulties.Selected;
        }

        // ---------- Called by states ----------

        public void GoToCountdown() => _machine.ChangeState(_countdown);
        public void GoToPlaying() => _machine.ChangeState(_playing);
        public void RaiseCountdown(int number) => CountdownTicked?.Invoke(number);

        /// <summary>Puts every system back to the start of a round using the selected difficulty.</summary>
        public void PrepareRound()
        {
            ActiveDifficulty = difficulties.Selected;
            WipeBoard();

            _playing.ResetRound();
            playerHealth.ResetHealth(ActiveDifficulty.PlayerMaxHealth);
            Score.Reset();
            Timer.Reset(ActiveDifficulty.RoundLength);
            blaster.ResetWeapon();
            abilityPlacer.ResetCharges();

            var arena = placement.Arena;
            enemyFactory.Configure(player, arena, ActiveDifficulty.EnemyDamageMultiplier);
            enemySpawner.Configure(ActiveDifficulty, () => Timer.Progress01, player, arena);
            cardSpawner.Configure(arena, player, BuildPlayerContext());
            abilityPlacer.SetArena(arena);
        }

        /// <summary>Turns the systems that only run during play on or off.</summary>
        public void SetCombatActive(bool active)
        {
            playerHealth.SetInvulnerable(!active);
            blaster.SetArmed(active);
            enemySpawner.SetRunning(active);
            cardSpawner.SetRunning(active);
        }

        /// <summary>Returns every enemy, projectile, card and gadget to its pool.</summary>
        public void WipeBoard()
        {
            enemyFactory.ReleaseAll();
            blaster.ReleaseAllBolts();
            cardSpawner.ReleaseAll();
            abilityPlacer.ClearPlaced();
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

        PlayerContext BuildPlayerContext() =>
            new PlayerContext(player, playerHealth, blaster, abilityPlacer, enemyFactory);

        void OnEnemyKilled(EnemyKilledArgs kill)
        {
            if (_machine.Current == _playing) Score.AddKill(kill);
        }

        void OnArenaPlaced(Transform arena, UnityEngine.XR.ARFoundation.ARPlane plane)
        {
            abilityPlacer.SetArena(placement.Arena);
        }

#if UNITY_EDITOR
        // Debug keys for testing the state machine in the editor (Milestone 3 requirement).
        void DebugKeys()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) BackToMenu();
            if (kb.f2Key.wasPressedThisFrame) StartGame();
            if (kb.f3Key.wasPressedThisFrame && placement.HasArena) GoToCountdown();
            if (kb.f4Key.wasPressedThisFrame && _machine.Current == _playing) EndRound(true);
            if (kb.f5Key.wasPressedThisFrame && _machine.Current == _playing)
                playerHealth.TakeDamage(new DamageInfo(9999f, player.transform.position, Vector3.forward));
            if (kb.pKey.wasPressedThisFrame)
            {
                if (_machine.Current == _paused) Resume();
                else Pause();
            }
        }
#endif
    }
}
