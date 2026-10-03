namespace Ricochet.Core
{
    public enum GameStateId
    {
        MainMenu,
        Leaderboard,
        Scanning,
        Countdown,
        Playing,
        Paused,
        GameOver
    }

    /// <summary>One phase of the game. Each state switches the systems it needs on in Enter and off in Exit.</summary>
    public interface IGameState
    {
        GameStateId Id { get; }
        void Enter();
        void Tick();
        void Exit();
    }

    /// <summary>Shared base so every state has access to the GameManager and empty defaults.</summary>
    public abstract class GameStateBase : IGameState
    {
        protected readonly GameManager Game;

        protected GameStateBase(GameManager game) => Game = game;

        public abstract GameStateId Id { get; }
        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }
}
