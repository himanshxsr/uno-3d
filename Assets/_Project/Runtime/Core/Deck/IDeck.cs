#nullable enable

using System.Collections.Generic;
using Uno.Core.Models;

namespace Uno.Core.Deck
{
    /// <summary>
    /// Contract defining deck management operations, card drawing, discard pile tracking, and recycling mechanics.
    /// </summary>
    public interface IDeck
    {
        /// <summary>
        /// Gets the total number of cards remaining in the Draw pile stack.
        /// </summary>
        int DrawPileCount { get; }

        /// <summary>
        /// Gets the total number of cards currently in the Discard pile stack.
        /// </summary>
        int DiscardPileCount { get; }

        /// <summary>
        /// Gets the top face-up card on the Discard pile, or null if the discard pile is empty.
        /// </summary>
        Card? TopDiscardCard { get; }

        /// <summary>
        /// Initializes or resets the deck to the full 108 standard UNO cards.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shuffles the current Draw pile using Fisher-Yates algorithm and an optional seed for PRNG determinism.
        /// </summary>
        /// <param name="seed">Optional random seed for deterministic replayability.</param>
        void Shuffle(int? seed = null);

        /// <summary>
        /// Draws a single card from the top of the Draw pile.
        /// Automatically recycles the Discard pile into the Draw pile if the Draw pile is empty.
        /// </summary>
        /// <returns>The drawn <see cref="Card"/>.</returns>
        Card Draw();

        /// <summary>
        /// Draws multiple cards from the Draw pile.
        /// </summary>
        /// <param name="count">Number of cards to draw.</param>
        /// <returns>List of drawn cards.</returns>
        IReadOnlyList<Card> DrawMultiple(int count);

        /// <summary>
        /// Places a card face-up onto the top of the Discard pile.
        /// </summary>
        /// <param name="card">The card to discard.</param>
        void Discard(Card card);

        /// <summary>
        /// Recycles all cards from the Discard pile (except the top card) back into the Draw pile and shuffles them.
        /// </summary>
        void RecycleDiscardIntoDrawPile();
    }
}
