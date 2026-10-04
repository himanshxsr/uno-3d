#nullable enable

using System.Collections.Generic;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Core.Rules
{
    /// <summary>
    /// Official UNO rule verification: color match, value/symbol match, and wild priority.
    /// Optional strict Wild Draw Four mode rejects +4 when the player still holds a matching suit card.
    /// </summary>
    public class UnoRuleValidator : IRuleValidator
    {
        private readonly bool _enforceStrictWildDrawFour;

        /// <summary>
        /// Creates a rule validator.
        /// </summary>
        /// <param name="enforceStrictWildDrawFour">
        /// When true, Wild Draw Four is illegal if the hand still contains a card matching the active color.
        /// Official house-rule challenge mode; default false for smoother casual play.
        /// </param>
        public UnoRuleValidator(bool enforceStrictWildDrawFour = false)
        {
            _enforceStrictWildDrawFour = enforceStrictWildDrawFour;
        }

        /// <inheritdoc />
        public bool IsMoveLegal(Card candidate, Card topCard, CardColor activeColor)
        {
            return IsMoveLegal(candidate, topCard, activeColor, null);
        }

        /// <summary>
        /// Evaluates move legality with optional hand context for strict Wild Draw Four checks.
        /// </summary>
        public bool IsMoveLegal(Card candidate, Card topCard, CardColor activeColor, IReadOnlyList<Card>? handContext)
        {
            if (candidate.Type == CardType.Wild)
            {
                return true;
            }

            if (candidate.Type == CardType.WildDrawFour)
            {
                if (!_enforceStrictWildDrawFour || handContext == null)
                {
                    return true;
                }

                return !HasMatchingColorCard(handContext, activeColor, topCard);
            }

            CardColor requiredColor = (activeColor != CardColor.Wild) ? activeColor : topCard.Color;

            if (candidate.Color == requiredColor)
            {
                return true;
            }

            if (candidate.Type != CardType.Number && candidate.Type == topCard.Type)
            {
                return true;
            }

            if (candidate.Type == CardType.Number && topCard.Type == CardType.Number && candidate.Value == topCard.Value)
            {
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public bool CanPlayAnyCard(IReadOnlyList<Card> hand, Card topCard, CardColor activeColor)
        {
            if (hand == null || hand.Count == 0)
            {
                return false;
            }

            int count = hand.Count;
            for (int i = 0; i < count; i++)
            {
                if (IsMoveLegal(hand[i], topCard, activeColor, hand))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMatchingColorCard(IReadOnlyList<Card> hand, CardColor activeColor, Card topCard)
        {
            CardColor requiredColor = (activeColor != CardColor.Wild) ? activeColor : topCard.Color;
            if (requiredColor == CardColor.Wild)
            {
                return false;
            }

            int count = hand.Count;
            for (int i = 0; i < count; i++)
            {
                Card card = hand[i];
                if (card.Type == CardType.Wild || card.Type == CardType.WildDrawFour)
                {
                    continue;
                }

                if (card.Color == requiredColor)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
