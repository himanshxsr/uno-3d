#nullable enable

namespace Uno.Application.Turns
{
    /// <summary>
    /// Contract defining player turn management, direction inversion, and skip mechanics.
    /// </summary>
    public interface ITurnManager
    {
        /// <summary>
        /// Gets the current active player seat index (0 to PlayerCount - 1).
        /// </summary>
        int CurrentPlayerIndex { get; }

        /// <summary>
        /// Gets the total number of players in the game (default 4).
        /// </summary>
        int PlayerCount { get; }

        /// <summary>
        /// Gets the current turn advancement direction.
        /// </summary>
        TurnDirection Direction { get; }

        /// <summary>
        /// Initializes turn tracking for a given number of seated players.
        /// </summary>
        /// <param name="playerCount">Number of seated players (minimum 2).</param>
        /// <param name="startingPlayerIndex">Initial seat index to begin play.</param>
        void Initialize(int playerCount = 4, int startingPlayerIndex = 0);

        /// <summary>
        /// Calculates the next player seat index based on direction and step offset without modifying state.
        /// </summary>
        /// <param name="stepCount">Number of steps forward in turn order.</param>
        /// <returns>Target player seat index.</returns>
        int GetNextPlayerIndex(int stepCount = 1);

        /// <summary>
        /// Advances the active turn to the next player seat.
        /// </summary>
        /// <param name="stepCount">Step count (default 1, >1 for skips).</param>
        void AdvanceTurn(int stepCount = 1);

        /// <summary>
        /// Inverts turn advancement direction (Clockwise <-> CounterClockwise).
        /// </summary>
        void ReverseDirection();

        /// <summary>
        /// Forces turn advancement past the next player seat (Skip effect).
        /// </summary>
        /// <param name="stepCount">Number of players to skip.</param>
        void SkipNextPlayer(int stepCount = 1);
    }
}
