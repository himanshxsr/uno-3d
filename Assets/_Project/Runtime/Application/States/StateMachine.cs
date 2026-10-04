#nullable enable

using System;

namespace Uno.Application.States
{
    /// <summary>
    /// Robust Finite State Machine controller managing state transitions and execution lifecycles.
    /// </summary>
    public class StateMachine
    {
        /// <summary>
        /// Gets the currently active gameplay state.
        /// </summary>
        public IGameState? CurrentState { get; private set; }

        /// <summary>
        /// Event broadcast when state transition completes.
        /// </summary>
        public event Action<IGameState>? OnStateChanged;

        /// <summary>
        /// Transitions the state machine from CurrentState to newState.
        /// </summary>
        /// <param name="newState">Target state to transition to.</param>
        public void ChangeState(IGameState newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            if (CurrentState == newState)
            {
                return;
            }

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();

            OnStateChanged?.Invoke(CurrentState);
        }

        /// <summary>
        /// Ticks the currently active state.
        /// </summary>
        /// <param name="deltaTime">Delta time in seconds.</param>
        public void Update(float deltaTime)
        {
            CurrentState?.Update(deltaTime);
        }
    }
}
