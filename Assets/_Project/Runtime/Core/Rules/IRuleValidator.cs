#nullable enable

using System.Collections.Generic;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Core.Rules
{
    /// <summary>
    /// Pure C# rule verification engine for official UNO game legal move validation.
    /// </summary>
    public interface IRuleValidator
    {
        /// <summary>
        /// Determines whether playing a candidate card onto the current Discard pile is legal.
        /// </summary>
        /// <param name="candidate">The card the player intends to play.</param>
        /// <param name="topCard">The current face-up card on top of the Discard pile.</param>
        /// <param name="activeColor">The currently active suit color (set by Wild choice or top card suit).</param>
        /// <returns>True if the move complies with UNO rules; otherwise, false.</returns>
        bool IsMoveLegal(Card candidate, Card topCard, CardColor activeColor);

        /// <summary>
        /// Evaluates whether a player's hand contains at least one legally playable card.
        /// </summary>
        /// <param name="hand">List of cards currently held in the player's hand.</param>
        /// <param name="topCard">The current face-up card on top of the Discard pile.</param>
        /// <param name="activeColor">The currently active suit color.</param>
        /// <returns>True if at least one card is playable; otherwise, false.</returns>
        bool CanPlayAnyCard(IReadOnlyList<Card> hand, Card topCard, CardColor activeColor);
    }
}
