#nullable enable

using System;
using System.Collections.Generic;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Core.Scoring
{
    /// <summary>
    /// Pure C# scoring engine calculating official UNO round points aggregated from opponents' remaining hands.
    /// </summary>
    public static class ScoreCalculator
    {
        /// <summary>
        /// Calculates the point value for an individual card based on standard UNO rules.
        /// </summary>
        /// <param name="card">Target card instance.</param>
        /// <returns>Point value (0-9 for numbers, 20 for actions, 50 for wilds).</returns>
        public static int CalculateCardPoints(Card card)
        {
            return card.Type switch
            {
                CardType.Number => card.Value,
                CardType.Skip => 20,
                CardType.Reverse => 20,
                CardType.DrawTwo => 20,
                CardType.Wild => 50,
                CardType.WildDrawFour => 50,
                _ => 0
            };
        }

        /// <summary>
        /// Sums the total point values of all cards remaining in a single player's hand.
        /// </summary>
        /// <param name="hand">List of cards remaining in hand.</param>
        /// <returns>Total hand point sum.</returns>
        public static int CalculateHandPoints(IReadOnlyList<Card> hand)
        {
            if (hand == null || hand.Count == 0)
            {
                return 0;
            }

            int totalPoints = 0;
            int count = hand.Count;
            for (int i = 0; i < count; i++)
            {
                totalPoints += CalculateCardPoints(hand[i]);
            }

            return totalPoints;
        }

        /// <summary>
        /// Aggregates the total score awarded to the round winner by summing remaining card points across all opponents' hands.
        /// </summary>
        /// <param name="opponentsHands">Collection of all opponents' hands.</param>
        /// <returns>Total round points awarded to the winner.</returns>
        public static int CalculateRoundScore(IEnumerable<IReadOnlyList<Card>> opponentsHands)
        {
            if (opponentsHands == null)
            {
                return 0;
            }

            int roundTotalScore = 0;
            foreach (IReadOnlyList<Card> opponentHand in opponentsHands)
            {
                roundTotalScore += CalculateHandPoints(opponentHand);
            }

            return roundTotalScore;
        }
    }
}
