#nullable enable

using System;
using System.Collections.Generic;
using Uno.Core.Models;

namespace Uno.Core.Players
{
    /// <summary>
    /// Tracks a single player's hand contents and UNO call registration state.
    /// Pure domain state with no Unity dependencies.
    /// </summary>
    public sealed class PlayerHandState
    {
        private readonly List<Card> _hand = new List<Card>(20);

        /// <summary>
        /// Gets the display name for this seat.
        /// </summary>
        public string PlayerName { get; }

        /// <summary>
        /// Gets the seat index (0 = human, 1-3 = bots).
        /// </summary>
        public int SeatIndex { get; }

        /// <summary>
        /// Gets whether this seat belongs to a human player.
        /// </summary>
        public bool IsHuman { get; }

        /// <summary>
        /// Gets whether the player has registered a valid "UNO!" call for the current single-card state.
        /// </summary>
        public bool HasCalledUno { get; private set; }

        /// <summary>
        /// Gets a read-only view of the hand.
        /// </summary>
        public IReadOnlyList<Card> Hand => _hand;

        /// <summary>
        /// Gets the mutable hand list used by application orchestration.
        /// </summary>
        public List<Card> MutableHand => _hand;

        /// <summary>
        /// Gets the number of cards currently held.
        /// </summary>
        public int CardCount => _hand.Count;

        /// <summary>
        /// Initializes a new player hand state.
        /// </summary>
        public PlayerHandState(int seatIndex, string playerName, bool isHuman)
        {
            if (seatIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seatIndex));
            }

            if (string.IsNullOrWhiteSpace(playerName))
            {
                throw new ArgumentException("Player name cannot be empty.", nameof(playerName));
            }

            SeatIndex = seatIndex;
            PlayerName = playerName;
            IsHuman = isHuman;
        }

        /// <summary>
        /// Clears the hand and resets UNO call state.
        /// </summary>
        public void Reset()
        {
            _hand.Clear();
            HasCalledUno = false;
        }

        /// <summary>
        /// Adds a card to the hand and clears UNO call if the hand exceeds one card.
        /// </summary>
        public void AddCard(Card card)
        {
            _hand.Add(card);
            if (_hand.Count > 1)
            {
                HasCalledUno = false;
            }
        }

        /// <summary>
        /// Removes a card from the hand.
        /// </summary>
        /// <returns>True if the card was present and removed.</returns>
        public bool RemoveCard(Card card)
        {
            bool removed = _hand.Remove(card);
            if (removed && _hand.Count != 1)
            {
                HasCalledUno = false;
            }

            return removed;
        }

        /// <summary>
        /// Registers an official UNO call. Valid only when exactly one card remains.
        /// </summary>
        /// <returns>True if the call was accepted.</returns>
        public bool TryCallUno()
        {
            if (_hand.Count != 1)
            {
                return false;
            }

            HasCalledUno = true;
            return true;
        }

        /// <summary>
        /// Returns true when the player has exactly one card and has not called UNO.
        /// </summary>
        public bool IsUnoPenaltyEligible()
        {
            return _hand.Count == 1 && !HasCalledUno;
        }
    }
}
