using System;

namespace Ricochet.Core
{
    /// <summary>
    /// Runs exactly one IGameState at a time (State pattern).
    /// Switching calls Exit on the old state and Enter on the new one, then tells listeners (e.g. UIManager).
    /// </summary>
    public class GameStateMachine
    {
        public IGameState Current { get; private set; }

        /// <summary>(previous, current). Previous is null for the first state.</summary>
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
}
