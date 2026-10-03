using Ricochet.Audio;
using UnityEngine;

namespace Ricochet.Core
{
    /// <summary>Title screen. The arena (if placed) stays in the world, but nothing is running.</summary>
    public class MainMenuState : GameStateBase
    {
        public MainMenuState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.MainMenu;

        public override void Enter()
        {
            Time.timeScale = 1f;
            Game.Placement.SetPlacementEnabled(false);
            Game.SetCombatActive(false);
            Game.WipeBoard();
        }
    }

    /// <summary>Shows the latest 5 runs. Nothing else runs.</summary>
    public class LeaderboardState : GameStateBase
    {
        public LeaderboardState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Leaderboard;
    }

    /// <summary>The player scans the floor and taps to place the beacon. Only state where placement is on.</summary>
    public class ScanningState : GameStateBase
    {
        public ScanningState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Scanning;

        public override void Enter()
        {
            Game.Placement.ArenaPlaced += OnArenaPlaced;
            Game.Placement.SetPlacementEnabled(true);
        }

        public override void Exit()
        {
            Game.Placement.ArenaPlaced -= OnArenaPlaced;
            Game.Placement.SetPlacementEnabled(false);
        }

        void OnArenaPlaced(Transform arena, UnityEngine.XR.ARFoundation.ARPlane plane) => Game.GoToCountdown();
    }

    /// <summary>Resets the round, then counts 3-2-1-GO before play starts.</summary>
    public class CountdownState : GameStateBase
    {
        const float Duration = 3f;
        float _remaining;
        int _shown;

        public CountdownState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Countdown;

        public override void Enter()
        {
            Time.timeScale = 1f;
            Game.PrepareRound();
            _remaining = Duration;
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

            // Hold "GO!" on screen briefly before switching.
            if (_remaining <= -0.4f)
                Game.GoToPlaying();
        }
    }

    /// <summary>
    /// The actual round: timer, spawner, weapon and cards run. Ends on timer = 0 (win) or health = 0 (lose).
    /// On death there is a short slow-motion beat (UI uses unscaled time) before the End screen.
    /// </summary>
    public class PlayingState : GameStateBase
    {
        const float DeathSlowMo = 0.35f;
        const float DeathDelay = 0.9f;

        bool _dying;
        float _deathTimer;
        int _survivalSecondsScored;

        public PlayingState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Playing;
        public bool IsDying => _dying;

        /// <summary>Called by PrepareRound so a fresh round starts clean (not on resume from pause).</summary>
        public void ResetRound()
        {
            _dying = false;
            _survivalSecondsScored = 0;
        }

        public override void Enter()
        {
            Time.timeScale = _dying ? DeathSlowMo : 1f;
            Game.PlayerHealth.Died += OnPlayerDied;
            if (!_dying) Game.SetCombatActive(true);
        }

        public override void Exit()
        {
            Game.PlayerHealth.Died -= OnPlayerDied;
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

            Game.Timer.Tick(Time.deltaTime);

            // +5 points per whole second survived.
            int seconds = Mathf.FloorToInt(Game.Timer.Elapsed);
            if (seconds > _survivalSecondsScored)
            {
                Game.Score.AddPoints((seconds - _survivalSecondsScored) * GameManager.PointsPerSecond);
                _survivalSecondsScored = seconds;
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

    /// <summary>Freezes gameplay with Time.timeScale = 0. The AR camera keeps tracking because it doesn't use game time.</summary>
    public class PausedState : GameStateBase
    {
        public PausedState(GameManager game) : base(game) { }
        public override GameStateId Id => GameStateId.Paused;

        public override void Enter() => Time.timeScale = 0f;
        public override void Exit() => Time.timeScale = 1f;
    }

    /// <summary>Round over: every enemy, projectile and card is returned to its pool. The result was saved in EndRound.</summary>
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
}
