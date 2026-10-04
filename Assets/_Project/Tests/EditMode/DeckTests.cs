#nullable enable

using NUnit.Framework;
using Uno.Core.Deck;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Tests.Editor
{
    [TestFixture]
    public class DeckTests
    {
        private Deck _deck = null!;

        [SetUp]
        public void Setup()
        {
            _deck = new Deck();
        }

        [Test]
        public void Initialize_GeneratesExactly108Cards()
        {
            Assert.That(_deck.DrawPileCount, Is.EqualTo(108));
            Assert.That(_deck.DiscardPileCount, Is.EqualTo(0));
        }

        [Test]
        public void Draw_DecrementsDrawPile_AndReturnsCard()
        {
            Card card = _deck.Draw();
            Assert.That(card, Is.Not.Null);
            Assert.That(_deck.DrawPileCount, Is.EqualTo(107));
        }

        [Test]
        public void SeededShuffle_ProducesDeterministicOrder()
        {
            Deck deckA = new Deck();
            Deck deckB = new Deck();

            deckA.Shuffle(12345);
            deckB.Shuffle(12345);

            Card cardA = deckA.Draw();
            Card cardB = deckB.Draw();

            Assert.That(cardA.Id, Is.EqualTo(cardB.Id));
            Assert.That(cardA.Color, Is.EqualTo(cardB.Color));
            Assert.That(cardA.Type, Is.EqualTo(cardB.Type));
        }

        [Test]
        public void DiscardRecycling_PreservesTopDiscardCard()
        {
            Card? lastDiscarded = null;

            // Empty the draw pile by discarding cards onto the discard pile
            while (_deck.DrawPileCount > 0)
            {
                lastDiscarded = _deck.Draw();
                _deck.Discard(lastDiscarded.Value);
            }

            Assert.That(_deck.DrawPileCount, Is.EqualTo(0));
            Assert.That(_deck.DiscardPileCount, Is.EqualTo(108));
            Assert.That(_deck.TopDiscardCard?.Id, Is.EqualTo(lastDiscarded?.Id));

            // Draw one card to trigger auto-recycle of the 107 underlying discard cards
            Card drawnCard = _deck.Draw();
            Assert.That(drawnCard, Is.Not.Null);
            Assert.That(_deck.DrawPileCount, Is.EqualTo(106));
            Assert.That(_deck.DiscardPileCount, Is.EqualTo(1));
            Assert.That(_deck.TopDiscardCard?.Id, Is.EqualTo(lastDiscarded?.Id));
        }
    }
}
