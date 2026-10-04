#nullable enable

using Uno.Application.Turns;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Application.Events
{
    /// <summary>
    /// Event broadcast when a player legally plays a card onto the discard pile.
    /// </summary>
    public readonly struct CardPlayedEvent
    {
        public int PlayerId { get; }
        public Card Card { get; }

        public CardPlayedEvent(int playerId, Card card)
        {
            PlayerId = playerId;
            Card = card;
        }
    }

    /// <summary>
    /// Event broadcast when a player draws a single card from the draw pile.
    /// Published once per physical card so presentation can spawn every view.
    /// </summary>
    public readonly struct CardDrawnEvent
    {
        public int PlayerId { get; }
        public Card Card { get; }

        public CardDrawnEvent(int playerId, Card card)
        {
            PlayerId = playerId;
            Card = card;
        }
    }

    /// <summary>
    /// Event broadcast when turn advances or turn direction reverses.
    /// </summary>
    public readonly struct TurnChangedEvent
    {
        public int CurrentPlayerId { get; }
        public TurnDirection Direction { get; }

        public TurnChangedEvent(int currentPlayerId, TurnDirection direction)
        {
            CurrentPlayerId = currentPlayerId;
            Direction = direction;
        }
    }

    /// <summary>
    /// Event broadcast when the active suit color changes.
    /// </summary>
    public readonly struct ColorChangedEvent
    {
        public CardColor NewActiveColor { get; }

        public ColorChangedEvent(CardColor newActiveColor)
        {
            NewActiveColor = newActiveColor;
        }
    }

    /// <summary>
    /// Event broadcast when a player declares "UNO!" with exactly one card remaining.
    /// </summary>
    public readonly struct UnoDeclaredEvent
    {
        public int PlayerId { get; }

        public UnoDeclaredEvent(int playerId)
        {
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// Event broadcast when a player is penalized for failing to call UNO.
    /// </summary>
    public readonly struct UnoPenaltyEvent
    {
        public int PlayerId { get; }
        public int CardsDrawn { get; }

        public UnoPenaltyEvent(int playerId, int cardsDrawn)
        {
            PlayerId = playerId;
            CardsDrawn = cardsDrawn;
        }
    }

    /// <summary>
    /// Event broadcast when a wild card requires color selection from a human player.
    /// </summary>
    public readonly struct ColorSelectionRequestedEvent
    {
        public int PlayerId { get; }

        public ColorSelectionRequestedEvent(int playerId)
        {
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// Event broadcast when a player wins the round and total points are calculated.
    /// </summary>
    public readonly struct RoundEndedEvent
    {
        public int WinnerPlayerId { get; }
        public int FinalScore { get; }

        public RoundEndedEvent(int winnerPlayerId, int finalScore)
        {
            WinnerPlayerId = winnerPlayerId;
            FinalScore = finalScore;
        }
    }

    /// <summary>
    /// Event broadcast for non-critical gameplay log messages (HUD feed / debug).
    /// </summary>
    public readonly struct GameLogEvent
    {
        public string Message { get; }

        public GameLogEvent(string message)
        {
            Message = message;
        }
    }
}
