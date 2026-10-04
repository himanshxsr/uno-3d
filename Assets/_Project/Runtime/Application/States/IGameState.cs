#nullable enable

namespace Uno.Application.States
{
    /// <summary>
    /// Contract defining a discrete gameplay state in the Finite State Machine.
    /// </summary>
    public interface IGameState
    {
        /// <summary>
        /// Called when the state machine enters this state.
        /// </summary>
        void Enter();

        /// <summary>
        /// Called each tick update while this state remains active.
        /// </summary>
        /// <param name="deltaTime">Elapsed delta time since last frame.</param>
        void Update(float deltaTime);

        /// <summary>
        /// Called when the state machine exits this state.
        /// </summary>
        void Exit();
    }
}
