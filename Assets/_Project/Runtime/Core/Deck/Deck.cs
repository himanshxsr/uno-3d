#nullable enable

using System;
using System.Collections.Generic;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Core.Deck
{
    /// <summary>
    /// Production implementation of the UNO deck manager handling 108-card deck initialization,
    /// seedable Fisher-Yates shuffling, draw stack pops, discard pile pushes, and automatic recycling.
    /// </summary>
    public class Deck : IDeck
    {
        private readonly List<Card> _drawStack = new List<Card>(108);
        private readonly List<Card> _discardStack = new List<Card>(108);

        /// <inheritdoc />
        public int DrawPileCount => _drawStack.Count;

        /// <inheritdoc />
        public int DiscardPileCount => _discardStack.Count;

        /// <inheritdoc />
        public Card? TopDiscardCard => _discardStack.Count > 0 ? _discardStack[_discardStack.Count - 1] : null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Deck"/> class and generates all 108 cards.
        /// </summary>
        public Deck()
        {
            Initialize();
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _drawStack.Clear();
            _discardStack.Clear();

            CardColor[] colors = new[] { CardColor.Red, CardColor.Blue, CardColor.Green, CardColor.Yellow };

            // 1. Generate Suited Number Cards (0-9)
            foreach (CardColor color in colors)
            {
                string colorPrefix = color.ToString().ToLowerInvariant();

                // Exactly one '0' card per color suit (4 cards total)
                _drawStack.Add(Card.CreateNumber($"{colorPrefix}_0_1", color, 0));

                // Two of each '1' through '9' cards per color suit (72 cards total)
                for (int num = 1; num <= 9; num++)
                {
                    _drawStack.Add(Card.CreateNumber($"{colorPrefix}_{num}_1", color, num));
                    _drawStack.Add(Card.CreateNumber($"{colorPrefix}_{num}_2", color, num));
                }

                // Two of each Action Card per color suit (24 cards total: 8 Skip, 8 Reverse, 8 DrawTwo)
                _drawStack.Add(Card.CreateAction($"{colorPrefix}_skip_1", color, CardType.Skip));
                _drawStack.Add(Card.CreateAction($"{colorPrefix}_skip_2", color, CardType.Skip));

                _drawStack.Add(Card.CreateAction($"{colorPrefix}_reverse_1", color, CardType.Reverse));
                _drawStack.Add(Card.CreateAction($"{colorPrefix}_reverse_2", color, CardType.Reverse));

                _drawStack.Add(Card.CreateAction($"{colorPrefix}_drawtwo_1", color, CardType.DrawTwo));
                _drawStack.Add(Card.CreateAction($"{colorPrefix}_drawtwo_2", color, CardType.DrawTwo));
            }

            // 2. Generate Neutral Wild Cards (8 cards total: 4 Wild, 4 WildDrawFour)
            for (int i = 1; i <= 4; i++)
            {
                _drawStack.Add(Card.CreateWild($"wild_{i}", CardType.Wild));
                _drawStack.Add(Card.CreateWild($"wild_drawfour_{i}", CardType.WildDrawFour));
            }

            if (_drawStack.Count != 108)
            {
                throw new InvalidOperationException($"Deck generation error: expected exactly 108 cards, but generated {_drawStack.Count}.");
            }
        }

        /// <inheritdoc />
        public void Shuffle(int? seed = null)
        {
            Random random = seed.HasValue ? new Random(seed.Value) : new Random();

            // In-place Fisher-Yates Shuffle
            int n = _drawStack.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Card temp = _drawStack[i];
                _drawStack[i] = _drawStack[j];
                _drawStack[j] = temp;
            }
        }

        /// <inheritdoc />
        public Card Draw()
        {
            if (_drawStack.Count == 0)
            {
                RecycleDiscardIntoDrawPile();
            }

            if (_drawStack.Count == 0)
            {
                throw new InvalidOperationException("Cannot draw card: both Draw pile and Discard pile are completely empty.");
            }

            int lastIndex = _drawStack.Count - 1;
            Card drawnCard = _drawStack[lastIndex];
            _drawStack.RemoveAt(lastIndex);
            return drawnCard;
        }

        /// <inheritdoc />
        public IReadOnlyList<Card> DrawMultiple(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Draw count cannot be negative.");
            }

            List<Card> drawnCards = new List<Card>(count);
            for (int i = 0; i < count; i++)
            {
                drawnCards.Add(Draw());
            }

            return drawnCards;
        }

        /// <inheritdoc />
        public void Discard(Card card)
        {
            _discardStack.Add(card);
        }

        /// <inheritdoc />
        public void RecycleDiscardIntoDrawPile()
        {
            if (_discardStack.Count <= 1)
            {
                return; // Nothing to recycle if 0 or 1 card in discard
            }

            // Preserve top discard card
            int topIndex = _discardStack.Count - 1;
            Card topCard = _discardStack[topIndex];
            _discardStack.RemoveAt(topIndex);

            // Move remaining discard cards into draw stack
            _drawStack.AddRange(_discardStack);
            _discardStack.Clear();
            _discardStack.Add(topCard);

            // Shuffle recycled draw stack
            Shuffle();
        }
    }
}
