#nullable enable

using System;

namespace Uno.Application.Turns
{
    /// <summary>
    /// Production implementation of player turn sequence orchestration, wrapping modular turn math:
    /// (currentIndex + direction * step) % playerCount
    /// </summary>
    public class TurnManager : ITurnManager
    {
        /// <inheritdoc />
        public int CurrentPlayerIndex { get; private set; }

        /// <inheritdoc />
        public int PlayerCount { get; private set; } = 4;

        /// <inheritdoc />
        public TurnDirection Direction { get; private set; } = TurnDirection.Clockwise;

        /// <inheritdoc />
        public void Initialize(int playerCount = 4, int startingPlayerIndex = 0)
        {
            if (playerCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be at least 2.");
            }

            PlayerCount = playerCount;
            CurrentPlayerIndex = (startingPlayerIndex % playerCount + playerCount) % playerCount;
            Direction = TurnDirection.Clockwise;
        }

        /// <inheritdoc />
        public int GetNextPlayerIndex(int stepCount = 1)
        {
            if (PlayerCount <= 0)
            {
                return 0;
            }

            int stepOffset = (int)Direction * stepCount;
            int nextIndex = (CurrentPlayerIndex + stepOffset) % PlayerCount;
            if (nextIndex < 0)
            {
                nextIndex += PlayerCount;
            }

            return nextIndex;
        }

        /// <inheritdoc />
        public void AdvanceTurn(int stepCount = 1)
        {
            CurrentPlayerIndex = GetNextPlayerIndex(stepCount);
        }

        /// <inheritdoc />
        public void ReverseDirection()
        {
            // In 2-player game, Reverse acts identically to a Skip
            if (PlayerCount == 2)
            {
                AdvanceTurn(1);
                return;
            }

            Direction = (Direction == TurnDirection.Clockwise)
                ? TurnDirection.CounterClockwise
                : TurnDirection.Clockwise;
        }

        /// <inheritdoc />
        public void SkipNextPlayer(int stepCount = 1)
        {
            AdvanceTurn(stepCount + 1);
        }
    }
}
