#nullable enable

using System.Collections.Generic;
using Uno.Core.Enums;
using Uno.Core.Models;
using Uno.Core.Rules;

namespace Uno.Application.AI
{
    /// <summary>
    /// Production heuristic AI decision engine evaluating legal plays via multi-factor scoring:
    /// Score = w_match * MatchBonus + w_color * DominantColorBonus + w_threat * ThreatMitigation - w_wild * WildConservation
    /// </summary>
    public class UnoHeuristicAiStrategy : IAIDecisionStrategy
    {
        private readonly IRuleValidator _ruleValidator;

        private const float WeightMatch = 1.0f;
        private const float WeightColor = 1.5f;
        private const float WeightThreat = 2.0f;
        private const float WeightWildConservation = 1.8f;

        public UnoHeuristicAiStrategy(IRuleValidator ruleValidator)
        {
            _ruleValidator = ruleValidator;
        }

        /// <inheritdoc />
        public Card? SelectCardToPlay(IReadOnlyList<Card> hand, Card topCard, CardColor activeColor, int nextPlayerCardCount = 7)
        {
            if (hand == null || hand.Count == 0)
            {
                return null;
            }

            // Find all legal candidates
            List<Card> legalCandidates = new List<Card>(hand.Count);
            int count = hand.Count;
            for (int i = 0; i < count; i++)
            {
                if (_ruleValidator.IsMoveLegal(hand[i], topCard, activeColor))
                {
                    legalCandidates.Add(hand[i]);
                }
            }

            if (legalCandidates.Count == 0)
            {
                return null;
            }

            if (legalCandidates.Count == 1)
            {
                return legalCandidates[0];
            }

            // Evaluate heuristic score for each candidate
            Card bestCard = legalCandidates[0];
            float maxScore = float.MinValue;

            int candidateCount = legalCandidates.Count;
            for (int i = 0; i < candidateCount; i++)
            {
                Card candidate = legalCandidates[i];
                float score = EvaluateHeuristicScore(candidate, hand, topCard, activeColor, nextPlayerCardCount);
                if (score > maxScore)
                {
                    maxScore = score;
                    bestCard = candidate;
                }
            }

            return bestCard;
        }

        private float EvaluateHeuristicScore(Card candidate, IReadOnlyList<Card> hand, Card topCard, CardColor activeColor, int nextPlayerCardCount)
        {
            float score = 0f;

            // 1. Match Bonus
            float matchBonus = 0f;
            CardColor targetColor = (activeColor != CardColor.Wild) ? activeColor : topCard.Color;
            if (candidate.Color == targetColor)
            {
                matchBonus += 10f;
            }
            if (candidate.Type != CardType.Number && candidate.Type == topCard.Type)
            {
                matchBonus += 15f;
            }
            score += WeightMatch * matchBonus;

            // 2. Dominant Color Bonus
            int colorCountInHand = CountCardsOfColor(hand, candidate.Color);
            score += WeightColor * (colorCountInHand * 5f);

            // 3. Threat Mitigation
            float threatMitigation = 0f;
            if (nextPlayerCardCount <= 2 && (candidate.Type == CardType.Skip || candidate.Type == CardType.DrawTwo || candidate.Type == CardType.WildDrawFour || candidate.Type == CardType.Reverse))
            {
                threatMitigation += 30f;
            }
            score += WeightThreat * threatMitigation;

            // 4. Wild Conservation Penalty
            if (candidate.Type == CardType.Wild || candidate.Type == CardType.WildDrawFour)
            {
                // Penalize playing wild if non-wild options exist
                score -= WeightWildConservation * 25f;
            }

            return score;
        }

        /// <inheritdoc />
        public CardColor SelectWildColor(IReadOnlyList<Card> hand)
        {
            if (hand == null || hand.Count == 0)
            {
                return CardColor.Red;
            }

            int redCount = CountCardsOfColor(hand, CardColor.Red);
            int blueCount = CountCardsOfColor(hand, CardColor.Blue);
            int greenCount = CountCardsOfColor(hand, CardColor.Green);
            int yellowCount = CountCardsOfColor(hand, CardColor.Yellow);

            CardColor dominantColor = CardColor.Red;
            int maxCount = redCount;

            if (blueCount > maxCount)
            {
                maxCount = blueCount;
                dominantColor = CardColor.Blue;
            }
            if (greenCount > maxCount)
            {
                maxCount = greenCount;
                dominantColor = CardColor.Green;
            }
            if (yellowCount > maxCount)
            {
                dominantColor = CardColor.Yellow;
            }

            return dominantColor;
        }

        private static int CountCardsOfColor(IReadOnlyList<Card> hand, CardColor color)
        {
            if (color == CardColor.Wild)
            {
                return 0;
            }

            int count = 0;
            int total = hand.Count;
            for (int i = 0; i < total; i++)
            {
                if (hand[i].Color == color)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
