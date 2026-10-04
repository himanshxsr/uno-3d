#nullable enable

using System.Collections.Generic;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Application.AI
{
    /// <summary>
    /// Contract for AI Bot decision evaluation, selecting legal moves based on heuristic scoring matrices.
    /// </summary>
    public interface IAIDecisionStrategy
    {
        /// <summary>
        /// Evaluates the AI bot's hand and selects the optimal legal card to play, or returns null if no move is legal.
        /// </summary>
        /// <param name="hand">Cards in bot's hand.</param>
        /// <param name="topCard">Current top discard card.</param>
        /// <param name="activeColor">Active suit color requirement.</param>
        /// <param name="nextPlayerCardCount">Remaining card count of the immediate next opponent (threat factor).</param>
        /// <returns>Selected <see cref="Card"/> to play, or null if drawing a card is required.</returns>
        Card? SelectCardToPlay(IReadOnlyList<Card> hand, Card topCard, CardColor activeColor, int nextPlayerCardCount = 7);

        /// <summary>
        /// Selects the optimal suit color when playing a Wild or Wild Draw Four card based on hand distribution.
        /// </summary>
        /// <param name="hand">Cards remaining in bot's hand.</param>
        /// <returns>Chosen <see cref="CardColor"/>.</returns>
        CardColor SelectWildColor(IReadOnlyList<Card> hand);
    }
}
